using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

internal static class OfflineChecks
{
    internal static async Task RunAsync(string root, JsonNode config, JsonArray fixtures, string prompt, JsonNode schema)
    {
        var count = 0;
        void Check(string name, Action test)
        {
            try { test(); }
            catch (Exception ex) { throw new InvalidOperationException($"Offline check failed: {name}: {ex.Message}", ex); }
            count++; Console.WriteLine($"PASS {name}");
        }
        void Assert(bool value) { if (!value) throw new InvalidOperationException("Offline assertion failed"); }
        void Reject(string name, Action test, string? code = null) => Check(name, () =>
        {
            try { test(); }
            catch (EvaluationFailure ex) when (code is null || ex.Code == code) { return; }
            catch (Exception ex) when (code is null && ex is JsonException or InvalidDataException) { return; }
            throw new InvalidOperationException($"Expected rejection: {name}");
        });
        var fixture = fixtures[0]!;
        var answer = SampleAnswer(fixture);
        Check("request grounds the model with the same schema used for constrained output", () =>
        {
            foreach (var route in new[] { "native", "compatible" })
            {
                var request = Evaluation.Request(fixture, config, prompt, schema, route);
                var sentSchema = route == "native" ? request["format"]! : request["response_format"]!["json_schema"]!["schema"]!;
                var system = Evaluation.Text(request["messages"]![0]!["content"]);
                Assert(system.EndsWith(sentSchema.ToJsonString(), StringComparison.Ordinal));
            }
        });
        Check("request evidence IDs are restricted to present sources of the correct category", () =>
        {
            foreach (var item in fixtures)
            {
                var catalog = Evaluation.Catalog(item!);
                var sent = Evaluation.Request(item!, config, prompt, schema, "native")["format"]!;
                foreach (var (definition, prefix) in new[] { ("valueEvidence", "patient."), ("textEvidence", "study.") })
                {
                    var ids = sent["$defs"]![definition]!["properties"]!["sourceId"]!["enum"]!.AsArray().Select(Evaluation.Text).ToArray();
                    var allowed = catalog.Where(p => p.Key.StartsWith(prefix, StringComparison.Ordinal)
                        && p.Value!["present"]!.GetValue<bool>()
                        && (prefix == "patient." || p.Value["value"] is JsonValue value && value.TryGetValue<string>(out var text) && text.Length > 0))
                        .Select(p => p.Key);
                    Assert(ids.SequenceEqual(allowed));
                }
                var examined = sent["$defs"]!["criterion"]!["properties"]!["examinedSources"]!["items"]!["enum"]!;
                Assert(examined.AsArray().Select(Evaluation.Text).SequenceEqual(catalog.Where(p => p.Key.StartsWith("study.", StringComparison.Ordinal)).Select(p => p.Key)));
                Assert(JsonNode.DeepEquals(examined, sent["$defs"]!["cancerCriterion"]!["properties"]!["examinedSources"]!["items"]!["enum"]));
            }
        });
        Check("request schema specialization does not mutate the reusable schema", () =>
        {
            var before = schema.ToJsonString();
            Evaluation.Request(fixtures[2]!, config, prompt, schema, "native");
            Evaluation.Request(fixtures[5]!, config, prompt, schema, "compatible");
            Assert(schema.ToJsonString() == before);
        });
        Check("request construction ignores answer keys and review guidance", () =>
        {
            var changed = fixture.DeepClone();
            changed["expected"] = new JsonObject { ["stage"] = "LEAK-MARKER" };
            changed["reviewGuidance"] = "LEAK-MARKER";
            Assert(JsonNode.DeepEquals(Evaluation.Request(fixture, config, prompt, schema, "native"),
                Evaluation.Request(changed, config, prompt, schema, "native")));
        });
        Check("cancer schema uses a standalone object with one narrowed result definition", () =>
        {
            var cancer = schema["$defs"]!["cancerCriterion"]!;
            var criterion = schema["$defs"]!["criterion"]!;
            Assert(cancer["allOf"] is null && Evaluation.Text(cancer["type"]) == "object");
            Assert(cancer["additionalProperties"]!.GetValue<bool>() == false);
            Assert(JsonNode.DeepEquals(cancer["required"], criterion["required"]));
            Assert(cancer["properties"]!.AsObject().Count == 7);
            Assert(cancer["properties"]!["result"]!["enum"]!.AsArray().Select(Evaluation.Text)
                .SequenceEqual(new[] { "satisfied", "excluded", "undetermined" }));
            foreach (var property in criterion["properties"]!.AsObject().Where(p => p.Key != "result"))
                Assert(JsonNode.DeepEquals(cancer["properties"]![property.Key], property.Value));
        });

        // Exercise MSBuild's actual recursive Compile glob with a new ignored source.
        // This also works before the first live run has produced any input snapshots.
        var sentinelDirectory = Path.Combine(root, "results", "build-exclusion-check");
        Directory.CreateDirectory(sentinelDirectory);
        var sentinel = Path.Combine(sentinelDirectory, $"MustNotCompile-{Guid.NewGuid():N}.cs");
        await File.WriteAllTextAsync(sentinel, "#error Evaluation results must never be compiled\n");
        try
        {
            var start = new System.Diagnostics.ProcessStartInfo("dotnet")
            { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            foreach (var argument in new[] { "msbuild", Path.Combine(root, "InferenceEvaluation.csproj"), "-getItem:Compile" })
                start.ArgumentList.Add(argument);
            using var process = System.Diagnostics.Process.Start(start) ?? throw new InvalidOperationException("Cannot inspect Compile inputs");
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var stdout = process.StandardOutput.ReadToEndAsync(deadline.Token);
            var stderr = process.StandardError.ReadToEndAsync(deadline.Token);
            try { await process.WaitForExitAsync(deadline.Token); }
            catch (OperationCanceledException) { process.Kill(entireProcessTree: true); throw; }
            var compile = Evaluation.Parse(await stdout);
            var errors = await stderr;
            Check("MSBuild excludes saved and diagnostic result sources from compilation", () =>
            {
                Assert(process.ExitCode == 0 && errors.Length == 0);
                var names = compile["Items"]!["Compile"]!.AsArray().Select(item => Evaluation.Text(item!["Identity"]).Replace('\\', '/')).ToArray();
                Assert(names.Order().SequenceEqual(new[] { "Evaluation.cs", "OfflineChecks.cs", "Program.cs" }.Order()));
                Assert(!names.Any(name => name.StartsWith("results/", StringComparison.OrdinalIgnoreCase)));
            });
        }
        finally { File.Delete(sentinel); }
        var design = File.ReadAllText(Path.Combine(root, "../../docs/planning/inference-evaluation-design.md"));
        Check("six inputs and expected outcomes match approved design", () =>
        {
            var letters = new Dictionary<string, string> { ["S"] = "satisfied", ["E"] = "excluded", ["U"] = "undetermined", ["N"] = "noRestrictionIdentified" };
            foreach (var item in fixtures)
            {
                var id = Evaluation.Text(item!["id"]);
                var section = Regex.Match(design, $@"(?ms)^### {id}\r?\n(.*?)(?=^### |^## |\z)").Groups[1].Value;
                Assert(section.Length > 0);
                var quoted = Regex.Match(section, @"(?m)^> (.+)").Groups[1].Value.TrimEnd('\r');
                Assert(quoted.Length == 0 ? item["study"]!["eligibilityCriteria"] is null : quoted == Evaluation.Text(item["study"]!["eligibilityCriteria"]));
                var expected = Regex.Match(section, @"\*\*([SEUN]) / ([SEUN]) / ([SEUN])\*\*");
                for (var i = 0; i < 3; i++) Assert(Evaluation.Text(item["expected"]![Evaluation.Criteria[i]]) == letters[expected.Groups[i + 1].Value]);
                var outcome = Evaluation.Text(item["expected"]!["outcome"]);
                Assert(section.Contains(outcome switch { "potentialMatch" => "**Potential match**", "nonmatch" => "**Nonmatch**", _ => "**Insufficient information**" }, StringComparison.Ordinal));
                Assert(Evaluation.Text(item["patient"]!["cancerType"]) == (id == "D9" ? "Fictional cancer B" : "Fictional cancer A"));
                Assert(Evaluation.Text(item["patient"]!["diseaseState"]!["value"]) == (id == "D3" ? "newlyDiagnosed" : "refractory"));
                var expectedPatient = Evaluation.Parse("""
                    {"id":"placeholder","ageYears":52,"sex":"male","cancerType":"Fictional cancer A","stage":{"status":"known","value":"I","classificationSystem":"Development staging system"},"diseaseState":{"status":"known","value":"refractory"}}
                    """);
                expectedPatient["id"] = $"evaluation-{id}";
                if (id == "D9") expectedPatient["cancerType"] = "Fictional cancer B";
                if (id == "D3") expectedPatient["diseaseState"]!["value"] = "newlyDiagnosed";
                if (id == "D13") expectedPatient["stage"] = Evaluation.Parse("""{"status":"unknown","value":null,"classificationSystem":null}""");
                Assert(JsonNode.DeepEquals(expectedPatient, item["patient"]));
                foreach (var field in new[] { "officialTitle", "briefSummary", "detailedDescription", "studyPopulation" }) Assert(item["study"]![field] is null);
                Assert(Evaluation.Text(item["study"]!["briefTitle"]) == "Fictional cancer A development study");
                Assert(JsonNode.DeepEquals(item["study"]!["conditions"], new JsonArray("Fictional cancer A")));
            }
        });
        Check("D13 unknown stage preserves null value/classification", () =>
        {
            var stage = fixtures[5]!["patient"]!["stage"]!;
            Assert(Evaluation.Text(stage["status"]) == "unknown" && stage["value"] is null && stage["classificationSystem"] is null);
        });
        Check("missing study source differs from present patient null", () =>
        {
            Assert(Evaluation.Catalog(fixtures[2]!)["study.eligibilityCriteria"]!["present"]!.GetValue<bool>() == false);
            Assert(Evaluation.Catalog(fixtures[5]!)["patient.stage.value"]!["present"]!.GetValue<bool>());
        });
        Check("valid answer is mechanically valid but semantic review remains pending", () =>
        {
            var validated = Evaluation.ValidateAnswer(answer.ToJsonString(), fixture);
            Assert(Evaluation.Text(validated["semanticReview"]) == "pending" && validated["overallPass"] is null);
        });
        Reject("malformed JSON", () => Evaluation.ValidateAnswer("{", fixture), "invalidResponse");
        Reject("duplicate criterion including escaped duplicate key", () => Evaluation.Parse("{\"stage\":1,\"st\\u0061ge\":2}"));
        var missing = answer.DeepClone(); missing.AsObject().Remove("stage");
        Reject("missing criterion", () => Evaluation.ValidateAnswer(missing.ToJsonString(), fixture), "invalidResponse");
        var extra = answer.DeepClone(); extra["outcome"] = "potentialMatch";
        Reject("model-owned overall outcome", () => Evaluation.ValidateAnswer(extra.ToJsonString(), fixture), "invalidResponse");
        var badResult = answer.DeepClone(); badResult["stage"]!["result"] = "probably";
        Reject("invalid criterion result", () => Evaluation.ValidateAnswer(badResult.ToJsonString(), fixture), "invalidResponse");
        var altered = answer.DeepClone(); altered["stage"]!["studyEvidence"]![0]!["quote"] = "invented quotation";
        Reject("altered quotation invalidates whole answer", () => Evaluation.ValidateAnswer(altered.ToJsonString(), fixture), "invalidEvidence");
        var misattributed = answer.DeepClone(); misattributed["stage"]!["studyEvidence"]![0]!["sourceId"] = "study.briefTitle";
        Reject("authentic eligibility quotation cannot be attributed to the title", () => Evaluation.ValidateAnswer(misattributed.ToJsonString(), fixture), "invalidEvidence");
        var examinedPatient = answer.DeepClone(); examinedPatient["stage"]!["examinedSources"] = new JsonArray("patient.stage.value");
        Reject("examined sources cannot contain patient IDs", () => Evaluation.ValidateAnswer(examinedPatient.ToJsonString(), fixture), "invalidEvidence");
        var unsupported = answer.DeepClone(); unsupported["cancerType"]!["patientEvidence"] = new JsonArray();
        Reject("positive cancer alignment still requires patient evidence", () => Evaluation.ValidateAnswer(unsupported.ToJsonString(), fixture), "invalidEvidence");
        var fakeValue = answer.DeepClone(); fakeValue["stage"]!["patientEvidence"]![0]!["value"] = "III";
        Reject("invented patient value", () => Evaluation.ValidateAnswer(fakeValue.ToJsonString(), fixture), "invalidEvidence");
        var fakeSource = answer.DeepClone(); fakeSource["stage"]!["studyEvidence"]![0]!["sourceId"] = "study.otherPair";
        Reject("unknown source reference", () => Evaluation.ValidateAnswer(fakeSource.ToJsonString(), fixture), "invalidEvidence");
        var wrongType = answer.DeepClone(); wrongType["stage"]!["patientEvidence"]![0]!["value"] = 1;
        Reject("JSON types not coerced", () => Evaluation.ValidateAnswer(wrongType.ToJsonString(), fixture), "invalidEvidence");
        var absentQuote = answer.DeepClone(); absentQuote["stage"]!["studyEvidence"]![0]!["sourceId"] = "study.officialTitle";
        Reject("absent source cannot supply evidence", () => Evaluation.ValidateAnswer(absentQuote.ToJsonString(), fixture), "invalidEvidence");
        var wrongLogic = answer.DeepClone(); wrongLogic["stage"]!["result"] = "excluded";
        Check("authentic evidence does not prove OR interpretation", () =>
        {
            var result = Evaluation.ValidateAnswer(wrongLogic.ToJsonString(), fixture);
            Assert(result["expectedLabelDifferences"]!.AsArray().Select(Evaluation.Text).Contains("stage"));
            Assert(Evaluation.Text(result["outcome"]) == "nonmatch" && result["overallPass"] is null);
        });
        var noRestriction = answer.DeepClone(); noRestriction["stage"]!["result"] = "noRestrictionIdentified";
        noRestriction["stage"]!["examinedSources"] = new JsonArray();
        Reject("absence claim without examined eligibility", () => Evaluation.ValidateAnswer(noRestriction.ToJsonString(), fixture), "invalidEvidence");
        var native = Evaluation.Request(fixture, config, prompt, schema, "native");
        var compatible = Evaluation.Request(fixture, config, prompt, schema, "compatible");
        Check("both adapters preserve messages/schema and shared settings", () =>
        {
            Assert(JsonNode.DeepEquals(native["messages"], compatible["messages"]));
            Assert(JsonNode.DeepEquals(native["format"], compatible["response_format"]!["json_schema"]!["schema"]));
            Assert(JsonNode.DeepEquals(native["options"]!["temperature"], compatible["temperature"]));
            Assert(JsonNode.DeepEquals(native["options"]!["top_p"], compatible["top_p"]));
            Assert(JsonNode.DeepEquals(native["options"]!["num_predict"], compatible["max_tokens"]));
            Assert(JsonNode.DeepEquals(native["options"]!["seed"], compatible["seed"]));
        });
        Check("answers/expectations are not supplied as model examples", () =>
        {
            var messages = native["messages"]!.ToJsonString();
            Assert(!messages.Contains("reviewGuidance", StringComparison.Ordinal) && !messages.Contains("expected", StringComparison.Ordinal));
            Assert(native["stream"]!.GetValue<bool>() == false);
        });
        var shortContext = config.DeepClone(); shortContext["contextTokens"] = 1;
        Check("oversized input is detected without changing input", () =>
            Assert(!Evaluation.Sizing(native, shortContext)["withinConservativeBound"]!.GetValue<bool>()));
        Check("sizing counts decoded Unicode message content and both reserves", () =>
        {
            var request = new JsonObject { ["messages"] = new JsonArray(
                new JsonObject { ["role"] = "system", ["content"] = "a" },
                new JsonObject { ["role"] = "user", ["content"] = "é😀" }) };
            var size = Evaluation.Sizing(request, config);
            Assert(size["messageContentUtf8Bytes"]!.GetValue<int>() == 7);
            Assert(size["conservativeTotalTokenBound"]!.GetValue<int>() == 7
                + config["framingReserveTokens"]!.GetValue<int>() + config["maxOutputTokens"]!.GetValue<int>());
        });
        Check("API packaging does not add prompt tokens or hide added message content", () =>
        {
            var changed = native.DeepClone().AsObject();
            var originalBound = Evaluation.Sizing(native, config)["conservativeTotalTokenBound"]!.GetValue<int>();
            changed["format"]!["description"] = new string('x', 20000);
            var size = Evaluation.Sizing(changed, config);
            Assert(size["conservativeTotalTokenBound"]!.GetValue<int>() == originalBound);
            Assert(size["requestUtf8Bytes"]!.GetValue<int>() > Evaluation.Sizing(native, config)["requestUtf8Bytes"]!.GetValue<int>());
            changed["messages"]![0]!["content"] = Evaluation.Text(changed["messages"]![0]!["content"]) + new string('x', 20000);
            Assert(!Evaluation.Sizing(changed, config)["withinConservativeBound"]!.GetValue<bool>());
            Assert(Evaluation.Sizing(changed, config)["conservativeTotalTokenBound"]!.GetValue<int>() == originalBound + 20000);
        });
        Check("sizing accepts the exact limit and rejects one token less without truncation", () =>
        {
            var before = native.ToJsonString();
            var boundary = config.DeepClone();
            var bound = Evaluation.Sizing(native, config)["conservativeTotalTokenBound"]!.GetValue<int>();
            boundary["contextTokens"] = bound;
            Assert(Evaluation.Sizing(native, boundary)["withinConservativeBound"]!.GetValue<bool>());
            boundary["contextTokens"] = bound - 1;
            Assert(!Evaluation.Sizing(native, boundary)["withinConservativeBound"]!.GetValue<bool>());
            Assert(native.ToJsonString() == before);
        });
        Check("both adapters have the same input bound and all six revised inputs fit", () =>
        {
            foreach (var item in fixtures)
            {
                var a = Evaluation.Sizing(Evaluation.Request(item!, config, prompt, schema, "native"), config);
                var b = Evaluation.Sizing(Evaluation.Request(item!, config, prompt, schema, "compatible"), config);
                Assert(a["withinConservativeBound"]!.GetValue<bool>() && b["withinConservativeBound"]!.GetValue<bool>());
                Assert(JsonNode.DeepEquals(a["conservativeTotalTokenBound"], b["conservativeTotalTokenBound"]));
            }
        });
        var withImages = native.DeepClone().AsObject(); withImages["messages"]![1]!["images"] = new JsonArray("image");
        Reject("message sizing rejects unreviewed media fields", () => Evaluation.Sizing(withImages, config));
        var withTools = native.DeepClone().AsObject(); withTools["tools"] = new JsonArray();
        Reject("message sizing rejects unreviewed tools", () => Evaluation.Sizing(withTools, config));
        Reject("remote inference endpoint rejected", () =>
        {
            var remote = config.DeepClone(); remote["baseUrl"] = "https://example.com/";
            Evaluation.ValidatePreparation(remote, fixtures);
        });
        Reject("unapproved seventh case rejected", () =>
        {
            var more = fixtures.DeepClone().AsArray(); more.Add(fixtures[0]!.DeepClone());
            Evaluation.ValidatePreparation(config, more);
        });
        var nativeEnvelope = new JsonObject { ["done"] = true, ["done_reason"] = "stop", ["message"] = new JsonObject { ["content"] = answer.ToJsonString(), ["thinking"] = "not retained" }, ["load_duration"] = 1000 };
        var compatibleEnvelope = new JsonObject { ["choices"] = new JsonArray(new JsonObject { ["finish_reason"] = "stop", ["message"] = new JsonObject { ["content"] = answer.ToJsonString() } }) };
        Check("both response adapters extract assessment without raw thinking", () =>
        {
            var a = Evaluation.Unpack(nativeEnvelope.ToJsonString(), "native");
            var b = Evaluation.Unpack(compatibleEnvelope.ToJsonString(), "compatible");
            Assert(a.Content == b.Content && !a.Metrics.ToJsonString().Contains("thinking", StringComparison.Ordinal));
            Assert(b.Metrics["loadTimingAvailable"]!.GetValue<bool>() == false);
        });
        var truncated = nativeEnvelope.DeepClone(); truncated["done_reason"] = "length";
        Reject("output-limited native response fails", () => Evaluation.Unpack(truncated.ToJsonString(), "native"), "invalidResponse");
        var refused = compatibleEnvelope.DeepClone(); refused["choices"]![0]!["finish_reason"] = "length";
        Reject("output-limited compatible response fails", () => Evaluation.Unpack(refused.ToJsonString(), "compatible"), "invalidResponse");
        var fingerprint = Evaluation.Fingerprint(root);
        Reject("committed example cannot grant execution", () => Evaluation.ValidateApproval(Evaluation.Read(Path.Combine(root, "execution-approval.example.json")), fingerprint));
        var approval = Approval(fingerprint);
        Check("exact run approval accepted offline", () => Evaluation.ValidateApproval(approval, fingerprint));
        var d1Approval = Approval(fingerprint);
        d1Approval["maxRequests"] = 1;
        d1Approval["caseIds"] = new JsonArray("D1");
        Check("D1-only approval accepted for D1 mode", () => Evaluation.ValidateApproval(d1Approval, fingerprint, "d1"));
        Reject("D1 approval cannot authorize the batch", () => Evaluation.ValidateApproval(d1Approval, fingerprint));
        Reject("batch approval cannot authorize D1 mode", () => Evaluation.ValidateApproval(approval, fingerprint, "d1"));
        var excessiveD1 = d1Approval.DeepClone(); excessiveD1["maxRequests"] = 6;
        Reject("D1 mode rejects a larger request allowance", () => Evaluation.ValidateApproval(excessiveD1, fingerprint, "d1"));
        var otherCase = d1Approval.DeepClone(); otherCase["caseIds"] = new JsonArray("D3");
        Reject("D1 mode rejects authorization for another case", () => Evaluation.ValidateApproval(otherCase, fingerprint, "d1"));
        var extraCase = d1Approval.DeepClone(); extraCase["caseIds"] = new JsonArray("D1", "D3");
        Reject("D1 mode rejects additional case IDs", () => Evaluation.ValidateApproval(extraCase, fingerprint, "d1"));
        var noCases = d1Approval.DeepClone(); noCases.AsObject().Remove("caseIds");
        Reject("D1 mode requires explicit case IDs", () => Evaluation.ValidateApproval(noCases, fingerprint, "d1"));
        var d1Template = Evaluation.Read(Path.Combine(root, "execution-approval-d1.example.json"));
        Reject("D1 example cannot grant execution", () => Evaluation.ValidateApproval(d1Template, fingerprint, "d1"));
        Reject("unknown selection scope rejected", () => Evaluation.SelectCases(fixtures, "other"));
        Check("D1 selection contains only the original unchanged D1 payload", () =>
        {
            var selected = Evaluation.SelectCases(fixtures, "d1");
            Assert(selected.Count == 1 && Evaluation.Text(selected[0]!["id"]) == "D1");
            Assert(JsonNode.DeepEquals(Evaluation.Request(selected[0]!, config, prompt, schema, "native"), native));
            selected[0]!["patient"]!["cancerType"] = "changed only in selected copy";
            Assert(Evaluation.Text(fixtures[0]!["patient"]!["cancerType"]) == "Fictional cancer A");
        });
        Check("batch selection retains all six cases in order", () =>
            Assert(Evaluation.SelectCases(fixtures, "batch").Select(item => Evaluation.Text(item!["id"])).SequenceEqual(Evaluation.CaseIds)));
        var notApproved = approval.DeepClone(); notApproved["approved"] = false;
        Reject("preparation is not execution approval", () => Evaluation.ValidateApproval(notApproved, fingerprint));
        var changed = approval.DeepClone(); changed["fingerprintSha256"] = new string('0', 64);
        Reject("changed candidate invalidates run approval", () => Evaluation.ValidateApproval(changed, fingerprint));
        var otherRoute = approval.DeepClone(); otherRoute["route"] = "compatible";
        Reject("compatible preview is not initial execution authorization", () => Evaluation.ValidateApproval(otherRoute, fingerprint));
        var traversal = approval.DeepClone(); traversal["runId"] = "../outside";
        Reject("run output cannot escape results directory", () => Evaluation.ValidateApproval(traversal, fingerprint));

        // In-memory handlers only: no sockets, server, Ollama, downloads, or model loading.
        using var fake = new FakeHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(nativeEnvelope.ToJsonString()) }));
        using var client = new HttpClient(fake) { BaseAddress = new Uri("http://127.0.0.1:1/") };
        var reply = await Evaluation.SendAsync(client, "api/chat", native, TimeSpan.FromSeconds(1), CancellationToken.None);
        Check("HTTP transport against in-memory handler", () => Assert(reply.Contains("done_reason", StringComparison.Ordinal) && fake.Calls == 1));
        using var d1Fake = new FakeHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(nativeEnvelope.ToJsonString()) }));
        using var d1Client = new HttpClient(d1Fake) { BaseAddress = client.BaseAddress };
        Evaluation.ValidateApproval(d1Approval, fingerprint, "d1");
        foreach (var selected in Evaluation.SelectCases(fixtures, "d1"))
            await Evaluation.SendAsync(d1Client, "api/chat", Evaluation.Request(selected!, config, prompt, schema, "native"), TimeSpan.FromSeconds(1), CancellationToken.None);
        Check("approved D1 selection makes exactly one fake transport request", () => Assert(d1Fake.Calls == 1));
        using var bad = new HttpClient(new FakeHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)))) { BaseAddress = client.BaseAddress };
        try { await Evaluation.SendAsync(bad, "api/chat", native, TimeSpan.FromSeconds(1), CancellationToken.None); throw new Exception("Expected HTTP failure"); }
        catch (EvaluationFailure ex) when (ex.Code == "inferenceError") { count++; Console.WriteLine("PASS HTTP error is execution failure"); }
        using var slow = new FakeHandler(async (_, token) => { await Task.Delay(Timeout.InfiniteTimeSpan, token); return new HttpResponseMessage(HttpStatusCode.OK); });
        using var slowClient = new HttpClient(slow) { BaseAddress = client.BaseAddress };
        try { await Evaluation.SendAsync(slowClient, "api/chat", native, TimeSpan.FromMilliseconds(30), CancellationToken.None); throw new Exception("Expected deadline"); }
        catch (EvaluationFailure ex) when (ex.Code == "inferenceTimeout") { count++; Console.WriteLine("PASS deadline cancels transport without retry"); }
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        try { await Evaluation.SendAsync(slowClient, "api/chat", native, TimeSpan.FromSeconds(1), cancel.Token); throw new Exception("Expected cancellation"); }
        catch (OperationCanceledException) { count++; Console.WriteLine("PASS explicit cancellation remains interruption"); }
        Console.WriteLine($"{count} offline checks passed. No network requests or inference performed.");
    }

    private static JsonObject Approval(JsonNode fingerprint) => new()
    {
        ["approved"] = true, ["contextAndOutputLimitsReviewed"] = true, ["templateAndTokenizerReviewed"] = true,
        ["fingerprintSha256"] = fingerprint["sha256"]!.DeepClone(), ["route"] = "native", ["maxRequests"] = 6,
        ["caseIds"] = new JsonArray(Evaluation.CaseIds.Select(id => (JsonNode?)JsonValue.Create(id)).ToArray()),
        ["runId"] = "offline-only", ["modelDigest"] = new string('a', 64), ["templateSha256"] = new string('b', 64),
        ["ollamaVersion"] = "offline-fake", ["humanApprovalReference"] = "offline fixture, never used for execution"
    };

    private static JsonObject SampleAnswer(JsonNode fixture)
    {
        var answer = new JsonObject();
        foreach (var name in Evaluation.Criteria)
        {
            var field = name == "cancerType" ? "patient.cancerType" : name == "stage" ? "patient.stage.value" : "patient.diseaseState.value";
            var catalog = Evaluation.Catalog(fixture);
            answer[name] = new JsonObject
            {
                ["assessmentStatus"] = "assessed", ["result"] = "satisfied",
                ["requirementSummary"] = "Offline response fixture; not an observed model answer",
                ["patientEvidence"] = new JsonArray(new JsonObject { ["kind"] = "value", ["sourceId"] = field, ["value"] = catalog[field]!["value"]!.DeepClone() }),
                ["studyEvidence"] = new JsonArray(new JsonObject { ["kind"] = "text", ["sourceId"] = "study.eligibilityCriteria", ["quote"] = fixture["study"]!["eligibilityCriteria"]!.DeepClone() }),
                ["examinedSources"] = new JsonArray("study.eligibilityCriteria"),
                ["explanation"] = "Authentic references alone do not establish semantic correctness."
            };
        }
        return answer;
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Calls++; return respond(request, cancellationToken); }
    }
}
