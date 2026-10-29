using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

return await MainAsync(args);

static async Task<int> MainAsync(string[] args)
{
    try
    {
        var root = FindRoot();
        var config = Evaluation.Read(Path.Combine(root, "candidate.json"));
        var fixtures = Evaluation.Read(Path.Combine(root, "fixtures.json")).AsArray();
        var prompt = File.ReadAllText(Path.Combine(root, "prompt.txt"));
        var schema = Evaluation.Read(Path.Combine(root, "response-schema.json"));
        Evaluation.ValidatePreparation(config, fixtures);
        var command = args.FirstOrDefault() ?? "dry-run";
        if (command == "self-check")
        {
            await OfflineChecks.RunAsync(root, config, fixtures, prompt, schema);
            return 0;
        }
        if (command is "dry-run" or "dry-run-d1")
        {
            var scope = command == "dry-run-d1" ? "d1" : "batch";
            if (scope == "d1" && args.Length != 1) throw new InvalidDataException("dry-run-d1 accepts no additional arguments");
            var route = scope == "d1" ? "native" : args.ElementAtOrDefault(1) ?? "native";
            if (route is not ("native" or "compatible")) throw new InvalidDataException("Route must be native or compatible");
            var selected = Evaluation.SelectCases(fixtures, scope);
            var fingerprint = Evaluation.Fingerprint(root);
            var directory = Path.Combine(root, "results", "preparation", Evaluation.Text(fingerprint["sha256"]), scope == "d1" ? "native-d1" : route);
            Directory.CreateDirectory(directory);
            var requests = new JsonArray();
            foreach (var fixture in selected)
            {
                var body = Evaluation.Request(fixture!, config, prompt, schema, route);
                var id = Evaluation.Text(fixture!["id"]);
                WriteJson(Path.Combine(directory, $"{id}.request.json"), body);
                requests.Add(new JsonObject
                {
                    ["caseId"] = id, ["path"] = Evaluation.RoutePath(route),
                    ["requestSha256"] = Evaluation.Hash(Encoding.UTF8.GetBytes(body.ToJsonString())),
                    ["sizing"] = Evaluation.Sizing(body, config),
                    ["expected"] = fixture["expected"]!.DeepClone()
                });
            }
            var manifest = new JsonObject
            {
                ["status"] = "preparation only; zero HTTP requests; execution not approved",
                ["fingerprint"] = fingerprint, ["route"] = route, ["scope"] = scope,
                ["caseIds"] = new JsonArray(Evaluation.ScopeCaseIds(scope).Select(id => (JsonNode?)JsonValue.Create(id)).ToArray()),
                ["requestCount"] = selected.Count,
                ["timeoutSecondsPerRequest"] = 120, ["maximumRequestSeconds"] = selected.Count * 120,
                ["limitsStatus"] = config["limitsStatus"]!.DeepClone(),
                ["routeLimitations"] = route == "compatible"
                    ? "Adapter preview only. Context/top-k/min-p need reviewed server-side configuration; native/compatible equivalence has not been established. Live compatible execution is not enabled in this checkpoint."
                    : $"Native-only {scope} proposal; CPU offload permitted; semantic review required.",
                ["requests"] = requests
            };
            WriteJson(Path.Combine(directory, "manifest.json"), manifest);
            Console.WriteLine(manifest.ToJsonString(Evaluation.Pretty));
            Console.WriteLine($"Review files: {directory}");
            return 0;
        }
        if ((command is "run" or "run-d1") && args.Length == 2)
        {
            await RunAsync(root, config, fixtures, prompt, schema, args[1], command == "run-d1" ? "d1" : "batch");
            return 0;
        }
        Console.Error.WriteLine("Commands: dry-run [native|compatible], dry-run-d1, self-check, run|run-d1 <separately-approved-local-file.json>");
        return 1;
    }
    catch (Exception exception) when (exception is not OutOfMemoryException)
    {
        Console.Error.WriteLine($"Stopped: {exception.Message}");
        return 1;
    }
}

static string FindRoot()
{
    // No machine-specific paths or copied runtime configuration.
    for (var directory = new DirectoryInfo(Environment.CurrentDirectory); directory is not null; directory = directory.Parent)
    {
        var nested = Path.Combine(directory.FullName, "tools", "inference-evaluation");
        if (File.Exists(Path.Combine(nested, "candidate.json"))) return nested;
        if (File.Exists(Path.Combine(directory.FullName, "candidate.json"))
            && File.Exists(Path.Combine(directory.FullName, "InferenceEvaluation.csproj"))) return directory.FullName;
    }
    throw new InvalidDataException("Run from the repository root or tools/inference-evaluation");
}

