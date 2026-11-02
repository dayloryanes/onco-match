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
            ["runId"] = "offline-" + Guid.NewGuid().ToString("N"), ["route"] = "native", ["maxRequests"] = 16, ["caseIds"] = c["caseIds"]!.DeepClone(),
            ["contextAndOutputLimitsReviewed"] = true, ["templateAndTokenizerReviewed"] = true,
            ["modelDigest"] = c["modelDigest"]!.DeepClone(), ["templateSha256"] = c["templateSha256"]!.DeepClone(), ["ollamaVersion"] = c["ollamaVersion"]!.DeepClone(),
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
            var file = Path.Combine(dir, Evaluation.Text(a["runId"]) + ".json"); File.WriteAllText(file, a.ToJsonString());
            var template = Evaluation.Text(metadata.Single(m => Evaluation.Text(m!["name"]) == "gpt-oss:20b")!["template"]);
            var handler = new FakeHandler(c, template, cached, mode);
            await ComparisonPilot.RunAsync(root, fixtures, prompt, c, file, handler);
            return (file, Path.Combine(root, "results", Evaluation.Text(a["runId"])), handler);
        }
        foreach (var c in profiles)
        {
            var run = await FakeRun(c!, "normal"); Check(Evaluation.Text(c!["profileId"]) + " sixteen synthetic transport controls", () => Assert(run.Handler.InferenceRequests == 16
                && Evaluation.Read(Path.Combine(run.Directory, "checkpoint.json"))["completed"]!.GetValue<int>() == 16
                && !File.ReadAllText(Path.Combine(run.Directory, "attempts.jsonl")).Contains("PRIVATE-FAKE-THINKING", StringComparison.Ordinal)));
        }
        foreach (var mode in new[] { "timeout", "invalid-first", "inline-first", "length-first", "identity", "transport" })
        {
            var run = await FakeRun(profiles[0]!, mode); Check(mode + " recorder policy/privacy", () => Assert(run.Handler.InferenceRequests == (mode is "identity" or "transport" ? 1 : 16)
                && Evaluation.Read(Path.Combine(run.Directory, "checkpoint.json"))["failed"]!.GetValue<int>() == (mode == "timeout" ? 16 : 1)
                && !File.ReadAllText(Path.Combine(run.Directory, "attempts.jsonl")).Contains("PRIVATE-FAKE-THINKING", StringComparison.Ordinal)));
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
        return new JsonObject { ["passed"] = checks.Count, ["checks"] = checks, ["inferenceRequests"] = 0, ["scope"] = "Offline baseline parity, approval, order and fake-transport checks; no model quality claim" };
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
    private sealed class FakeHandler(JsonNode config, string template, Dictionary<string, string> contents, string mode) : HttpMessageHandler
    {
        internal int InferenceRequests; internal int MetadataRequests;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            JsonNode response; var endpoint = request.RequestUri!.AbsolutePath;
            if (endpoint == "/api/chat")
            {
                var index = InferenceRequests++;
                if (mode == "timeout") throw new EvaluationFailure("inferenceTimeout", "Injected offline timeout");
                if (mode == "transport") throw new HttpRequestException("Injected offline transport failure");
                var content = mode == "invalid-first" && index == 0 ? "{bad" : mode == "inline-first" && index == 0 ? "<|channel|>analysis PRIVATE-FAKE-THINKING" : contents[ComparisonProfile.CaseIds[index]];
                response = new JsonObject { ["model"] = mode == "identity" ? "wrong" : config["model"]!.DeepClone(), ["done"] = true,
                    ["done_reason"] = mode == "length-first" && index == 0 ? "length" : "stop",
                    ["message"] = new JsonObject { ["content"] = content, ["thinking"] = "PRIVATE-FAKE-THINKING" } };
            }
            else if (endpoint == "/api/ps") response = new JsonObject { ["models"] = new JsonArray() };
            else
            {
                MetadataRequests++;
                response = endpoint switch
                {
                    "/api/version" => new JsonObject { ["version"] = config["ollamaVersion"]!.DeepClone() },
                    "/api/tags" => new JsonObject { ["models"] = new JsonArray(new JsonObject { ["name"] = config["model"]!.DeepClone(), ["digest"] = config["modelDigest"]!.DeepClone(), ["details"] = new JsonObject { ["runner"] = config["runner"]!.DeepClone() } }) },
                    "/api/show" => new JsonObject { ["template"] = template, ["thinking"] = config["thinkingMetadata"]?.DeepClone(), ["template_source"] = config["templateSource"]!.DeepClone(), ["renderer"] = config["renderer"]?.DeepClone(), ["parser"] = config["parser"]?.DeepClone(), ["model_info"] = new JsonObject { ["tokenizer.ggml.model"] = "gpt2", ["tokenizer.ggml.pre"] = config["preTokenizer"]!.DeepClone() } },
                    _ => throw new InvalidOperationException("Unexpected fake endpoint")
                };
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response.ToJsonString()) });
        }
    }
}
