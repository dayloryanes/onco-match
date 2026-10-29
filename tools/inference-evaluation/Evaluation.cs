using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;

internal sealed class EvaluationFailure(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

internal static class Evaluation
{
    internal static readonly string[] CaseIds = ["D1", "D3", "D5", "D6", "D9", "D13"];
    internal static readonly string[] Criteria = ["cancerType", "stage", "diseaseState"];
    internal static readonly string[] InputFiles = ["candidate.json", "fixtures.json", "prompt.txt", "response-schema.json",
        "Program.cs", "Evaluation.cs", "OfflineChecks.cs", "InferenceEvaluation.csproj", "README.md", "../../GLOSSARY.md",
        "../../docs/planning/inference-evaluation-design.md", "../../docs/planning/assessment-contract-design.md"];
    internal static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    internal static string Text(JsonNode? value) => value?.GetValue<string>() ?? throw new InvalidDataException("Expected a string");
    internal static JsonNode Read(string path) => Parse(File.ReadAllText(path));
    internal static JsonNode Parse(string text)
    {
        using var document = JsonDocument.Parse(text);
        CheckDuplicates(document.RootElement);
        return JsonNode.Parse(text) ?? throw new JsonException("JSON null is not an object");
    }

    private static void CheckDuplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new JsonException($"Duplicate property: {property.Name}");
                CheckDuplicates(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) CheckDuplicates(item);
    }

    internal static void ExactKeys(JsonNode node, params string[] keys)
    {
        if (node is not JsonObject obj || !obj.Select(p => p.Key).Order().SequenceEqual(keys.Order()))
            throw new EvaluationFailure("invalidResponse", "Missing or unexpected response fields");
    }

    internal static JsonObject Catalog(JsonNode fixture)
    {
        var sources = new JsonObject();
        void Add(string id, string path, JsonNode? value, bool present) => sources[id] = new JsonObject
        {
            ["path"] = path, ["present"] = present, ["value"] = value?.DeepClone()
        };
        var patient = fixture["patient"]!;
        Add("patient.cancerType", "patient.cancerType", patient["cancerType"], true);
        foreach (var group in new[] { "stage", "diseaseState" })
            foreach (var property in patient[group]!.AsObject())
                Add($"patient.{group}.{property.Key}", $"patient.{group}.{property.Key}", property.Value, true);
        var study = fixture["study"]!;
        var paths = new Dictionary<string, string>
        {
            ["briefTitle"] = "identificationModule.briefTitle", ["officialTitle"] = "identificationModule.officialTitle",
            ["briefSummary"] = "descriptionModule.briefSummary", ["detailedDescription"] = "descriptionModule.detailedDescription",
            ["eligibilityCriteria"] = "eligibilityModule.eligibilityCriteria", ["studyPopulation"] = "eligibilityModule.studyPopulation"
        };
        foreach (var (field, path) in paths)
            Add($"study.{field}", $"protocolSection.{path}", study[field], study[field] is not null);
        for (var i = 0; i < study["conditions"]!.AsArray().Count; i++)
            Add($"study.conditions.{i}", $"protocolSection.conditionsModule.conditions[{i}]", study["conditions"]![i], true);
        return sources;
    }

    internal static JsonArray Messages(JsonNode fixture, string prompt) => new(
        new JsonObject { ["role"] = "system", ["content"] = prompt },
        new JsonObject { ["role"] = "user", ["content"] = new JsonObject { ["sources"] = Catalog(fixture) }.ToJsonString() });

    internal static JsonObject Request(JsonNode fixture, JsonNode config, string prompt, JsonNode schema, string route)
    {
        // Derive allowed IDs from supplied inputs only, never the answer key.
        var catalog = Catalog(fixture);
        var requestSchema = schema.DeepClone();
        JsonArray Ids(Func<KeyValuePair<string, JsonNode?>, bool> include) =>
            new(catalog.Where(include).Select(p => (JsonNode?)JsonValue.Create(p.Key)).ToArray());
        requestSchema["$defs"]!["valueEvidence"]!["properties"]!["sourceId"]!["enum"] =
            Ids(p => p.Key.StartsWith("patient.", StringComparison.Ordinal) && p.Value!["present"]!.GetValue<bool>());
        requestSchema["$defs"]!["textEvidence"]!["properties"]!["sourceId"]!["enum"] =
            Ids(p => p.Key.StartsWith("study.", StringComparison.Ordinal) && p.Value!["present"]!.GetValue<bool>()
                && p.Value["value"] is JsonValue value && value.TryGetValue<string>(out var text) && text.Length > 0);
        foreach (var definition in new[] { "criterion", "cancerCriterion" })
            requestSchema["$defs"]![definition]!["properties"]!["examinedSources"]!["items"]!["enum"] =
                Ids(p => p.Key.StartsWith("study.", StringComparison.Ordinal));
        var body = new JsonObject
        {
            ["model"] = config["model"]!.DeepClone(),
            ["messages"] = Messages(fixture, prompt.TrimEnd() + "\nResponse schema:\n" + requestSchema.ToJsonString()), ["stream"] = false
        };
        if (route == "native")
        {
            body["format"] = requestSchema;
            // No separate thinking control is part of this experiment.
            body["keep_alive"] = "5m";
            body["options"] = new JsonObject
            {
                ["temperature"] = config["temperature"]!.DeepClone(), ["top_p"] = config["topP"]!.DeepClone(),
                ["top_k"] = config["topK"]!.DeepClone(), ["min_p"] = config["minP"]!.DeepClone(),
                ["seed"] = config["seed"]!.DeepClone(), ["num_ctx"] = config["contextTokens"]!.DeepClone(),
                ["num_predict"] = config["maxOutputTokens"]!.DeepClone()
            };
        }
        else if (route == "compatible")
        {
            body["temperature"] = config["temperature"]!.DeepClone();
            body["top_p"] = config["topP"]!.DeepClone();
            body["seed"] = config["seed"]!.DeepClone();
            body["max_tokens"] = config["maxOutputTokens"]!.DeepClone();
            body["response_format"] = new JsonObject
            {
                ["type"] = "json_schema", ["json_schema"] = new JsonObject
                { ["name"] = "oncomatch_assessment", ["strict"] = true, ["schema"] = requestSchema }
            };
        }
        else throw new InvalidDataException("Route must be native or compatible");
        return body;
    }

    internal static JsonObject Sizing(JsonObject request, JsonNode config)
    {
        // Reviewed byte-level BPE models: decoded message bytes bound content tokens.
        // The schema embedded in the system message counts once. API-only format/schema,
        // HTTP JSON escaping, and inference options are not rendered into the prompt.
        // This bound covers only the reviewed two-message, text-only, no-tools path.
        if (request["messages"] is not JsonArray messages || messages.Count != 2 || request["tools"] is not null)
            throw new InvalidDataException("Sizing requires two text-only messages and no tools");
        var contentBytes = 0;
        for (var i = 0; i < messages.Count; i++)
        {
            var message = messages[i] ?? throw new InvalidDataException("Missing message");
            ExactKeys(message, "role", "content");
            if (Text(message["role"]) != (i == 0 ? "system" : "user"))
                throw new InvalidDataException("Sizing requires system then user messages");
            contentBytes = checked(contentBytes + Encoding.UTF8.GetByteCount(Text(message["content"])));
        }
        var framing = config["framingReserveTokens"]!.GetValue<int>();
        var output = config["maxOutputTokens"]!.GetValue<int>();
        if (framing <= 0 || output <= 0) throw new InvalidDataException("Sizing reserves must be positive");
        var bound = checked(contentBytes + framing + output);
        return new JsonObject
        {
            ["requestUtf8Bytes"] = Encoding.UTF8.GetByteCount(request.ToJsonString()),
            ["messageContentUtf8Bytes"] = contentBytes, ["framingReserveTokens"] = framing,
            ["maxOutputTokens"] = output, ["conservativeTotalTokenBound"] = bound,
            ["contextTokens"] = config["contextTokens"]!.DeepClone(),
            ["withinConservativeBound"] = bound <= config["contextTokens"]!.GetValue<int>(),
            ["method"] = "Decoded message-content UTF-8 byte bound plus reviewed chat framing reserve and output allowance; API-only fields excluded; not an exact token count or VRAM estimate"
        };
    }

    internal static JsonObject Fingerprint(string root)
    {
        var files = new JsonObject();
        foreach (var name in InputFiles) files[name] = Hash(File.ReadAllBytes(Path.Combine(root, name)));
        return new JsonObject { ["sha256"] = Hash(Encoding.UTF8.GetBytes(files.ToJsonString())), ["files"] = files };
    }
    internal static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    internal static string[] ScopeCaseIds(string scope) => scope switch
    {
        "batch" => CaseIds,
        "d1" => ["D1"],
        _ => throw new InvalidDataException("Scope must be batch or d1")
    };

    internal static JsonArray SelectCases(JsonArray fixtures, string scope)
    {
        var selected = new JsonArray();
        foreach (var id in ScopeCaseIds(scope))
        {
            var fixture = fixtures.SingleOrDefault(f => Text(f!["id"]) == id)
                ?? throw new InvalidDataException($"Missing fixture {id}");
            selected.Add(fixture.DeepClone());
        }
        return selected;
    }

    internal static void ValidateApproval(JsonNode approval, JsonNode fingerprint, string scope = "batch")
    {
        var ids = ScopeCaseIds(scope);
        if (approval["approved"]?.GetValue<bool>() != true
            || approval["contextAndOutputLimitsReviewed"]?.GetValue<bool>() != true
            || approval["templateAndTokenizerReviewed"]?.GetValue<bool>() != true
            || Text(approval["fingerprintSha256"]) != Text(fingerprint["sha256"])
            || Text(approval["route"]) != "native" || approval["maxRequests"]?.GetValue<int>() != ids.Length
            || approval["caseIds"] is not JsonArray approvedIds || !approvedIds.Select(Text).SequenceEqual(ids))
            throw new InvalidDataException($"Missing/mismatched approval for this exact native {scope} scope");
        var runId = Text(approval["runId"]);
        if (!System.Text.RegularExpressions.Regex.IsMatch(runId, "^[a-z0-9][a-z0-9-]{0,63}$") || runId is "preparation")
            throw new InvalidDataException("Use a unique lowercase run ID; path separators are not allowed");
        foreach (var field in new[] { "modelDigest", "templateSha256" })
            if (!System.Text.RegularExpressions.Regex.IsMatch(Text(approval[field]), "^[a-f0-9]{64}$"))
                throw new InvalidDataException($"An exact reviewed {field} is required");
        if (string.IsNullOrWhiteSpace(Text(approval["ollamaVersion"])) || string.IsNullOrWhiteSpace(Text(approval["humanApprovalReference"])))
            throw new InvalidDataException("Record the installed version and actual human execution approval");
    }

    internal static void ValidatePreparation(JsonNode config, JsonArray fixtures)
    {
        if (!config["caseIds"]!.AsArray().Select(Text).SequenceEqual(CaseIds)
            || !fixtures.Select(f => Text(f!["id"])).SequenceEqual(CaseIds))
            throw new InvalidDataException("This checkpoint supports exactly the six agreed development cases, in order");
        if (Text(config["route"]) != "native" || string.IsNullOrWhiteSpace(Text(config["model"]))
            || config["requestTimeoutSeconds"]!.GetValue<int>() != 120)
            throw new InvalidDataException("Invalid native feasibility configuration");
        foreach (var fixture in fixtures)
        {
            // Only the six reviewed, whole-year fixtures are supported here. No general production age parser is implied.
            if (fixture!["patient"]!["ageYears"]!.GetValue<int>() != 52
                || Text(fixture["patient"]!["sex"]) != "male"
                || Text(fixture["study"]!["minimumAge"]) != "21 Years"
                || Text(fixture["study"]!["maximumAge"]) != "75 Years"
                || Text(fixture["study"]!["sex"]) != "ALL")
                throw new InvalidDataException("Unsupported deterministic inputs; needs a reviewed runner extension");
        }
        if (config["contextTokens"]!.GetValue<int>() <= 0 || config["maxOutputTokens"]!.GetValue<int>() <= 0)
            throw new InvalidDataException("Invalid context/output limit");
        var uri = new Uri(Text(config["baseUrl"]));
        if (uri.Scheme != "http" || !IPAddress.TryParse(uri.Host, out var address) || !IPAddress.IsLoopback(address)
            || uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.UserInfo.Length != 0)
            throw new InvalidDataException("Only an explicit loopback HTTP Ollama endpoint is supported");
    }

    internal static JsonObject ValidateAnswer(string content, JsonNode fixture)
    {
        JsonNode answer;
        try { answer = Parse(content); }
        catch (JsonException ex) { throw new EvaluationFailure("invalidResponse", ex.Message); }
        ExactKeys(answer, Criteria);
        var catalog = Catalog(fixture);
        foreach (var name in Criteria)
        {
            var summary = answer[name] ?? throw new EvaluationFailure("invalidResponse", "Null criterion");
            ExactKeys(summary, "assessmentStatus", "result", "requirementSummary", "patientEvidence", "studyEvidence", "examinedSources", "explanation");
            string RequiredText(JsonNode? value)
            {
                if (value is not JsonValue v || !v.TryGetValue<string>(out var s) || string.IsNullOrWhiteSpace(s))
                    throw new EvaluationFailure("invalidResponse", "Required nonempty text missing");
                return s;
            }
            var result = RequiredText(summary["result"]);
            if (RequiredText(summary["assessmentStatus"]) != "assessed"
                || !new[] { "satisfied", "excluded", "undetermined", "noRestrictionIdentified" }.Contains(result)
                || name == "cancerType" && result == "noRestrictionIdentified")
                throw new EvaluationFailure("invalidResponse", "Invalid criterion result/status");
            RequiredText(summary["requirementSummary"]);
            RequiredText(summary["explanation"]);
            var patientEvidence = summary["patientEvidence"] as JsonArray ?? throw new EvaluationFailure("invalidResponse", "Expected patient evidence list");
            var studyEvidence = summary["studyEvidence"] as JsonArray ?? throw new EvaluationFailure("invalidResponse", "Expected study evidence list");
            var examined = summary["examinedSources"] as JsonArray ?? throw new EvaluationFailure("invalidResponse", "Expected examined sources list");
            foreach (var reference in patientEvidence.Concat(studyEvidence))
            {
                if (reference is null) throw new EvaluationFailure("invalidResponse", "Null evidence");
                var kind = RequiredText(reference["kind"]);
                var id = RequiredText(reference["sourceId"]);
                var isPatient = patientEvidence.Any(item => ReferenceEquals(item, reference));
                if (kind != (isPatient ? "value" : "text")) throw new EvaluationFailure("invalidResponse", "Evidence kind does not match list");
                ExactKeys(reference, "kind", "sourceId", isPatient ? "value" : "quote");
                if (!id.StartsWith(isPatient ? "patient." : "study.", StringComparison.Ordinal)
                    || catalog[id] is not JsonObject source || source["present"]!.GetValue<bool>() != true)
                    throw new EvaluationFailure("invalidEvidence", "Unknown, absent, or wrong-category evidence source");
                if (isPatient)
                {
                    if (!JsonNode.DeepEquals(reference["value"], source["value"]))
                        throw new EvaluationFailure("invalidEvidence", "Patient evidence value/type differs from supplied input");
                }
                else if (!RequiredText(source["value"]).Contains(RequiredText(reference["quote"]), StringComparison.Ordinal))
                    throw new EvaluationFailure("invalidEvidence", "Quotation is not an exact substring");
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in examined)
            {
                var id = RequiredText(item);
                if (!id.StartsWith("study.", StringComparison.Ordinal) || catalog[id] is null || !seen.Add(id))
                    throw new EvaluationFailure("invalidEvidence", "Invalid or duplicate examined source");
            }
            if (result is "satisfied" or "excluded" && (patientEvidence.Count == 0 || studyEvidence.Count == 0))
                throw new EvaluationFailure("invalidEvidence", "Positive claims need patient and study evidence");
            if (result == "noRestrictionIdentified" && (!seen.Contains("study.eligibilityCriteria")
                || catalog["study.eligibilityCriteria"]!["present"]!.GetValue<bool>() != true))
                throw new EvaluationFailure("invalidEvidence", "Absence claim needs present relevant eligibility content and examined-source declaration");
            if (result == "undetermined" && patientEvidence.Count + studyEvidence.Count + examined.Count == 0)
                throw new EvaluationFailure("invalidEvidence", "Uncertainty must identify relevant supplied/missing sources");
        }
        var outcomes = Criteria.Select(c => Text(answer[c]!["result"])).ToArray();
        var outcome = outcomes.Contains("excluded") ? "nonmatch" : outcomes.Contains("undetermined") ? "insufficientInformation" : "potentialMatch";
        var differences = new JsonArray();
        foreach (var criterion in Criteria)
            if (Text(answer[criterion]!["result"]) != Text(fixture["expected"]![criterion])) differences.Add(criterion);
        if (outcome != Text(fixture["expected"]!["outcome"])) differences.Add("outcome");
        return new JsonObject
        {
            ["assessment"] = answer, ["outcome"] = outcome,
            ["deterministicResults"] = new JsonObject { ["age"] = "satisfied", ["sex"] = "noRestrictionIdentified" },
            ["expectedLabelDifferences"] = differences, ["semanticReview"] = "pending",
            ["overallPass"] = null, ["scope"] = "Preliminary research screening within the limited assessment scope; not clinical eligibility"
        };
    }

    internal static string RoutePath(string route) => route == "native" ? "api/chat" : "v1/chat/completions";

    internal static (string Content, JsonObject Metrics) Unpack(string response, string route)
    {
        var envelope = Parse(response);
        if (route == "native")
        {
            if (envelope["done"]?.GetValue<bool>() != true || Text(envelope["done_reason"]) != "stop")
                throw new EvaluationFailure("invalidResponse", "Incomplete or output-limited native response");
            var metrics = NativeMetrics(envelope);
            return (Text(envelope["message"]?["content"]), metrics);
        }
        if (envelope["choices"] is not JsonArray choices || choices.Count != 1 || Text(choices[0]?["finish_reason"]) != "stop")
            throw new EvaluationFailure("invalidResponse", "Incomplete/refused/ambiguous compatible response");
        return (Text(choices[0]!["message"]?["content"]), new JsonObject
        { ["usage"] = envelope["usage"]?.DeepClone(), ["loadTimingAvailable"] = false });
    }

    internal static JsonObject NativeMetrics(JsonNode envelope)
    {
        var metrics = new JsonObject();
        foreach (var key in new[] { "total_duration", "load_duration", "prompt_eval_count", "prompt_eval_duration", "eval_count", "eval_duration" })
            metrics[key] = envelope[key]?.DeepClone();
        return metrics;
    }

    internal static async Task<string> SendAsync(HttpClient client, string path, JsonNode body, TimeSpan timeout, CancellationToken stop)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stop);
        deadline.CancelAfter(timeout);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path)
            { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
            using var response = await client.SendAsync(request, deadline.Token);
            if (!response.IsSuccessStatusCode) throw new EvaluationFailure("inferenceError", $"HTTP {(int)response.StatusCode}");
            return await response.Content.ReadAsStringAsync(deadline.Token);
        }
        catch (OperationCanceledException) when (!stop.IsCancellationRequested)
        { throw new EvaluationFailure("inferenceTimeout", "Request deadline exceeded; server cancellation is not instantaneous"); }
    }
}
