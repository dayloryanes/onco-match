using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

internal static class Labels
{
    internal static readonly string[] Results = ["alignmentSupported", "conflictIdentified", "undetermined"];
    internal static readonly string[] Findings = ["cancerType", "stage", "diseaseState"];
    internal static readonly Dictionary<string, string> Symbols = new()
    { ["S"] = Results[0], ["C"] = Results[1], ["U"] = Results[2], ["N"] = "notSpecified" };

    // Read only the approved development section. Acceptance inputs never enter preparation.
    internal static JsonArray LoadDevelopment(string repository)
    {
        var design = File.ReadAllText(Path.Combine(repository, "docs/planning/condition-label-evaluation-design.md"));
        var baseText = Regex.Match(design, @"(?s)The controlled base patient is:.*?```json\s*(.*?)\s*```").Groups[1].Value;
        var baseline = Evaluation.Parse(baseText);
        var section = design.Split("## Development cases: exact inputs and reviewed expectations", StringSplitOptions.None)[1]
            .Split("## Controlled acceptance cases:", StringSplitOptions.None)[0];
        var fixtures = new JsonArray();
        foreach (Match match in Regex.Matches(section, @"(?ms)^### (CL-D\d{2}):[^\n]*\n(.*?)(?=^### |\z)"))
        {
            var id = match.Groups[1].Value;
            var text = match.Groups[2].Value;
            var patient = baseline.DeepClone();
            var changes = Evaluation.Parse(Regex.Match(text, @"Patient changes: `([^`]+)`").Groups[1].Value);
            foreach (var property in changes.AsObject()) patient[property.Key] = property.Value?.DeepClone();
            var conditions = Evaluation.Parse(Regex.Match(text, @"Conditions: `([^`]+)`").Groups[1].Value).AsArray();
            var expected = new JsonArray();
            foreach (Match tuple in Regex.Matches(text, @"\(([SCUN]),([SCUN]),([SCUN]);([SCU])\)"))
            {
                var label = new JsonObject { ["labelId"] = $"study.conditions.{expected.Count}" };
                for (var i = 0; i < 3; i++) label[Findings[i]] = Symbols[tuple.Groups[i + 1].Value];
                label["result"] = Symbols[tuple.Groups[4].Value];
                expected.Add(label);
            }
            var pairSymbol = Regex.Match(text, @"; pair ([SCUM])\.").Groups[1].Value;
            if (expected.Count != conditions.Count || pairSymbol.Length == 0)
                throw new InvalidDataException($"Could not read exact approved expectations for {id}");
            fixtures.Add(new JsonObject
            {
                ["id"] = id, ["patient"] = patient,
                ["study"] = new JsonObject { ["conditions"] = conditions, ["minimumAge"] = "18 Years", ["maximumAge"] = "65 Years", ["sex"] = "ALL" },
                ["expected"] = expected, ["expectedPairSummary"] = PairSymbol(pairSymbol),
                // Frozen review notes use CRLF; preserve their text across checkout formats.
                // These notes are never included in model requests.
                ["reviewGuidance"] = text.Trim().ReplaceLineEndings("\r\n")
            });
        }
        if (!fixtures.Select(f => Evaluation.Text(f!["id"])).SequenceEqual(Enumerable.Range(1, 16).Select(i => $"CL-D{i:00}")))
            throw new InvalidDataException("Expected exactly CL-D01 through CL-D16 in approved order");
        return fixtures;
    }

    internal static string PairSymbol(string symbol) => symbol switch
    {
        "S" => "conditionLabelAlignmentSupported", "C" => "conditionLabelConflictsIdentified",
        "M" => "mixedConditionLabelFindings", "U" => "conditionLabelAlignmentUndetermined",
        _ => throw new InvalidDataException("Unexpected pair symbol")
    };

    internal static string Required(JsonNode? node)
    {
        if (node is not JsonValue value || !value.TryGetValue<string>(out var text) || string.IsNullOrWhiteSpace(text))
            throw new EvaluationFailure("invalidResponse", "Required nonempty string missing");
        return text;
    }

