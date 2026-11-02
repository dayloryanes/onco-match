using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

internal static class ComparisonPilot
{
    internal const string ReviewedPreparation = "2df57b35d043143ccce8bf48dcfcdd8a31ab32e814ad3d5c0ac9b7865b2b8a98";
    internal static JsonObject Fingerprint(string root) => ComparisonProfile.Fingerprint(root);

    internal static void ValidateProductionRun(string root, JsonNode config, JsonNode approval)
    {
        if (Evaluation.Text(approval["runId"]) != Evaluation.Text(config["profileId"]) + "-1")
            throw new InvalidDataException("Use the fixed single-use profile run ID");
        throw new InvalidDataException("Inference deferred; all previous approvals consumed. Fresh execution design and human approval required.");
    }

    internal static void ValidateApproval(JsonNode approval, JsonNode config, JsonNode fingerprint)
    {
        if (approval["candidatePreparationFingerprintSha256"]?.GetValue<string>() != ComparisonProfile.CandidatePreparation
            || approval["promptSha256"]?.GetValue<string>() != ComparisonProfile.CandidatePromptSha256
            || approval["continueAfterTimeoutReviewed"]?.GetValue<bool>() != true
            || approval["approved"]?.GetValue<bool>() != true
            || approval["contextAndOutputLimitsReviewed"]?.GetValue<bool>() != true
            || approval["templateAndTokenizerReviewed"]?.GetValue<bool>() != true
            || Evaluation.Text(approval["contract"]) != "condition-label-gpt-development-full-v1"
            || Evaluation.Text(approval["fingerprintSha256"]) != Evaluation.Text(fingerprint["sha256"])
            || Evaluation.Text(approval["preparationFingerprintSha256"]) != ReviewedPreparation
            || Evaluation.Text(approval["profileId"]) != Evaluation.Text(config["profileId"])
            || Evaluation.Text(approval["model"]) != Evaluation.Text(config["model"])
            || !JsonNode.DeepEquals(approval["think"], config["think"])
            || Evaluation.Text(approval["modelDigest"]) != Evaluation.Text(config["modelDigest"])
            || Evaluation.Text(approval["templateSha256"]) != Evaluation.Text(config["templateSha256"])
            || Evaluation.Text(approval["ollamaVersion"]) != Evaluation.Text(config["ollamaVersion"])
            || Evaluation.Text(approval["route"]) != "native"
            || approval["maxRequests"]?.GetValue<int>() != config["caseIds"]!.AsArray().Count
            || !JsonNode.DeepEquals(approval["caseIds"], config["caseIds"]))
            throw new InvalidDataException("Missing/mismatched fresh approval for this exact installed-model comparison profile");
        var runId = Evaluation.Text(approval["runId"]);
        if (!Regex.IsMatch(runId, "^[a-z0-9][a-z0-9-]{0,63}$") || runId == "preparation")
            throw new InvalidDataException("Use a unique lowercase run ID without path separators");
        foreach (var field in new[] { "modelDigest", "templateSha256" })
            if (!Regex.IsMatch(Evaluation.Text(approval[field]), "^[a-f0-9]{64}$")) throw new InvalidDataException($"Exact {field} required");
        if (string.IsNullOrWhiteSpace(Evaluation.Text(approval["ollamaVersion"]))
            || string.IsNullOrWhiteSpace(Evaluation.Text(approval["humanApprovalReference"])))
            throw new InvalidDataException("Actual human execution approval and installed version required");
    }

