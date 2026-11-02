using System.Text.Json.Nodes;

internal static class SchemaGuidance
{
    internal const string Direct = "Compare the stated supported qualifier with the supplied patient value, independently of branch applicability. Comparable equal values yield alignmentSupported; comparable unequal values yield conflictIdentified. Unknown unsupported selectors cannot make known comparable values undetermined. Use undetermined only for missing supported comparison facts, ambiguous staging context or unestablished classification equivalence. Keep this finding even when the complete label is undetermined.";
    internal const string Whole = "Assess the complete label using its explicit relationships and applicable requirements. An unknown unsupported selector that determines which branch applies yields undetermined, unless an independent applicable conflict establishes conflict. A direct mismatch in a branch with unknown applicability is not an independent applicable conflict. Preserve the direct findings without changing them. Respect mandatory AND and satisfied OR alternatives.";

    internal static void Apply(JsonNode schema)
    {
        schema["$defs"]!["finding"]!["properties"]!["result"]!["description"] = Direct;
        schema["$defs"]!["label"]!["properties"]!["result"]!["description"] = Whole;
    }

    internal static void VerifyRequest(JsonNode body, JsonNode fixture, JsonNode config, string prompt)
    {
        var repo = new DirectoryInfo(Environment.CurrentDirectory);
        while (repo is not null && !File.Exists(Path.Combine(repo.FullName,"docs/planning/condition-label-evaluation-design.md"))) repo=repo.Parent;
        if (repo is null) throw new InvalidDataException("Run inside repository");
        var caseId = Evaluation.Text(fixture["id"]);
        if (ComparisonProfile.CaseIds.Contains(caseId))
        {
            var frozen = Evaluation.Read(Path.Combine(repo.FullName,"tools/condition-label-evaluation/baseline/requests",caseId+".request.json"));
            if (frozen.ToJsonString()!=body.ToJsonString()) throw new InvalidDataException("Frozen full-low reference request changed");
        }
        var reference = Evaluation.Read(Path.Combine(repo.FullName,"tools/condition-label-evaluation/baseline/requests/CL-D01.request.json"));
        if (Evaluation.Text(reference["format"]!["$defs"]!["finding"]!["properties"]!["result"]!["description"]) != Direct
            || Evaluation.Text(reference["format"]!["$defs"]!["label"]!["properties"]!["result"]!["description"]) != Whole)
            throw new InvalidDataException("Successful result descriptions changed");
        var old = ComparisonProfile.BaseRequest(fixture,config,prompt);
        var originalSchema = old["format"]!.ToJsonString();
        var restored = body["format"]!.DeepClone();
        restored["$defs"]!["finding"]!["properties"]!["result"]!.AsObject().Remove("description");
        restored["$defs"]!["label"]!["properties"]!["result"]!.AsObject().Remove("description");
        if (restored.ToJsonString()!=originalSchema) throw new InvalidDataException("Schema fields,order or validation constraints changed");
        Apply(old["format"]!);
        old["messages"]![0]!["content"] = prompt.TrimEnd()+"\nResponse schema:\n"+old["format"]!.ToJsonString();
        if (old.ToJsonString()!=body.ToJsonString()) throw new InvalidDataException("More than two schema descriptions changed from original v5 request");
    }
}
