using System.Text.Json;
using System.Text.Json.Nodes;
using OncoMatch.Api.Features.Patients;
using OncoMatch.Api.Features.Studies;

namespace OncoMatch.Api.Features.Match.Inference;

// Adapted from reviewed schema/validation logic; independent of runner assets and reporting.
public static class ConditionLabelContract
{
    private static readonly string[] Results = ["alignmentSupported", "conflictIdentified", "undetermined"];
    private static readonly string[] Findings = ["cancerType", "stage", "diseaseState"];
    public static JsonObject Sources(PatientDetails patient, StudyDetails study)
    {
        var catalog = new JsonObject { ["patient.cancerType"] = patient.CancerType,
            ["patient.stage.status"] = patient.Stage.Status, ["patient.stage.value"] = patient.Stage.Value,
            ["patient.stage.classificationSystem"] = patient.Stage.ClassificationSystem,
            ["patient.diseaseState.status"] = patient.DiseaseState.Status, ["patient.diseaseState.value"] = patient.DiseaseState.Value };
        for (var i = 0; i < (study.Conditions?.Length ?? 0); i++)
            if (!string.IsNullOrWhiteSpace(study.Conditions![i])) catalog[$"study.conditions.{i}"] = study.Conditions[i];
        return catalog;
    }
    private static string[] LabelIds(JsonObject sources) => sources.Select(p => p.Key).Where(k => k.StartsWith("study.conditions.", StringComparison.Ordinal)).ToArray();
    public static JsonArray Validate(string content, JsonObject sources)
    {
        try { return ValidateCore(content, sources); }
        catch (InferenceFailure) { throw; }
        catch (Exception error) when (error is JsonException or InvalidOperationException or InvalidDataException or ArgumentException)
        { throw new InferenceFailure("invalidResponse", "The model response does not match the application contract."); }
    }
    private static string Required(JsonNode? node)
    {
        if (node is not JsonValue value || !value.TryGetValue<string>(out var text) || string.IsNullOrWhiteSpace(text))
            throw new InferenceFailure("invalidResponse", "Required nonempty string missing");
        return text;
    }