    internal static void ValidatePreviews(string root, JsonArray fixtures, JsonNode config, string prompt)
    {
        var fingerprint = Fingerprint(root);
        var directory = Path.Combine(root, "results/preparation", Evaluation.Text(fingerprint["sha256"]));
        var manifest = Evaluation.Read(Path.Combine(directory, "manifest.json"));
        var profileId = Evaluation.Text(config["profileId"]);
        var entry = manifest["profiles"]!.AsArray().Single(p => Evaluation.Text(p!["profileId"]) == profileId)!;
        if (!JsonNode.DeepEquals(entry["configuration"], config)) throw new InvalidDataException("Prepared profile configuration differs");
        foreach (var item in entry["requests"]!.AsArray())
        {
            var id = Evaluation.Text(item!["caseId"]);
            var fixture = fixtures.Single(f => Evaluation.Text(f!["id"]) == id)!;
            var bytes = File.ReadAllBytes(Path.Combine(directory, profileId, id + ".request.json"));
            if (Evaluation.Hash(bytes) != Evaluation.Text(item["requestFileSha256"])
                || !JsonNode.DeepEquals(Evaluation.Parse(Encoding.UTF8.GetString(bytes)), ComparisonProfile.Request(fixture, config, prompt)))
                throw new InvalidDataException("Prepared request changed: " + id);
        }
    }

    internal static JsonObject Residency(JsonNode response, string model)
    {
        var loaded = response["models"]!.AsArray().SingleOrDefault(m => Evaluation.Text(m!["name"]) == model);
        if (loaded is null) return new JsonObject { ["status"] = "candidate not reported loaded; memory unknown" };
        return new JsonObject
        {
            ["status"] = "point-in-time model allocation; not peak process/system memory",
            ["sizeBytes"] = loaded["size"]?.DeepClone(), ["gpuSizeBytes"] = loaded["size_vram"]?.DeepClone(),
            ["contextTokens"] = loaded["context_length"]?.DeepClone()
        };
    }