static void WriteJson(string path, JsonNode value) => File.WriteAllText(path, value.ToJsonString(Evaluation.Pretty) + "\n", new UTF8Encoding(false));

static async Task RunAsync(string root, JsonNode config, JsonArray fixtures, string prompt, JsonNode schema, string approvalPath, string scope)
{
    var approval = Evaluation.Read(approvalPath);
    var fingerprint = Evaluation.Fingerprint(root);
    Evaluation.ValidateApproval(approval, fingerprint, scope);
    fixtures = Evaluation.SelectCases(fixtures, scope);
    foreach (var fixture in fixtures)
        if (Evaluation.Sizing(Evaluation.Request(fixture!, config, prompt, schema, "native"), config)["withinConservativeBound"]!.GetValue<bool>() != true)
            throw new InvalidDataException("Input exceeds conservative context bound; review limits instead of truncating");

    var results = Path.Combine(root, "results");
    Directory.CreateDirectory(results);
    using var runLock = new FileStream(Path.Combine(results, "execution.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    var runDirectory = Path.Combine(results, Evaluation.Text(approval["runId"]));
    Directory.CreateDirectory(runDirectory);
    // Claim once before any HTTP request. Even a failed outset cannot reuse a consumed run approval.
    using (var claim = new FileStream(Path.Combine(runDirectory, "claim.json"), FileMode.CreateNew, FileAccess.Write))
    {
        var bytes = Encoding.UTF8.GetBytes(approval.ToJsonString(Evaluation.Pretty));
        claim.Write(bytes);
    }
    var snapshot = Path.Combine(runDirectory, "inputs");
    Directory.CreateDirectory(snapshot);
    foreach (var file in Evaluation.InputFiles)
        File.Copy(Path.Combine(root, file), Path.Combine(snapshot, Path.GetFileName(file)), overwrite: false);
    WriteJson(Path.Combine(runDirectory, "fingerprint.json"), fingerprint);
    using var stop = new CancellationTokenSource();
    ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; stop.Cancel(); };
    Console.CancelKeyPress += cancel;
    using var handler = new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false };
    using var client = new HttpClient(handler) { BaseAddress = new Uri(Evaluation.Text(config["baseUrl"])), Timeout = Timeout.InfiniteTimeSpan };
    var ledger = Path.Combine(runDirectory, "attempts.jsonl");
    try
    {
        JsonNode metadata;
        try
        {
            using var preflight = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
            preflight.CancelAfter(TimeSpan.FromSeconds(15));
            var version = Evaluation.Parse(await client.GetStringAsync("api/version", preflight.Token));
            var tags = Evaluation.Parse(await client.GetStringAsync("api/tags", preflight.Token));
            var model = tags["models"]!.AsArray().SingleOrDefault(m => Evaluation.Text(m!["name"]) == Evaluation.Text(config["model"]))
                ?? throw new InvalidDataException("Selected model is not installed; no model will be downloaded automatically");
            if (Evaluation.Text(model["digest"]) != Evaluation.Text(approval["modelDigest"])
                || Evaluation.Text(version["version"]) != Evaluation.Text(approval["ollamaVersion"]))
                throw new InvalidDataException("Installed model/version differs from approved execution record");
            var show = Evaluation.Parse(await Evaluation.SendAsync(client, "api/show", new JsonObject { ["model"] = config["model"]!.DeepClone() }, TimeSpan.FromSeconds(15), stop.Token));
            if (model["remote_host"] is not null || model["remote_model"] is not null
                || show["remote_host"] is not null || show["remote_model"] is not null)
                throw new InvalidDataException("Remote/cloud-backed models are outside the local experiment scope");
            if (show["model_info"]?["tokenizer.ggml.model"]?.GetValue<string>() != "gpt2")
                throw new InvalidDataException("Byte-level tokenizer prerequisite has not been verified");
            var template = Evaluation.Text(show["template"]);
            if (Evaluation.Hash(Encoding.UTF8.GetBytes(template)) != Evaluation.Text(approval["templateSha256"]))
                throw new InvalidDataException("Chat template differs from reviewed execution record");
            metadata = new JsonObject
            {
                ["model"] = config["model"]!.DeepClone(), ["modelDigest"] = model["digest"]!.DeepClone(),
                ["ollamaVersion"] = version["version"]!.DeepClone(), ["details"] = show["details"]?.DeepClone(),
                ["templateSha256"] = approval["templateSha256"]!.DeepClone(), ["dotnetRuntime"] = Environment.Version.ToString()
            };
            WriteJson(Path.Combine(runDirectory, "metadata.json"), metadata);
        }
        catch (Exception ex)
        {
            WriteJson(Path.Combine(runDirectory, "stopped.json"), new JsonObject
            { ["code"] = stop.IsCancellationRequested ? "interrupted" : "inferenceUnavailable", ["message"] = ex.Message, ["inferenceRequests"] = 0 });
            throw new InvalidDataException("Run stopped before inference; inspect stopped.json", ex);
        }

        async Task AppendAsync(JsonObject record) => await File.AppendAllTextAsync(ledger, record.ToJsonString() + "\n");
        for (var index = 0; index < fixtures.Count && !stop.IsCancellationRequested; index++)
        {
            var fixture = fixtures[index]!;
            var id = Evaluation.Text(fixture["id"]);
            var body = Evaluation.Request(fixture, config, prompt, schema, "native");
            WriteJson(Path.Combine(runDirectory, $"{id}.request.json"), body);
            await AppendAsync(new JsonObject { ["event"] = "started", ["caseId"] = id, ["atUtc"] = DateTimeOffset.UtcNow.ToString("O") });
            var record = new JsonObject { ["event"] = "finished", ["caseId"] = id, ["index"] = index, ["semanticReview"] = "pending" };
            var watch = Stopwatch.StartNew();
            var mustStop = false;
            try
            {
                var raw = await Evaluation.SendAsync(client, "api/chat", body, TimeSpan.FromSeconds(120), stop.Token);
                var received = Evaluation.Parse(raw);
                record["content"] = received["message"]?["content"]?.DeepClone();
                record["metrics"] = Evaluation.NativeMetrics(received);
                var unpacked = Evaluation.Unpack(raw, "native");
                // Retain structured answer text even when its schema/evidence is invalid; never retain raw thinking.
                record["content"] = unpacked.Content;
                record["metrics"] = unpacked.Metrics;
                record["validation"] = Evaluation.ValidateAnswer(unpacked.Content, fixture);
                record["state"] = "completed";
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            { record["state"] = "interrupted"; mustStop = true; }
            catch (EvaluationFailure ex)
            {
                record["state"] = "failed";
                record["failure"] = new JsonObject { ["code"] = ex.Code, ["message"] = ex.Message };
                // Stop after timeout: review server activity before scheduling another request.
                mustStop = ex.Code == "inferenceTimeout";
            }
            catch (HttpRequestException ex)
            {
                record["state"] = "failed";
                record["failure"] = new JsonObject { ["code"] = "inferenceError", ["message"] = ex.Message };
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or InvalidDataException)
            {
                record["state"] = "failed";
                record["failure"] = new JsonObject { ["code"] = "invalidResponse", ["message"] = ex.Message };
            }
            finally
            {
                record["durationMs"] = watch.Elapsed.TotalMilliseconds;
                record["atUtc"] = DateTimeOffset.UtcNow.ToString("O");
                try
                {
                    using var inspectDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    var running = Evaluation.Parse(await client.GetStringAsync("api/ps", inspectDeadline.Token));
                    var loaded = running["models"]?.AsArray().FirstOrDefault(m => Evaluation.Text(m!["name"]) == Evaluation.Text(config["model"]));
                    record["residency"] = loaded is null ? null : new JsonObject
                    {
                        ["sizeBytes"] = loaded["size"]?.DeepClone(), ["gpuSizeBytes"] = loaded["size_vram"]?.DeepClone(),
                        ["contextTokens"] = loaded["context_length"]?.DeepClone()
                    };
                }
                catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException or InvalidDataException)
                { record["residencyObservation"] = "unavailable; do not infer full GPU residency"; }
                await AppendAsync(record);
            }
            Console.WriteLine($"{id}: {record["state"]}; {watch.Elapsed.TotalSeconds:F1}s; semantic review pending");
            if (mustStop) break;
        }
        WriteJson(Path.Combine(runDirectory, "checkpoint.json"), new JsonObject
        {
            ["status"] = "stopped for human review; no retries or next batch", ["scope"] = scope,
            ["caseIds"] = approval["caseIds"]!.DeepClone(), ["interrupted"] = stop.IsCancellationRequested,
            ["atUtc"] = DateTimeOffset.UtcNow.ToString("O")
        });
    }
    finally { Console.CancelKeyPress -= cancel; }
}
