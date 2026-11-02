using System.Net;
using System.Text.Json.Nodes;

internal static class ReasoningChecks
{
    internal static async Task<JsonObject> Run(string root, JsonArray fixtures, string prompt, JsonArray profiles)
    {
        var checks = new JsonArray();
        void Assert(bool ok) { if (!ok) throw new InvalidOperationException("Offline baseline assertion failed"); }
        void Check(string name, Action action) { action(); checks.Add(name); }
        var fingerprint = ComparisonProfile.Fingerprint(root); var metadata = ComparisonProfile.Metadata(root)["models"]!.AsArray();
        var cached = fixtures.ToDictionary(f => Evaluation.Text(f!["id"]), f => OfflineAnswer(f!).ToJsonString());
        JsonObject Approval(JsonNode c) => new()
        {
            ["approved"] = true, ["continueAfterTimeoutReviewed"] = true, ["contract"] = "condition-label-gpt-development-full-v1",
            ["candidatePreparationFingerprintSha256"] = ComparisonProfile.CandidatePreparation, ["promptSha256"] = ComparisonProfile.CandidatePromptSha256,
            ["preparationFingerprintSha256"] = ComparisonPilot.ReviewedPreparation, ["fingerprintSha256"] = fingerprint["sha256"]!.DeepClone(),
            ["profileId"] = c["profileId"]!.DeepClone(), ["model"] = c["model"]!.DeepClone(), ["think"] = c["think"]!.DeepClone(),
            ["runId"] = "offline-" + Guid.NewGuid().ToString("N"), ["route"] = c["route"]!.DeepClone(), ["maxRequests"] = 16, ["caseIds"] = c["caseIds"]!.DeepClone(),
            ["contextAndOutputLimitsReviewed"] = true, ["templateAndTokenizerReviewed"] = true,
            ["modelDigest"] = c["modelDigest"]?.DeepClone() ?? JsonValue.Create(new string('1', 64)),
            ["configurationSha256"] = Evaluation.Hash(System.Text.Encoding.UTF8.GetBytes(c.ToJsonString())),
            ["templateSha256"] = c["templateSha256"]!.DeepClone(), ["ollamaVersion"] = c["ollamaVersion"]!.DeepClone(),
            ["humanApprovalReference"] = "FAKE OFFLINE ONLY; fixed production IDs prohibit live use"
        };
        Check("fixture loading is identical for LF and CRLF documentation", () =>
        {
            var design = File.ReadAllText(Path.Combine(root, "../../docs/planning/condition-label-evaluation-design.md"));
            var temporaryRepository = Path.Combine(root, "results/offline-fixture-newlines", Guid.NewGuid().ToString("N"));
            var directory = Path.Combine(temporaryRepository, "docs/planning");
            Directory.CreateDirectory(directory);
            foreach (var newline in new[] { "\n", "\r\n" })
            {
                File.WriteAllText(Path.Combine(directory, "condition-label-evaluation-design.md"), design.ReplaceLineEndings(newline));
                Assert(JsonNode.DeepEquals(Labels.LoadDevelopment(temporaryRepository), fixtures));
            }
        });
        Check("only reviewed v5 and low sixteen-case development profile", () => Assert(profiles.Count == 1
            && profiles.Select(p => Evaluation.Text(p!["think"])).SequenceEqual(new[] { "low" })
            && System.Text.RegularExpressions.Regex.Matches(prompt, @"\S+").Count == 945
            && !prompt.Contains("Check stage.result/diseaseState.result separately from label.result.", StringComparison.Ordinal)));
        Check("exact D1-D16 only; original controls except scope and operation ceiling", () =>
        {
            Assert(fixtures.Count == 16 && fixtures.Select(f => Evaluation.Text(f!["id"])).SequenceEqual(Enumerable.Range(1,16).Select(i => $"CL-D{i:00}")));
            var original = Evaluation.Read(Path.Combine(ComparisonProfile.CandidateDirectory(root),"subset-configuration.json"));
            var config = profiles[0]!.DeepClone();
            Assert(config["operationTimeoutSeconds"]!.GetValue<int>() == 9660 && config["requestTimeoutSeconds"]!.GetValue<int>() == 600);
            foreach (var field in new[] { "profileId", "caseIds", "operationTimeoutSeconds" }) config[field] = original[field]!.DeepClone();
            Assert(JsonNode.DeepEquals(config,original));
            Assert(File.ReadAllText(Path.Combine(root,"SchemaGuidance.cs")).Contains("internal const string Direct = \"" + SchemaGuidance.Direct + "\";",StringComparison.Ordinal));
        });
        Check("request guard rejects schema-description mutation", () =>
        {
            var f = fixtures.Single(f => Evaluation.Text(f!["id"]) == "CL-D11")!;
            var body = ComparisonProfile.Request(f,profiles[0]!,prompt);
            body["format"]!["$defs"]!["finding"]!["properties"]!["result"]!["description"] = "unauthorized";
            try { SchemaGuidance.VerifyRequest(body,f,profiles[0]!,prompt); }
            catch (InvalidDataException) { return; }
            throw new InvalidOperationException("Changed description accepted");
        });
        Check("compatible preparation preserves native messages and assessment schema", () =>
        {
            var compatible = ComparisonProfile.Profiles(root, "openai-compatible")[0]!;
            var request = ComparisonProfile.Request(fixtures[0]!, compatible, prompt);
            var native = Evaluation.Read(Path.Combine(root, "baseline/requests/CL-D01.request.json"));
            Assert(JsonNode.DeepEquals(request["messages"], native["messages"])
                && JsonNode.DeepEquals(request["response_format"]?["json_schema"]?["schema"], native["format"])
                && request["stream"]!.GetValue<bool>() == false && request["n"]!.GetValue<int>() == 1
                && Evaluation.Text(request["keep_alive"]) == "5m"
                && Evaluation.Text(request["reasoning_effort"]) == "low"
                && request["max_tokens"]!.GetValue<int>() == 8192
                && request["temperature"]!.GetValue<double>() == 0.7
                && request["top_p"]!.GetValue<double>() == 0.8 && request["seed"]!.GetValue<int>() == 42
                && request["options"] is null && request["format"] is null && request["think"] is null);
        });
        Check("compatible declaration prepares without inventing an installed model identity", () =>
        {
            var c = ComparisonProfile.Profiles(root, "openai-compatible")[0]!;
            ComparisonProfile.ValidateConfig(root, c, fixtures);
            var body = ComparisonProfile.Request(fixtures[0]!, c, prompt);
            Assert(Evaluation.Text(body["model"]) == "oncomatch-gpt-oss-20b-openai-compatible:latest"
                && Evaluation.Text(c["sourceModel"]) == "gpt-oss:20b" && c["modelDigest"] is null
                && Evaluation.Text(c["modelVerification"]) == "not-verified"
                && c["modelParameters"]?["num_ctx"]?.GetValue<int>() == 24576
                && c["modelParameters"]?["top_k"]?.GetValue<int>() == 20
                && c["modelParameters"]?["min_p"]?.GetValue<int>() == 0
                && c["modelParameters"]?["repeat_penalty"]?.GetValue<int>() == 1);
        });
        Check("offline preparation records distinct native and compatible candidates", () =>
        {
            ComparisonProfile.Prepare(root, fixtures, prompt, "openai-compatible");
            var directory = ComparisonProfile.Prepare(root, fixtures, prompt, "native");
            var entries = Evaluation.Read(Path.Combine(directory, "manifest.json"))["profiles"]!.AsArray();
            var compatible = entries.Single(p => Evaluation.Text(p!["route"]) == "openai-compatible")!;
            Assert(entries.Count == 2 && compatible["requests"]!.AsArray().Count == 16
                && Evaluation.Text(compatible["endpoint"]) == "/v1/chat/completions"
                && Evaluation.Text(compatible["modelVerification"]) == "not-verified"
                && compatible["configuration"]?["modelDigest"] is null);
        });
        foreach (var p in profiles)
        {
            var c = p!; Check(Evaluation.Text(c["profileId"]) + " closed config/previews", () => { ComparisonProfile.ValidateConfig(root, c, fixtures); ComparisonPilot.ValidatePreviews(root, fixtures, c, prompt); });
            foreach (var f in fixtures) Check(Evaluation.Text(c["think"]) + "/" + Evaluation.Text(f!["id"]) + " only two schema descriptions change from v5", () =>
            {
                var body = ComparisonProfile.Request(f, c, prompt);
                var reviewed = ComparisonProfile.BaseRequest(f,c,prompt);
                SchemaGuidance.Apply(reviewed["format"]!);
                reviewed["messages"]![0]!["content"] = prompt.TrimEnd() + "\nResponse schema:\n" + body["format"]!.ToJsonString();
                Assert(JsonNode.DeepEquals(body, reviewed));
                var poisoned = f.DeepClone(); poisoned["expected"] = "NEVER-LEAK"; poisoned["expectedPairSummary"] = "NEVER-LEAK"; poisoned["reviewGuidance"] = "NEVER-LEAK";
                Assert(JsonNode.DeepEquals(body, ComparisonProfile.Request(poisoned, c, prompt)) && !body["messages"]!.ToJsonString().Contains("CL-D", StringComparison.Ordinal));
                Assert(Evaluation.Sizing(body, c)["withinConservativeBound"]!.GetValue<bool>());
            });
        }
        foreach (var field in new[] { "approved", "candidatePreparationFingerprintSha256", "promptSha256", "continueAfterTimeoutReviewed", "contract", "fingerprintSha256", "think", "modelDigest", "templateSha256", "ollamaVersion", "caseIds", "profileId", "maxRequests", "contextAndOutputLimitsReviewed", "templateAndTokenizerReviewed", "humanApprovalReference" })
            Check("missing approval rejects: " + field, () =>
            {
                var a = Approval(profiles[0]!); a.Remove(field);
                try { ComparisonPilot.ValidateApproval(a, profiles[0]!, fingerprint); } catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException) { return; }
                throw new InvalidOperationException("Invalid approval accepted");
            });
        async Task<(string File, string Directory, FakeHandler Handler)> FakeRun(JsonNode c, string mode)
        {
            var a = Approval(c); var dir = Path.Combine(root, "results/offline-approvals"); Directory.CreateDirectory(dir);
            if (mode == "smoke")
            {
                a["scope"] = "oncomatch-local-smoke-20261102"; a["caseIds"] = new JsonArray("CL-D01"); a["maxRequests"] = 1;
                a["requestTimeoutSeconds"] = 600; a["maxOutputTokens"] = 8192;
                a["requestSha256"] = Evaluation.Hash(System.Text.Encoding.UTF8.GetBytes(ComparisonProfile.Request(fixtures[0]!, c, prompt).ToJsonString()));
            }
            var file = Path.Combine(dir, Evaluation.Text(a["runId"]) + ".json"); File.WriteAllText(file, a.ToJsonString());
            var template = Evaluation.Text(metadata.Single(m => Evaluation.Text(m!["name"]) == "gpt-oss:20b")!["template"]);
            using var cancel = new CancellationTokenSource();
            var clock = mode == "operation-deadline-third" ? new ControlledClock() : null;
            var handler = new FakeHandler(c, template, cached, mode, cancel, clock);
            try { await ComparisonPilot.RunAsync(root, fixtures, prompt, c, file, handler, cancel.Token, liveSmoke: mode == "smoke", timeProvider: clock); }
            catch (InvalidDataException ex) when (mode.StartsWith("metadata-", StringComparison.Ordinal)
                && ex.Message == "Preflight stopped; no inference requests; approval consumed") { }
            return (file, Path.Combine(root, "results", Evaluation.Text(a["runId"])), handler);
        }
        foreach (var c in profiles)
        {
            var run = await FakeRun(c!, "normal"); Check(Evaluation.Text(c!["profileId"]) + " sixteen synthetic transport controls", () => Assert(run.Handler.InferenceRequests == 16
                && Evaluation.Read(Path.Combine(run.Directory, "checkpoint.json"))["completed"]!.GetValue<int>() == 16
                && !File.ReadAllText(Path.Combine(run.Directory, "attempts.jsonl")).Contains("PRIVATE-FAKE-THINKING", StringComparison.Ordinal)));
        }
        var compatibleProfile = ComparisonProfile.Profiles(root, "openai-compatible")[0]!;
        foreach (var c in new[] { profiles[0]!, compatibleProfile })
        {
            var smoke = await FakeRun(c, "smoke");
            Check(Evaluation.Text(c["route"]) + " bounded smoke sends exactly CL-D01 once", () => Assert(smoke.Handler.InferenceRequests == 1
                && Evaluation.Read(Path.Combine(smoke.Directory, "checkpoint.json"))["completed"]!.GetValue<int>() == 1));
            foreach (var field in new[] { "scope", "caseIds", "maxRequests", "requestTimeoutSeconds", "maxOutputTokens", "requestSha256", "think", "fingerprintSha256", "configurationSha256", "route" })
            {
                var altered = Evaluation.Read(smoke.File); altered.AsObject().Remove(field);
                var file = Path.Combine(root, "results/offline-approvals", "tampered-smoke.json");
                File.WriteAllText(file, altered.ToJsonString());
                var handler = new FakeHandler(c, "unused", cached, "normal");
                try { await ComparisonPilot.RunAsync(root, fixtures, prompt, c, file, handler, liveSmoke: true); throw new InvalidOperationException("Incomplete smoke approval accepted"); }
                catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException && ex.Message != "Incomplete smoke approval accepted")
                { Check(Evaluation.Text(c["route"]) + " smoke missing " + field + " rejects before HTTP", () => Assert(handler.InferenceRequests + handler.MetadataRequests == 0)); }
            }
            var replayHandler = new FakeHandler(c, "unused", cached, "normal");
            try { await ComparisonPilot.RunAsync(root, fixtures, prompt, c, smoke.File, replayHandler, liveSmoke: true); throw new InvalidOperationException("Smoke approval replayed"); }
            catch (IOException) { Check(Evaluation.Text(c["route"]) + " smoke replay rejects before HTTP", () => Assert(replayHandler.InferenceRequests + replayHandler.MetadataRequests == 0)); }
            Check(Evaluation.Text(c["route"]) + " production smoke cannot use an arbitrary fresh run identity", () =>
            {
                var altered = Evaluation.Read(smoke.File);
                try { ComparisonPilot.ValidateSmokeApproval(altered, c, fingerprint, fixtures[0]!, prompt, offline: false); }
                catch (InvalidDataException) { return; }
                throw new InvalidOperationException("Unbounded production smoke identity accepted");
            });
            Check(Evaluation.Text(c["route"]) + " consumed smoke scope rejects an otherwise complete production approval", () =>
            {
                var a = Evaluation.Read(smoke.File); a["runId"] = "smoke-20261102-" + Evaluation.Text(c["route"]) + "-1";
                try { ComparisonPilot.ValidateSmokeApproval(a, c, fingerprint, fixtures[0]!, prompt, offline: false); }
                catch (InvalidDataException) { return; }
                throw new InvalidOperationException("Consumed live smoke scope still enabled");
            });
        }
        var remoteSource = await FakeRun(compatibleProfile, "metadata-source-remote");
        Check("compatible verification rejects a remote source before inference", () => Assert(
            remoteSource.Handler.InferenceRequests == 0 && File.Exists(Path.Combine(remoteSource.Directory, "stopped.json"))));
        foreach (var mode in new[] { "metadata-version", "metadata-digest", "metadata-source-digest", "metadata-parameters", "metadata-missing-parameters", "metadata-duplicate-parameters", "metadata-private" })
        {
            var rejected = await FakeRun(compatibleProfile, mode);
            Check(mode + " stops before inference without leaking provider diagnostics", () => Assert(
                rejected.Handler.InferenceRequests == 0 && File.Exists(Path.Combine(rejected.Directory, "stopped.json"))
                && !File.ReadAllText(Path.Combine(rejected.Directory, "stopped.json")).Contains("PRIVATE-FAKE-THINKING", StringComparison.Ordinal)));
        }
        var compatibleRun = await FakeRun(compatibleProfile, "normal");
        Check("compatible route records sixteen validated results without native inference or private reasoning", () =>
        {
            var checkpoint = Evaluation.Read(Path.Combine(compatibleRun.Directory, "checkpoint.json"));
            var ledger = File.ReadAllText(Path.Combine(compatibleRun.Directory, "attempts.jsonl"));
            Assert(compatibleRun.Handler.InferenceRequests == 16 && checkpoint["completed"]!.GetValue<int>() == 16
                && Evaluation.Text(checkpoint["route"]) == "openai-compatible"
                && !ledger.Contains("PRIVATE-FAKE-THINKING", StringComparison.Ordinal));
        });
        var equivalentNative = await FakeRun(profiles[0]!, "normal");
        Check("identical fake assessment content produces the same validation on both routes", () =>
        {
            var nativeRecords = File.ReadLines(Path.Combine(equivalentNative.Directory, "attempts.jsonl")).Select(Evaluation.Parse)
                .Where(r => Evaluation.Text(r["event"]) == "finished").ToArray();
            var compatibleRecords = File.ReadLines(Path.Combine(compatibleRun.Directory, "attempts.jsonl")).Select(Evaluation.Parse)
                .Where(r => Evaluation.Text(r["event"]) == "finished").ToArray();
            Assert(nativeRecords.Length == 16 && compatibleRecords.Length == 16);
            for (var i = 0; i < 16; i++) Assert(JsonNode.DeepEquals(nativeRecords[i]["validation"], compatibleRecords[i]["validation"]));
            Assert(compatibleRecords[0]["metrics"]?["loadTimingAvailable"]?.GetValue<bool>() == false
                && compatibleRecords[0]["metrics"]?["usage"]?["prompt_tokens"]?.GetValue<int>() == 100);
        });
        var refusalRun = await FakeRun(compatibleProfile, "refusal-first");
        Check("compatible refusal is a failed attempt even when content looks valid", () => Assert(
            Evaluation.Read(Path.Combine(refusalRun.Directory, "checkpoint.json"))["failed"]!.GetValue<int>() == 1
            && Evaluation.Read(Path.Combine(refusalRun.Directory, "checkpoint.json"))["completed"]!.GetValue<int>() == 15));
        foreach (var mode in new[] { "private-usage", "private-duplicate" })
        {
            var privacyRun = await FakeRun(compatibleProfile, mode);
            Check(mode + " never persists provider text outside final content", () => Assert(
                !File.ReadAllText(Path.Combine(privacyRun.Directory, "attempts.jsonl")).Contains("PRIVATE-FAKE-THINKING", StringComparison.Ordinal)));
        }
        foreach (var c in new[] { profiles[0]!, compatibleProfile })
        {
            var cancelled = await FakeRun(c, "cancel-third");
            Check(Evaluation.Text(c["route"]) + " cancellation retains two completed pairs and interrupts the third", () =>
            {
                var checkpoint = Evaluation.Read(Path.Combine(cancelled.Directory, "checkpoint.json"));
                Assert(checkpoint["completed"]!.GetValue<int>() == 2 && checkpoint["interrupted"]!.GetValue<int>() == 1
                    && cancelled.Handler.InferenceRequests == 3);
            });
        }
        foreach (var c in new[] { profiles[0]!, compatibleProfile })
        {
            var expired = await FakeRun(c, "operation-deadline-third");
            Check(Evaluation.Text(c["route"]) + " operation deadline preserves completed results and interrupts active work", () =>
            {
                var checkpoint = Evaluation.Read(Path.Combine(expired.Directory, "checkpoint.json"));
                var finished = File.ReadLines(Path.Combine(expired.Directory, "attempts.jsonl")).Select(Evaluation.Parse)
                    .Where(r => Evaluation.Text(r["event"]) == "finished").ToArray();
                Assert(expired.Handler.InferenceRequests == 3 && checkpoint["completed"]!.GetValue<int>() == 2
                    && checkpoint["interrupted"]!.GetValue<int>() == 1 && checkpoint["failed"]!.GetValue<int>() == 0
                    && checkpoint["aggregateDeadlineReached"]!.GetValue<bool>() && finished.Length == 3
                    && finished[0]["validation"] is not null && finished[1]["validation"] is not null
                    && Evaluation.Text(finished[2]["state"]) == "interrupted" && finished[2]["validation"] is null);
            });
        }
        foreach (var mode in new[] { "timeout", "invalid-first", "inline-first", "length-first", "identity", "transport" })
        {
            foreach (var c in new[] { profiles[0]!, compatibleProfile })
            {
                var run = await FakeRun(c, mode); Check(Evaluation.Text(c["route"]) + "/" + mode + " recorder policy/privacy", () => Assert(run.Handler.InferenceRequests == (mode is "identity" or "transport" ? 1 : 16)
                    && Evaluation.Read(Path.Combine(run.Directory, "checkpoint.json"))["failed"]!.GetValue<int>() == (mode == "timeout" ? 16 : 1)
                    && !File.ReadAllText(Path.Combine(run.Directory, "attempts.jsonl")).Contains("PRIVATE-FAKE-THINKING", StringComparison.Ordinal)));
            }
        }
        foreach (var mode in new[] { "missing-choices", "multiple-choices", "null-content", "tool-first", "wrong-role", "malformed-first", "invalid-evidence-first", "missing-label-first" })
        {
            var rejected = await FakeRun(compatibleProfile, mode);
            Check("compatible/" + mode + " rejects the whole answer and continues remaining cases", () =>
            {
                var checkpoint = Evaluation.Read(Path.Combine(rejected.Directory, "checkpoint.json"));
                Assert(rejected.Handler.InferenceRequests == 16 && checkpoint["failed"]!.GetValue<int>() == 1
                    && checkpoint["completed"]!.GetValue<int>() == 15);
            });
        }
        foreach (var original in new[] { profiles[0]!, compatibleProfile })
        {
            foreach (var field in new[] { "route", "contextTokens", "think", "modelDigest", "caseIds" }.Concat(
                Evaluation.Text(original["route"]) == "openai-compatible" ? new[] { "modelParameters", "sourceModelDigest", "modelVerification" } : Array.Empty<string>()))
            {
                Check(Evaluation.Text(original["route"]) + " rejects missing required control: " + field, () =>
                {
                    var altered = original.DeepClone(); altered.AsObject().Remove(field);
                    try { ComparisonProfile.ValidateConfig(root, altered, fixtures); }
                    catch (InvalidDataException) { return; }
                    throw new InvalidOperationException("Unresolved required control accepted");
                });
            }
        }
        Check("compatible approval cannot change route or omit the exact configuration binding", () =>
        {
            foreach (var field in new[] { "route", "configurationSha256" })
            {
                var a = Approval(compatibleProfile); a[field] = "mismatch";
                try { ComparisonPilot.ValidateApproval(a, compatibleProfile, fingerprint); }
                catch (InvalidDataException) { continue; }
                throw new InvalidOperationException("Mismatched candidate approval accepted");
            }
        });
        var preparationDirectory = Path.Combine(root, "results/preparation", Evaluation.Text(fingerprint["sha256"]));
        var manifestPath = Path.Combine(preparationDirectory, "manifest.json");
        var savedManifest = File.ReadAllText(manifestPath);
        try
        {
            var incomplete = Evaluation.Parse(savedManifest);
            var candidate = incomplete["profiles"]!.AsArray().Single(p => Evaluation.Text(p!["route"]) == "openai-compatible")!;
            candidate["requests"]!.AsArray().RemoveAt(0);
            File.WriteAllText(manifestPath, incomplete.ToJsonString());
            try { await FakeRun(compatibleProfile, "normal"); throw new InvalidOperationException("Incomplete preparation accepted"); }
            catch (InvalidDataException) { Check("incomplete prepared coverage rejects before execution", () => Assert(true)); }
        }
        finally { File.WriteAllText(manifestPath, savedManifest); }
        foreach (var c in new[] { profiles[0]!, compatibleProfile })
        {
            var a = Approval(c); a["runId"] = Evaluation.Text(c["profileId"]) + "-1";
            var file = Path.Combine(root, "results/offline-approvals", Evaluation.Text(a["runId"]) + ".json");
            File.WriteAllText(file, a.ToJsonString());
            try { await ComparisonPilot.RunAsync(root, fixtures, prompt, c, file); throw new InvalidOperationException("Live execution enabled"); }
            catch (InvalidDataException ex) when (ex.Message.StartsWith("Inference deferred;", StringComparison.Ordinal))
            { Check(Evaluation.Text(c["route"]) + " production guard rejects a syntactically complete approval", () => Assert(true)); }
        }
        var replay = await FakeRun(profiles[0]!, "normal"); var h = new FakeHandler(profiles[0]!, "unused", cached, "normal");
        try { await ComparisonPilot.RunAsync(root, fixtures, prompt, profiles[0]!, replay.File, h); throw new InvalidOperationException("Claim replayed"); }
        catch (IOException) { Check("replay blocked before transport", () => Assert(h.InferenceRequests + h.MetadataRequests == 0)); }
        try { await ComparisonPilot.RunAsync(root, fixtures, prompt, profiles[0]!, replay.File); throw new InvalidOperationException("Fake ID reached live client"); }
        catch (InvalidDataException ex) when (ex.Message == "Use the fixed single-use profile run ID") { Check("fake ID cannot reach live transport", () => Assert(true)); }
        Check("consumed production profile cannot run", () =>
        {
            var a = Approval(profiles[0]!); a["runId"] = "development-full-low-gpt-oss-20b-1";
            try { ComparisonPilot.ValidateProductionRun(root, profiles[0]!, a); }
            catch (InvalidDataException ex) when (ex.Message.StartsWith("Inference deferred;", StringComparison.Ordinal)) { return; }
            throw new InvalidOperationException("Consumed production profile accepted");
        });
        return new JsonObject { ["passed"] = checks.Count, ["checks"] = checks, ["inferenceRequests"] = 0,
            ["routes"] = new JsonArray("native", "openai-compatible"), ["scope"] = "Offline baseline parity, route preparation, approval and fake-transport checks; no model quality or live compatibility claim" };
    }
    // Mechanical transport controls only; synthetic expectations never enter real requests.
    private static JsonObject OfflineAnswer(JsonNode fixture)
    {
        var catalog = Labels.Catalog(fixture); var labels = new JsonArray();
        foreach (var expected in fixture["expected"]!.AsArray())
        {
            var id = Evaluation.Text(expected!["labelId"]);
            var label = new JsonObject { ["labelId"] = id, ["result"] = expected["result"]!.DeepClone(),
                ["requirementSummary"] = "Synthetic offline control, not model output", ["unassessedQualifiers"] = new JsonArray(), ["explanation"] = "Synthetic offline control, not semantic evidence" };
            foreach (var name in Labels.Findings)
            {
                var absent = Evaluation.Text(expected[name]) == "notSpecified";
                var patient = new JsonArray();
                if (!absent) foreach (var source in catalog.Where(p => p.Key == "patient."+name || p.Key.StartsWith("patient."+name+".",StringComparison.Ordinal)))
                    patient.Add(new JsonObject { ["sourceId"] = source.Key, ["value"] = source.Value?.DeepClone() });
                label[name] = new JsonObject { ["result"] = expected[name]!.DeepClone(), ["patientEvidence"] = patient,
                    ["studyEvidence"] = absent ? new JsonArray() : new JsonArray(new JsonObject { ["sourceId"] = id, ["quote"] = catalog[id]!.DeepClone() }),
                    ["examinedSources"] = new JsonArray(id), ["explanation"] = "Synthetic offline control, not model output" };
            }
            labels.Add(label);
        }
        return new JsonObject { ["labels"] = labels };
    }
    private sealed class ControlledClock : TimeProvider
    {
        private readonly List<ControlledTimer> timers = new();
        private DateTimeOffset now = new(2026, 11, 2, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() { lock (timers) return now; }
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ControlledTimer(this, callback, state);
            lock (timers) { timers.Add(timer); timer.Change(dueTime, period); }
            return timer;
        }
        internal void Advance(TimeSpan elapsed)
        {
            ControlledTimer[] pending;
            lock (timers) { now += elapsed; pending = timers.ToArray(); }
            foreach (var timer in pending) timer.Fire();
        }
        private sealed class ControlledTimer(ControlledClock clock, TimerCallback callback, object? state) : ITimer
        {
            private DateTimeOffset? due;
            private TimeSpan period;
            private bool disposed;
            public bool Change(TimeSpan dueTime, TimeSpan repeat)
            {
                lock (clock.timers)
                {
                    if (disposed) return false;
                    due = dueTime == Timeout.InfiniteTimeSpan ? null : clock.now + dueTime;
                    period = repeat;
                    return true;
                }
            }
            internal void Fire()
            {
                lock (clock.timers)
                {
                    if (disposed || due is null || due > clock.now) return;
                    due = period > TimeSpan.Zero ? clock.now + period : null;
                }
                callback(state);
            }
            public void Dispose() { lock (clock.timers) { disposed = true; clock.timers.Remove(this); } }
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
    private sealed class FakeHandler(JsonNode config, string template, Dictionary<string, string> contents, string mode, CancellationTokenSource? cancel = null, ControlledClock? clock = null) : HttpMessageHandler
    {
        internal int InferenceRequests; internal int MetadataRequests;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            JsonNode response; var endpoint = request.RequestUri!.AbsolutePath;
            if (endpoint == (Evaluation.Text(config["route"]) == "native" ? "/api/chat" : "/v1/chat/completions"))
            {
                var index = InferenceRequests++;
                if (mode == "cancel-third" && index == 2) { cancel!.Cancel(); cancellationToken.ThrowIfCancellationRequested(); }
                if (mode == "operation-deadline-third" && index == 2)
                { clock!.Advance(TimeSpan.FromSeconds(9660)); cancellationToken.ThrowIfCancellationRequested(); }
                if (mode == "timeout") throw new EvaluationFailure("inferenceTimeout", "Injected offline timeout");
                if (mode == "transport") throw new HttpRequestException("Injected offline transport failure");
                var content = mode == "invalid-first" && index == 0 ? "{bad" : mode == "inline-first" && index == 0 ? "<|channel|>analysis PRIVATE-FAKE-THINKING" : contents[ComparisonProfile.CaseIds[index]];
                if (mode == "invalid-evidence-first" && index == 0)
                {
                    var invalid = Evaluation.Parse(content);
                    invalid["labels"]![0]!["cancerType"]!["patientEvidence"]![0]!["value"] = "fabricated";
                    content = invalid.ToJsonString();
                }
                if (mode == "missing-label-first" && index == 0) content = "{\"labels\":[]}";
                response = new JsonObject { ["model"] = mode == "identity" ? "wrong" : config["model"]!.DeepClone(), ["done"] = true,
                    ["done_reason"] = mode == "length-first" && index == 0 ? "length" : "stop",
                    ["message"] = new JsonObject { ["content"] = content, ["thinking"] = "PRIVATE-FAKE-THINKING" } };
                if (Evaluation.Text(config["route"]) == "openai-compatible")
                    response = new JsonObject { ["model"] = response["model"]!.DeepClone(),
                        ["choices"] = new JsonArray(new JsonObject { ["index"] = 0, ["finish_reason"] = mode == "length-first" && index == 0 ? "length" : "stop",
                            ["message"] = new JsonObject { ["role"] = "assistant", ["content"] = content, ["reasoning"] = "PRIVATE-FAKE-THINKING" } }),
                        ["usage"] = new JsonObject { ["prompt_tokens"] = 100, ["completion_tokens"] = 20, ["total_tokens"] = 120 } };
                if (mode == "refusal-first" && index == 0) response["choices"]![0]!["message"]!["refusal"] = "PRIVATE-FAKE-THINKING";
                if (mode == "private-usage") response["usage"]!["unexpected"] = "PRIVATE-FAKE-THINKING";
                if (mode == "private-duplicate" && index == 0)
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"PRIVATE-FAKE-THINKING\":1,\"PRIVATE-FAKE-THINKING\":2}") });
                if (index == 0)
                {
                    switch (mode)
                    {
                        case "missing-choices": response.AsObject().Remove("choices"); break;
                        case "multiple-choices": response["choices"]!.AsArray().Add(response["choices"]![0]!.DeepClone()); break;
                        case "null-content": response["choices"]![0]!["message"]!["content"] = null; break;
                        case "tool-first": response["choices"]![0]!["message"]!["tool_calls"] = new JsonArray(new JsonObject()); break;
                        case "wrong-role": response["choices"]![0]!["message"]!["role"] = "user"; break;
                        case "malformed-first": return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{broken") });
                    }
                }
            }
            else if (endpoint == "/api/ps") response = new JsonObject { ["models"] = new JsonArray() };
            else
            {
                MetadataRequests++;
                response = endpoint switch
                {
                    "/api/version" => new JsonObject { ["version"] = config["ollamaVersion"]!.DeepClone() },
                    "/api/tags" => new JsonObject { ["models"] = new JsonArray(new JsonObject { ["name"] = config["model"]!.DeepClone(),
                        ["digest"] = config["modelDigest"]?.DeepClone() ?? JsonValue.Create(new string('1', 64)), ["details"] = new JsonObject { ["runner"] = config["runner"]!.DeepClone() } },
                        new JsonObject { ["name"] = config["sourceModel"]?.DeepClone() ?? JsonValue.Create("unused-native-source"), ["digest"] = config["sourceModelDigest"]?.DeepClone() ?? JsonValue.Create("unused-native-source") }) },
                    "/api/show" => new JsonObject { ["template"] = template, ["parameters"] = "num_ctx 24576\ntop_k 20\nmin_p 0\nrepeat_penalty 1",
                        ["thinking"] = config["thinkingMetadata"]?.DeepClone(), ["template_source"] = config["templateSource"]!.DeepClone(), ["renderer"] = config["renderer"]?.DeepClone(), ["parser"] = config["parser"]?.DeepClone(), ["model_info"] = new JsonObject { ["tokenizer.ggml.model"] = "gpt2", ["tokenizer.ggml.pre"] = config["preTokenizer"]!.DeepClone() } },
                    _ => throw new InvalidOperationException("Unexpected fake endpoint")
                };
            }
            if (endpoint == "/api/tags" && mode == "metadata-source-remote") response["models"]![1]!["remote_host"] = "https://remote.invalid";
            if (endpoint == "/api/version" && mode == "metadata-version") response["version"] = "wrong";
            if (endpoint == "/api/tags" && mode == "metadata-digest") response["models"]![0]!["digest"] = new string('2', 64);
            if (endpoint == "/api/tags" && mode == "metadata-source-digest") response["models"]![1]!["digest"] = new string('2', 64);
            if (endpoint == "/api/show" && mode == "metadata-parameters") response["parameters"] = "num_ctx 32000\ntop_k 20\nmin_p 0\nrepeat_penalty 1";
            if (endpoint == "/api/show" && mode == "metadata-missing-parameters") response["parameters"] = "top_k 20\nmin_p 0\nrepeat_penalty 1";
            if (endpoint == "/api/show" && mode == "metadata-duplicate-parameters") response["parameters"] = "num_ctx 24576\nnum_ctx 32000\ntop_k 20\nmin_p 0\nrepeat_penalty 1";
            if (endpoint == "/api/version" && mode == "metadata-private")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"PRIVATE-FAKE-THINKING\":1,\"PRIVATE-FAKE-THINKING\":2}") });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response.ToJsonString()) });
        }
    }
}