    internal static async Task RunAsync(string root, JsonArray fixtures, string prompt, JsonNode config, string approvalPath, HttpMessageHandler? testHandler = null)
    {
        ComparisonProfile.ValidateConfig(root, config, fixtures);
        var fingerprint = Fingerprint(root);
        var approval = Evaluation.Read(approvalPath);
        ValidateApproval(approval, config, fingerprint);
        ValidatePreviews(root, fixtures, config, prompt);
        if (testHandler is null)
        {
            ValidateProductionRun(root, config, approval);
            ComparisonProfile.ValidateOrder(root, config, approval);
        }
        var ids = config["caseIds"]!.AsArray().Select(Evaluation.Text).ToArray();
        var selected = ids.Select(id => fixtures.Single(f => Evaluation.Text(f!["id"]) == id)!).ToArray();
        foreach (var fixture in selected)
            if (!Evaluation.Sizing(ComparisonProfile.Request(fixture, config, prompt), config)["withinConservativeBound"]!.GetValue<bool>())
                throw new InvalidDataException("inputTooLarge: revise approved limits; never truncate");
        var results = Path.Combine(root, "results");
        Directory.CreateDirectory(results);
        var sharedLockDirectory = results;
        Directory.CreateDirectory(sharedLockDirectory);
        using var runLock = new FileStream(Path.Combine(sharedLockDirectory, "execution.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var directory = Path.Combine(results, Evaluation.Text(approval["runId"]));
        Directory.CreateDirectory(directory);
        void Write(string file, JsonNode value) => File.WriteAllText(Path.Combine(directory, file), value.ToJsonString(Evaluation.Pretty) + "\n", new UTF8Encoding(false));
        // Claim before all HTTP calls; even failed preflight consumes this approval.
        using (var claim = new FileStream(Path.Combine(directory, "claim.json"), FileMode.CreateNew, FileAccess.Write))
            claim.Write(Encoding.UTF8.GetBytes(approval.ToJsonString(Evaluation.Pretty)));
        Write("fingerprint.json", fingerprint);
        Write("development-inputs.json", new JsonArray(selected.Select(f => f.DeepClone()).ToArray()));
        var snapshots = Path.Combine(directory, "inputs");
        Directory.CreateDirectory(snapshots);
        var index = 0;
        foreach (var file in fingerprint["files"]!.AsObject())
        {
            var bytes = File.ReadAllBytes(Path.Combine(root, file.Key));
            if (Evaluation.Hash(bytes) != Evaluation.Text(file.Value)) throw new InvalidDataException("Input changed after approval validation");
            File.WriteAllBytes(Path.Combine(snapshots, $"{index++:00}-{Path.GetFileName(file.Key)}"), bytes);
        }
        using var userStop = new CancellationTokenSource();
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(userStop.Token);
        operation.CancelAfter(TimeSpan.FromSeconds(config["operationTimeoutSeconds"]!.GetValue<int>()));
        ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; userStop.Cancel(); };
        Console.CancelKeyPress += cancel;
        using var handler = testHandler ?? new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:11434/"), Timeout = Timeout.InfiniteTimeSpan };
        var total = Stopwatch.StartNew();
        var utcStarted = DateTimeOffset.UtcNow;
        var operationDeadline = utcStarted.AddSeconds(config["operationTimeoutSeconds"]!.GetValue<int>());
        var deadlineWatch = WatchDeadlineAsync(operationDeadline, operation);
        var timings = new List<double>();
        var counts = new Dictionary<string, int> { ["completed"] = 0, ["failed"] = 0, ["interrupted"] = 0 };
        try
        {
            try
            {
                using var preflight = CancellationTokenSource.CreateLinkedTokenSource(operation.Token);
                preflight.CancelAfter(TimeSpan.FromSeconds(15));
                var version = Evaluation.Parse(await client.GetStringAsync("api/version", preflight.Token));
                var tags = Evaluation.Parse(await client.GetStringAsync("api/tags", preflight.Token));
                var model = tags["models"]!.AsArray().SingleOrDefault(m => Evaluation.Text(m!["name"]) == Evaluation.Text(config["model"]))
                    ?? throw new InvalidDataException("Approved model not installed; no automatic download");
                var show = Evaluation.Parse(await Evaluation.SendAsync(client, "api/show", new JsonObject { ["model"] = config["model"]!.DeepClone() }, TimeSpan.FromSeconds(15), preflight.Token));
                if (Evaluation.Text(version["version"]) != Evaluation.Text(approval["ollamaVersion"])
                    || Evaluation.Text(model["digest"]) != Evaluation.Text(approval["modelDigest"])
                    || Evaluation.Hash(Encoding.UTF8.GetBytes(Evaluation.Text(show["template"]))) != Evaluation.Text(approval["templateSha256"])
                    || show["model_info"]?["tokenizer.ggml.model"]?.GetValue<string>() != "gpt2"
                    || show["model_info"]?["tokenizer.ggml.pre"]?.GetValue<string>() != Evaluation.Text(config["preTokenizer"])
                    || model["details"]?["runner"]?.GetValue<string>() != Evaluation.Text(config["runner"])
                    || !JsonNode.DeepEquals(show["thinking"], config["thinkingMetadata"])
                    || Evaluation.Text(show["template_source"]) != Evaluation.Text(config["templateSource"])
                    || !JsonNode.DeepEquals(show["renderer"], config["renderer"]) || !JsonNode.DeepEquals(show["parser"], config["parser"])
                    || model["remote_host"] is not null || model["remote_model"] is not null || show["remote_host"] is not null || show["remote_model"] is not null)
                    throw new InvalidDataException("Installed version/model/template/tokenizer differs or model is remote");
                Write("metadata.json", new JsonObject
                {
                    ["profileId"] = config["profileId"]!.DeepClone(), ["phase"] = config["phase"]!.DeepClone(),
                    ["runner"] = model["details"]?["runner"]?.DeepClone(), ["renderer"] = show["renderer"]?.DeepClone(),
                    ["thinkingMetadata"] = show["thinking"]?.DeepClone(),
                    ["preTokenizer"] = show["model_info"]?["tokenizer.ggml.pre"]?.DeepClone(),
                    ["ollamaVersion"] = version["version"]!.DeepClone(), ["modelDigest"] = model["digest"]!.DeepClone(),
                    ["templateSha256"] = approval["templateSha256"]!.DeepClone(), ["template"] = show["template"]?.DeepClone(),
                    ["parameters"] = show["parameters"]?.DeepClone(), ["thinking"] = show["thinking"]?.DeepClone(),
                    ["capabilities"] = show["capabilities"]?.DeepClone(), ["runtime"] = Environment.Version.ToString()
                });
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Write("stopped.json", new JsonObject { ["code"] = operation.IsCancellationRequested ? "interrupted" : "inferenceUnavailable", ["message"] = ex.Message, ["inferenceRequests"] = 0 });
                throw new InvalidDataException("Preflight stopped; no inference requests; approval consumed", ex);
            }
            for (var i = 0; i < selected.Length && !operation.IsCancellationRequested; i++)
            {
                if (DateTimeOffset.UtcNow >= operationDeadline) { operation.Cancel(); break; }
                var fixture = selected[i];
                Console.WriteLine($"Starting {Evaluation.Text(config["profileId"])} development case D{int.Parse(ids[i][4..])} ({ids[i]})");
                var body = ComparisonProfile.Request(fixture, config, prompt);
                Write($"{ids[i]}.request.json", body);
                var ledger = Path.Combine(directory, "attempts.jsonl");
                await File.AppendAllTextAsync(ledger, new JsonObject { ["event"] = "started", ["caseId"] = ids[i], ["atUtc"] = DateTimeOffset.UtcNow.ToString("O") }.ToJsonString() + "\n");
                var record = new JsonObject
                {
                    ["event"] = "finished", ["caseId"] = ids[i], ["semanticReview"] = "pending",
                    ["profileId"] = config["profileId"]!.DeepClone(), ["phase"] = config["phase"]!.DeepClone(),
                    ["requestedThink"] = config["think"]?.DeepClone(),
                    ["thinkFieldPresent"] = config["think"] is not null
                };
                var watch = Stopwatch.StartNew();
                var requestStarted = DateTimeOffset.UtcNow;
                var stopAfter = false;
                try
                {
                    var raw = await SendInferenceAsync(client, body, TimeSpan.FromSeconds(config["requestTimeoutSeconds"]!.GetValue<int>()), operation.Token);
                    var envelope = Evaluation.Parse(raw);
                    if (Evaluation.Text(envelope["model"]) != Evaluation.Text(config["model"]))
                        throw new EvaluationFailure("modeMismatch", "Returned model identity differs");
                    // Never save the raw provider response or message.thinking.
                    var content = Evaluation.Text(envelope["message"]?["content"]);
                    if (content.Contains("<think>", StringComparison.OrdinalIgnoreCase) || content.Contains("</think>", StringComparison.OrdinalIgnoreCase)
                        || content.Contains("<|channel|>", StringComparison.Ordinal) || content.Contains("<|start|>", StringComparison.Ordinal))
                        throw new EvaluationFailure("invalidResponse", "Inline thinking content withheld from evidence");
                    record["content"] = content;
                    record["doneReason"] = Evaluation.Text(envelope["done_reason"]) is "stop" or "length" ? Evaluation.Text(envelope["done_reason"]) : "other";
                    record["metrics"] = Evaluation.NativeMetrics(envelope);
                    var unpacked = Evaluation.Unpack(raw, "native");
                    if (Evaluation.Text(config["phase"]) == "off" && unpacked.Metrics["reasoningObserved"]!.GetValue<bool>())
                        throw new EvaluationFailure("modeMismatch", "Observed thinking in an off profile; stop for review");
                    record["validation"] = Labels.Validate(unpacked.Content, fixture);
                    record["state"] = "completed";
                }
                catch (OperationCanceledException)
                { record["state"] = "interrupted"; stopAfter = true; }
                catch (EvaluationFailure ex)
                {
                    record["state"] = "failed";
                    record["failure"] = new JsonObject { ["code"] = ex.Code, ["message"] = ex.Message };
                    stopAfter = ex.Code is "inferenceError" or "modeMismatch";
                }
                catch (HttpRequestException ex)
                {
                    record["state"] = "failed";
                    record["failure"] = new JsonObject { ["code"] = "inferenceError", ["message"] = ex.Message };
                    stopAfter = true;
                }
                catch (Exception ex) when (ex is JsonException or InvalidDataException or InvalidOperationException)
                {
                    record["state"] = "failed";
                    record["failure"] = new JsonObject { ["code"] = "invalidResponse", ["message"] = ex.Message };
                }
                var elapsed = (DateTimeOffset.UtcNow - requestStarted).TotalMilliseconds;
                timings.Add(elapsed);
                counts[Evaluation.Text(record["state"])]++;
                record["durationMs"] = elapsed;
                record["activeClockDurationMs"] = watch.Elapsed.TotalMilliseconds;
                if (!stopAfter && !operation.IsCancellationRequested)
                {
                    try
                    {
                        using var inspect = CancellationTokenSource.CreateLinkedTokenSource(operation.Token);
                        inspect.CancelAfter(TimeSpan.FromSeconds(5));
                        var loaded = Evaluation.Parse(await client.GetStringAsync("api/ps", inspect.Token));
                        record["residency"] = Residency(loaded, Evaluation.Text(config["model"]));
                    }
                    catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or InvalidDataException or InvalidOperationException)
                    { record["residency"] = new JsonObject { ["status"] = "observation unavailable; memory unknown" }; }
                }
                record["atUtc"] = DateTimeOffset.UtcNow.ToString("O");
                await File.AppendAllTextAsync(ledger, record.ToJsonString() + "\n");
                Console.WriteLine($"{ids[i]}: {record["state"]}; {elapsed / 1000:F1}s; semantic review pending");
                if (stopAfter) break;
            }
        }
        finally
        {
            Console.CancelKeyPress -= cancel;
            var aggregateDeadlineReached = DateTimeOffset.UtcNow >= operationDeadline && !userStop.IsCancellationRequested;
            operation.Cancel();
            await deadlineWatch;
            var ordered = timings.Order().ToArray();
            Write("checkpoint.json", new JsonObject
            {
                ["status"] = "profile recorded; no retries; next profile requires valid ordering and fresh approval",
                ["attemptedRequests"] = timings.Count,
                ["completed"] = counts["completed"], ["failed"] = counts["failed"], ["interrupted"] = counts["interrupted"],
                ["medianMs"] = ordered.Length == 0 ? null : ordered.Length % 2 == 1 ? ordered[ordered.Length / 2] : (ordered[ordered.Length / 2 - 1] + ordered[ordered.Length / 2]) / 2,
                ["slowestMs"] = ordered.Length == 0 ? null : ordered[^1], ["requestTotalMs"] = timings.Sum(), ["operationTotalMs"] = (DateTimeOffset.UtcNow - utcStarted).TotalMilliseconds,
                ["activeClockOperationMs"] = total.Elapsed.TotalMilliseconds,
                ["aggregateDeadlineReached"] = aggregateDeadlineReached,
                ["semanticReview"] = "pending", ["overallPass"] = null
            });
        }
    }