    internal static JsonObject Catalog(JsonNode fixture)
    {
        var patient = fixture["patient"]!;
        var catalog = new JsonObject { ["patient.cancerType"] = patient["cancerType"]?.DeepClone() };
        foreach (var group in new[] { "stage", "diseaseState" })
            foreach (var property in patient[group]!.AsObject()) catalog[$"patient.{group}.{property.Key}"] = property.Value?.DeepClone();
        var conditionNode = fixture["study"]!["conditions"];
        if (conditionNode is not null && conditionNode is not JsonArray)
            throw new InvalidDataException("Malformed conditions field; expected array, not missing text");
        var conditions = conditionNode as JsonArray;
        if (conditions is not null)
            for (var i = 0; i < conditions.Count; i++)
            {
                if (conditions[i] is not JsonValue value || !value.TryGetValue<string>(out var label))
                    throw new InvalidDataException("Malformed condition entry; do not convert to missing text");
                if (!string.IsNullOrWhiteSpace(label)) catalog[$"study.conditions.{i}"] = label;
            }
        return catalog;
    }

    internal static string[] LabelIds(JsonObject catalog) => catalog.Where(p => p.Key.StartsWith("study.conditions.", StringComparison.Ordinal)).Select(p => p.Key).ToArray();

    internal static JsonObject Schema(JsonObject catalog)
    {
        JsonObject Object(JsonObject properties) => new()
        { ["type"] = "object", ["properties"] = properties, ["required"] = new JsonArray(properties.Select(p => (JsonNode?)JsonValue.Create(p.Key)).ToArray()), ["additionalProperties"] = false };
        JsonObject String() => new() { ["type"] = "string", ["minLength"] = 1 };
        JsonObject Enum(IEnumerable<string> values) => new() { ["type"] = "string", ["enum"] = new JsonArray(values.Select(v => (JsonNode?)JsonValue.Create(v)).ToArray()) };
        JsonObject Ref(string name) => new() { ["$ref"] = $"#/$defs/{name}" };
        JsonObject Array(JsonNode item) => new() { ["type"] = "array", ["items"] = item };
        var ids = LabelIds(catalog);
        var patientIds = catalog.Select(p => p.Key).Where(id => id.StartsWith("patient.", StringComparison.Ordinal));
        var definitions = new JsonObject
        {
            ["patientEvidence"] = Object(new JsonObject { ["sourceId"] = Enum(patientIds), ["value"] = new JsonObject() }),
            ["studyEvidence"] = Object(new JsonObject { ["sourceId"] = Enum(ids), ["quote"] = String() })
        };
        foreach (var name in new[] { "cancerFinding", "finding" })
            definitions[name] = Object(new JsonObject
            {
                ["result"] = Enum(name == "cancerFinding" ? Results : Results.Concat(["notSpecified"])),
                ["patientEvidence"] = Array(Ref("patientEvidence")), ["studyEvidence"] = Array(Ref("studyEvidence")),
                ["examinedSources"] = Array(Enum(ids)), ["explanation"] = String()
            });
        definitions["qualifier"] = Object(new JsonObject
        {
            ["qualifier"] = String(), ["studyEvidence"] = new JsonObject { ["type"] = "array", ["items"] = Ref("studyEvidence"), ["minItems"] = 1 }, ["reason"] = String()
        });
        definitions["label"] = Object(new JsonObject
        {
            ["labelId"] = Enum(ids), ["result"] = Enum(Results), ["requirementSummary"] = String(),
            ["cancerType"] = Ref("cancerFinding"), ["stage"] = Ref("finding"), ["diseaseState"] = Ref("finding"),
            ["unassessedQualifiers"] = Array(Ref("qualifier")), ["explanation"] = String()
        });
        var schema = Object(new JsonObject { ["labels"] = new JsonObject { ["type"] = "array", ["items"] = Ref("label"), ["minItems"] = ids.Length, ["maxItems"] = ids.Length } });
        schema["$defs"] = definitions;
        return schema;
    }

