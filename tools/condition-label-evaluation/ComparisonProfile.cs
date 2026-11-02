using System.Text;
using System.Text.Json.Nodes;

internal static class ComparisonProfile
{
    internal const string CandidatePreparation = "069cb29d15094136e5b55b10ed9777ba56ac99ce323d54ca8121579fd849da96";
    internal const string CandidatePromptSha256 = "f84d6f8f17dd5f71c7b2dc8b4f0e4b3b712a8c53a21ad4e0c5017ac881311a12";
    internal const string PromptPreparation = "2df57b35d043143ccce8bf48dcfcdd8a31ab32e814ad3d5c0ac9b7865b2b8a98";
    internal static readonly string[] CaseIds = Enumerable.Range(1,16).Select(i => $"CL-D{i:00}").ToArray();
    internal static string CandidateDirectory(string root) => Path.Combine(root, "baseline");
    internal static JsonNode Metadata(string root) => Evaluation.Read(Path.Combine(root, "baseline/metadata.json"));
    internal static string Prompt(string root)
    {
        var file = Path.Combine(root, "baseline/prompt.txt");
        if (Evaluation.Hash(File.ReadAllBytes(file)) != CandidatePromptSha256) throw new InvalidDataException("Only unchanged original v5 is allowed");
        return File.ReadAllText(file);
    }
    internal static JsonArray Fixtures(string root)
    {
        var current = Labels.LoadDevelopment(Path.GetFullPath(Path.Combine(root, "../..")));
        var reviewed = Evaluation.Read(Path.Combine(root, "baseline/development-inputs.json")).AsArray();
        return new JsonArray(CaseIds.Select(id =>
        {
            var f = current.Single(f => Evaluation.Text(f!["id"]) == id)!;
            var prior = reviewed.SingleOrDefault(f => Evaluation.Text(f!["id"]) == id);
            if (prior is not null && !JsonNode.DeepEquals(f, prior)) throw new InvalidDataException("Reviewed fixture changed");
            return f.DeepClone();
        }).ToArray());
    }
    internal static JsonArray Profiles(string root)
    {
        var config = Evaluation.Read(Path.Combine(root, "baseline/configuration.json"));
        return new JsonArray(config);
    }
    internal static JsonObject BaseRequest(JsonNode fixture, JsonNode config, string prompt)
    {
        var copy = config.DeepClone(); copy.AsObject().Remove("think");
        var body = Labels.Request(fixture, copy, prompt); body["think"] = config["think"]!.DeepClone();
        body["options"]!["repeat_penalty"] = config["repeatPenalty"]!.DeepClone();
        body["options"]!["presence_penalty"] = config["presencePenalty"]!.DeepClone();
        return body;
    }
    internal static JsonObject Request(JsonNode fixture, JsonNode config, string prompt)
    {
        var body = BaseRequest(fixture,config,prompt);
        SchemaGuidance.Apply(body["format"]!);
        body["messages"]![0]!["content"] = prompt.TrimEnd()+"\nResponse schema:\n"+body["format"]!.ToJsonString();
        SchemaGuidance.VerifyRequest(body,fixture,config,prompt);
        return body;
    }
    internal static void ValidateConfig(string root, JsonNode config, JsonArray fixtures)
    {
        if (!JsonNode.DeepEquals(config, Profiles(root).Single(p => Evaluation.Text(p!["profileId"]) == Evaluation.Text(config["profileId"])))
            || !fixtures.Select(f => Evaluation.Text(f!["id"])).SequenceEqual(CaseIds)) throw new InvalidDataException("Selected full-low profile/fixtures differ");
    }
    internal static JsonObject Fingerprint(string root)
    {
        var files = new JsonObject();
        var names = Directory.GetFiles(root).Where(p => Path.GetExtension(p) is ".cs" or ".csproj" or ".cjs" or ".md").Select(Path.GetFileName).Select(p => p!)
            .Concat(Directory.GetFiles(Path.Combine(root, "baseline"), "*", SearchOption.AllDirectories).Select(p => Path.GetRelativePath(root,p)))
            .Concat(new[] { ".gitignore", "baseline.sha256.json", "../../GLOSSARY.md", "../../docs/planning/condition-label-assessment-design.md", "../../docs/planning/condition-label-evaluation-design.md" });
        foreach (var name in names.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)) files[name] = Evaluation.Hash(File.ReadAllBytes(Path.Combine(root, name)));
        return new JsonObject { ["sha256"] = Evaluation.Hash(Encoding.UTF8.GetBytes(files.ToJsonString())), ["files"] = files };
    }
    internal static void ValidateOrder(string root, JsonNode config, JsonNode? approval = null)
    {
        if (Evaluation.Text(config["profileId"]) != "development-full-low-gpt-oss-20b") throw new InvalidDataException("Only low authorized");
    }
}