    public static JsonObject Schema(JsonObject catalog)
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
            ["conditionLabelId"] = Enum(ids), ["result"] = Enum(Results), ["requirementSummary"] = String(),
            ["cancerType"] = Ref("cancerFinding"), ["stage"] = Ref("finding"), ["diseaseState"] = Ref("finding"),
            ["unassessedQualifiers"] = Array(Ref("qualifier")), ["explanation"] = String()
        });
        var schema = Object(new JsonObject { ["conditionLabels"] = new JsonObject { ["type"] = "array", ["items"] = Ref("label"), ["minItems"] = ids.Length, ["maxItems"] = ids.Length } });
        schema["$defs"] = definitions;
        return schema;
    }

    public static string Aggregate(IEnumerable<string> results)
    {
        var values = results.ToArray();
        return values.Length == 0 ? "conditionLabelAlignmentUndetermined" : values.All(v => v == Results[0]) ? "conditionLabelAlignmentSupported"
            : values.Contains(Results[0]) ? "mixedConditionLabelFindings" : values.All(v => v == Results[1]) ? "conditionLabelConflictsIdentified" : "conditionLabelAlignmentUndetermined";
    }

    private static JsonArray ValidateCore(string content, JsonObject catalog)
    {
        var answer = ContractJson.Parse(content);
        ContractJson.ExactKeys(answer, "conditionLabels");
        var ids = LabelIds(catalog);
        var entries = answer["conditionLabels"] as JsonArray ?? throw new InferenceFailure("invalidResponse", "Expected labels array");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (entry is null) throw new InferenceFailure("invalidResponse", "Null label");
            ContractJson.ExactKeys(entry, "conditionLabelId", "result", "requirementSummary", "cancerType", "stage", "diseaseState", "unassessedQualifiers", "explanation");
            var id = Required(entry["conditionLabelId"]);
            if (!ids.Contains(id) || !seen.Add(id)) throw new InferenceFailure("invalidResponse", "Unknown or duplicate label ID");
            if (!Results.Contains(Required(entry["result"]))) throw new InferenceFailure("invalidResponse", "Invalid complete-label result");
            Required(entry["requirementSummary"]); Required(entry["explanation"]);
            void StudyEvidence(JsonNode? value, bool mustHaveEvidence)
            {
                var evidence = value as JsonArray ?? throw new InferenceFailure("invalidResponse", "Expected study evidence array");
                foreach (var reference in evidence)
                {
                    if (reference is null) throw new InferenceFailure("invalidResponse", "Null evidence");
                    ContractJson.ExactKeys(reference, "sourceId", "quote");
                    if (Required(reference["sourceId"]) != id || !ContractJson.Text(catalog[id]).Contains(Required(reference["quote"]), StringComparison.Ordinal))
                        throw new InferenceFailure("invalidEvidence", "Quote must exactly reference this complete label");
                }
                if (mustHaveEvidence && evidence.Count == 0) throw new InferenceFailure("invalidEvidence", "Required label evidence missing");
            }
            foreach (var name in Findings)
            {
                var finding = entry[name] ?? throw new InferenceFailure("invalidResponse", "Missing finding");
                ContractJson.ExactKeys(finding, "result", "patientEvidence", "studyEvidence", "examinedSources", "explanation");
                var result = Required(finding["result"]);
                if (!Results.Contains(result) && !(name != "cancerType" && result == "notSpecified"))
                    throw new InferenceFailure("invalidResponse", "Invalid direct finding");
                Required(finding["explanation"]);
                var patientEvidence = finding["patientEvidence"] as JsonArray ?? throw new InferenceFailure("invalidResponse", "Expected patient evidence array");
                foreach (var reference in patientEvidence)
                {
                    if (reference is null) throw new InferenceFailure("invalidResponse", "Null evidence");
                    ContractJson.ExactKeys(reference, "sourceId", "value");
                    var source = Required(reference["sourceId"]);
                    if (!source.StartsWith("patient.", StringComparison.Ordinal) || !catalog.ContainsKey(source) || !JsonNode.DeepEquals(reference["value"], catalog[source]))
                        throw new InferenceFailure("invalidEvidence", "Patient reference differs from supplied value/type");
                }
                var positive = result is "alignmentSupported" or "conflictIdentified";
                var requiredPrefix = "patient." + name;
                if (positive && !patientEvidence.Any(reference => Required(reference!["sourceId"]) == requiredPrefix
                    || Required(reference!["sourceId"]).StartsWith(requiredPrefix + ".", StringComparison.Ordinal)))
                    throw new InferenceFailure("invalidEvidence", "A direct comparison requires patient evidence for that assessed aspect.");
                StudyEvidence(finding["studyEvidence"], positive);
                if (result == "notSpecified" && (patientEvidence.Count != 0 || finding["studyEvidence"]!.AsArray().Count != 0))
                    throw new InferenceFailure("invalidEvidence", "An absent qualifier requires empty comparison evidence.");
                var examined = finding["examinedSources"] as JsonArray ?? throw new InferenceFailure("invalidResponse", "Expected examined-source array");
                if (examined.Any(source => Required(source) != id) || examined.Count > 1)
                    throw new InferenceFailure("invalidEvidence", "Invalid or duplicate examined label");
                if (positive && patientEvidence.Count == 0 || result == "notSpecified" && examined.Count != 1
                    || result == "undetermined" && patientEvidence.Count + finding["studyEvidence"]!.AsArray().Count + examined.Count == 0)
                    throw new InferenceFailure("invalidEvidence", "Required comparison or coverage evidence missing");
            }
            var qualifiers = entry["unassessedQualifiers"] as JsonArray ?? throw new InferenceFailure("invalidResponse", "Expected qualifier array");
            foreach (var qualifier in qualifiers)
            {
                if (qualifier is null) throw new InferenceFailure("invalidResponse", "Null qualifier");
                ContractJson.ExactKeys(qualifier, "qualifier", "studyEvidence", "reason");
                Required(qualifier["qualifier"]); Required(qualifier["reason"]); StudyEvidence(qualifier["studyEvidence"], true);
            }
        }
        if (seen.Count != ids.Length) throw new InferenceFailure("invalidResponse", "Omitted condition label; no partial salvage");
        return entries;
    }
}