    internal static JsonObject Request(JsonNode fixture, JsonNode config, string prompt)
    {
        var catalog = Catalog(fixture);
        if (LabelIds(catalog).Length == 0) throw new InvalidDataException("No condition text supplied; no model request");
        var schema = Schema(catalog);
        var body = new JsonObject
        {
            ["model"] = config["model"]!.DeepClone(), ["stream"] = false, ["keep_alive"] = "5m", ["format"] = schema,
            ["messages"] = new JsonArray(
                new JsonObject { ["role"] = "system", ["content"] = prompt.TrimEnd() + "\nResponse schema:\n" + schema.ToJsonString() },
                new JsonObject { ["role"] = "user", ["content"] = new JsonObject { ["sources"] = catalog }.ToJsonString() }),
            ["options"] = new JsonObject
            {
                ["temperature"] = config["temperature"]!.DeepClone(), ["top_p"] = config["topP"]!.DeepClone(),
                ["top_k"] = config["topK"]!.DeepClone(), ["min_p"] = config["minP"]!.DeepClone(),
                ["seed"] = config["seed"]!.DeepClone(), ["num_ctx"] = config["contextTokens"]!.DeepClone(), ["num_predict"] = config["maxOutputTokens"]!.DeepClone()
            }
        };
        if (config["think"] is not null) body["think"] = config["think"]!.GetValue<bool>();
        return body;
    }

    internal static string Aggregate(IEnumerable<string> results)
    {
        var values = results.ToArray();
        return values.Length == 0 ? PairSymbol("U") : values.All(v => v == Results[0]) ? PairSymbol("S")
            : values.Contains(Results[0]) ? PairSymbol("M") : values.All(v => v == Results[1]) ? PairSymbol("C") : PairSymbol("U");
    }