    private static async Task WatchDeadlineAsync(DateTimeOffset deadline, CancellationTokenSource source)
    {
        try
        {
            while (!source.IsCancellationRequested)
            {
                if (DateTimeOffset.UtcNow >= deadline) { source.Cancel(); return; }
                await Task.Delay(TimeSpan.FromMilliseconds(250), source.Token);
            }
        }
        catch (OperationCanceledException) when (source.IsCancellationRequested) { }
    }

    internal static async Task<string> SendInferenceAsync(HttpClient client, JsonNode body, TimeSpan timeout, CancellationToken stop)
    {
        using var requestDeadline = CancellationTokenSource.CreateLinkedTokenSource(stop);
        requestDeadline.CancelAfter(timeout);
        var utcDeadline = DateTimeOffset.UtcNow + timeout;
        var watch = WatchDeadlineAsync(utcDeadline, requestDeadline);
        try
        {
            var content = await Evaluation.SendAsync(client, "api/chat", body, timeout, requestDeadline.Token);
            if (DateTimeOffset.UtcNow >= utcDeadline) throw new EvaluationFailure("inferenceTimeout", "Wall-clock request deadline exceeded");
            return content;
        }
        catch (OperationCanceledException) when (!stop.IsCancellationRequested)
        { throw new EvaluationFailure("inferenceTimeout", "Request deadline exceeded; server cancellation is not instantaneous"); }
        finally { requestDeadline.Cancel(); await watch; }
    }
}