    internal static JsonObject Validate(string content, JsonNode fixture)
    {
        try { return ValidateCore(content, fixture); }
        catch (EvaluationFailure) { throw; }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or InvalidDataException or ArgumentException)
        { throw new EvaluationFailure("invalidResponse", ex.Message); }
    }

    private static JsonObject ValidateCore(string content, JsonNode fixture)
    {
        var answer = Evaluation.Parse(content);
        Evaluation.ExactKeys(answer, "labels");
        var catalog = Catalog(fixture);
        var ids = LabelIds(catalog);
        var entries = answer["labels"] as JsonArray ?? throw new EvaluationFailure("invalidResponse", "Expected labels array");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var differences = new JsonArray();
        foreach (var entry in entries)
        {
            if (entry is null) throw new EvaluationFailure("invalidResponse", "Null label");
            Evaluation.ExactKeys(entry, "labelId", "result", "requirementSummary", "cancerType", "stage", "diseaseState", "unassessedQualifiers", "explanation");
            var id = Required(entry["labelId"]);
            if (!ids.Contains(id) || !seen.Add(id)) throw new EvaluationFailure("invalidResponse", "Unknown or duplicate label ID");
            if (!Results.Contains(Required(entry["result"]))) throw new EvaluationFailure("invalidResponse", "Invalid complete-label result");
            Required(entry["requirementSummary"]); Required(entry["explanation"]);
            void StudyEvidence(JsonNode? value, bool mustHaveEvidence)
            {
                var evidence = value as JsonArray ?? throw new EvaluationFailure("invalidResponse", "Expected study evidence array");
                foreach (var reference in evidence)
                {
                    if (reference is null) throw new EvaluationFailure("invalidResponse", "Null evidence");
                    Evaluation.ExactKeys(reference, "sourceId", "quote");
                    if (Required(reference["sourceId"]) != id || !Evaluation.Text(catalog[id]).Contains(Required(reference["quote"]), StringComparison.Ordinal))
                        throw new EvaluationFailure("invalidEvidence", "Quote must exactly reference this complete label");
                }
                if (mustHaveEvidence && evidence.Count == 0) throw new EvaluationFailure("invalidEvidence", "Required label evidence missing");
            }
            foreach (var name in Findings)
            {
                var finding = entry[name] ?? throw new EvaluationFailure("invalidResponse", "Missing finding");
                Evaluation.ExactKeys(finding, "result", "patientEvidence", "studyEvidence", "examinedSources", "explanation");
                var result = Required(finding["result"]);
                if (!Results.Contains(result) && !(name != "cancerType" && result == "notSpecified"))
                    throw new EvaluationFailure("invalidResponse", "Invalid direct finding");
                Required(finding["explanation"]);
                var patientEvidence = finding["patientEvidence"] as JsonArray ?? throw new EvaluationFailure("invalidResponse", "Expected patient evidence array");
                foreach (var reference in patientEvidence)
                {
                    if (reference is null) throw new EvaluationFailure("invalidResponse", "Null evidence");
                    Evaluation.ExactKeys(reference, "sourceId", "value");
                    var source = Required(reference["sourceId"]);
                    if (!source.StartsWith("patient.", StringComparison.Ordinal) || !catalog.ContainsKey(source) || !JsonNode.DeepEquals(reference["value"], catalog[source]))
                        throw new EvaluationFailure("invalidEvidence", "Patient reference differs from supplied value/type");
                }
                var positive = result is "alignmentSupported" or "conflictIdentified";
                StudyEvidence(finding["studyEvidence"], positive);
                var examined = finding["examinedSources"] as JsonArray ?? throw new EvaluationFailure("invalidResponse", "Expected examined-source array");
                if (examined.Any(source => Required(source) != id) || examined.Count > 1)
                    throw new EvaluationFailure("invalidEvidence", "Invalid or duplicate examined label");
                if (positive && patientEvidence.Count == 0 || result == "notSpecified" && examined.Count != 1
                    || result == "undetermined" && patientEvidence.Count + finding["studyEvidence"]!.AsArray().Count + examined.Count == 0)
                    throw new EvaluationFailure("invalidEvidence", "Required comparison or coverage evidence missing");
            }
            var qualifiers = entry["unassessedQualifiers"] as JsonArray ?? throw new EvaluationFailure("invalidResponse", "Expected qualifier array");
            foreach (var qualifier in qualifiers)
            {
                if (qualifier is null) throw new EvaluationFailure("invalidResponse", "Null qualifier");
                Evaluation.ExactKeys(qualifier, "qualifier", "studyEvidence", "reason");
                Required(qualifier["qualifier"]); Required(qualifier["reason"]); StudyEvidence(qualifier["studyEvidence"], true);
            }
            var expected = fixture["expected"]?.AsArray().SingleOrDefault(e => Evaluation.Text(e!["labelId"]) == id);
            if (expected is not null)
            {
                foreach (var name in Findings)
                    if (Required(entry[name]!["result"]) != Evaluation.Text(expected[name])) differences.Add($"{id}.{name}");
                if (Required(entry["result"]) != Evaluation.Text(expected["result"])) differences.Add($"{id}.result");
            }
        }
        if (seen.Count != ids.Length) throw new EvaluationFailure("invalidResponse", "Omitted label entry; no partial salvage");
        var summary = Aggregate(entries.Select(e => Required(e!["result"])));
        if (fixture["expectedPairSummary"] is not null && summary != Evaluation.Text(fixture["expectedPairSummary"])) differences.Add("pairSummary");
        return new JsonObject
        {
            ["assessment"] = answer, ["pairSummary"] = summary, ["expectedLabelDifferences"] = differences,
            ["semanticReview"] = "pending; review applicability, evidence relevance, required qualifiers and explanations against approved guidance",
            ["overallPass"] = null,
            ["scope"] = "Label aggregation only; fixture structured age/sex checks are separate. Not clinical eligibility."
        };
    }
}
