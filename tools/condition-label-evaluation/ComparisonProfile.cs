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
    internal static JsonArray Profiles(string root, string route = "native")
    {
        var config = Evaluation.Read(Path.Combine(root, "baseline/configuration.json"));
        if (route == "openai-compatible")
        {
            config["profileId"] = "development-full-low-gpt-oss-20b-compatible";
            config["route"] = route;
            config["sourceModel"] = config["model"]!.DeepClone();
            config["sourceModelDigest"] = config["modelDigest"]!.DeepClone();
            config["model"] = "oncomatch-gpt-oss-20b-openai-compatible:latest";
            config["modelDigest"] = null;
            config["modelVerification"] = "not-verified";
            config["modelParameters"] = new JsonObject
                { ["num_ctx"] = 24576, ["top_k"] = 20, ["min_p"] = 0, ["repeat_penalty"] = 1 };
        }
        else if (route != "native") throw new InvalidDataException("Unknown inference route");
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
        var route = Evaluation.Text(config["route"]);
        if (route is not ("native" or "openai-compatible")) throw new InvalidDataException("Unknown inference route");
        var referenceConfig = config.DeepClone();
        if (route == "openai-compatible" && config["sourceModel"] is not null)
            referenceConfig["model"] = config["sourceModel"]!.DeepClone();
        var body = BaseRequest(fixture,referenceConfig,prompt);
        SchemaGuidance.Apply(body["format"]!);
        body["messages"]![0]!["content"] = prompt.TrimEnd()+"\nResponse schema:\n"+body["format"]!.ToJsonString();
        SchemaGuidance.VerifyRequest(body,fixture,referenceConfig,prompt);
        if (route == "native") return body;
        return new JsonObject
        {
            ["model"] = config["model"]!.DeepClone(), ["messages"] = body["messages"]!.DeepClone(),
            ["stream"] = false, ["n"] = 1, ["keep_alive"] = "5m", ["reasoning_effort"] = config["think"]!.DeepClone(),
            ["temperature"] = config["temperature"]!.DeepClone(), ["top_p"] = config["topP"]!.DeepClone(),
            ["seed"] = config["seed"]!.DeepClone(), ["presence_penalty"] = config["presencePenalty"]!.DeepClone(),
            ["max_tokens"] = config["maxOutputTokens"]!.DeepClone(),
            ["response_format"] = new JsonObject { ["type"] = "json_schema", ["json_schema"] = new JsonObject
                { ["name"] = "condition_label_assessment", ["schema"] = body["format"]!.DeepClone() } }
        };
    }
    internal static void ValidateConfig(string root, JsonNode config, JsonArray fixtures)
    {
        if (!JsonNode.DeepEquals(config, Profiles(root, Evaluation.Text(config["route"]))[0])
            || !fixtures.Select(f => Evaluation.Text(f!["id"])).SequenceEqual(CaseIds)) throw new InvalidDataException("Selected full-low profile/fixtures differ");
    }
    internal static string Endpoint(JsonNode config) => Evaluation.Text(config["route"]) switch
    {
        "native" => "/api/chat", "openai-compatible" => "/v1/chat/completions",
        _ => throw new InvalidDataException("Unknown inference route")
    };

    internal static void ValidateModelParameters(JsonNode config, string parameters)
    {
        var required = config["modelParameters"]!.AsObject();
        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in parameters.Split('\n'))
        {
            var fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length == 0 || !required.ContainsKey(fields[0])) continue;
            if (fields.Length != 2 || !found.Add(fields[0])
                || !double.TryParse(fields[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var actual)
                || actual != required[fields[0]]!.GetValue<int>())
                throw new InvalidDataException("Declared model parameter differs: " + fields[0]);
        }
        if (found.Count != required.Count) throw new InvalidDataException("Required explicit model parameters missing");
    }

    internal static string Prepare(string root, JsonArray fixtures, string prompt, string route = "native")
    {
        var config = Profiles(root, route)[0]!;
        ValidateConfig(root, config, fixtures);
        var fingerprint = Fingerprint(root);
        var directory = Path.Combine(root, "results/preparation", Evaluation.Text(fingerprint["sha256"]));
        var profileId = Evaluation.Text(config["profileId"]);
        var profileDirectory = Path.Combine(directory, profileId);
        Directory.CreateDirectory(profileDirectory);
        File.WriteAllText(Path.Combine(profileDirectory, "configuration.json"), config.ToJsonString(Evaluation.Pretty) + "\n", new UTF8Encoding(false));
        var requests = new JsonArray();
        foreach (var fixture in fixtures)
        {
            var id = Evaluation.Text(fixture!["id"]);
            var body = Request(fixture, config, prompt);
            var sizing = Evaluation.Sizing(body, config);
            if (!sizing["withinConservativeBound"]!.GetValue<bool>()) throw new InvalidDataException("Input too large; never truncate");
            var file = Path.Combine(profileDirectory, id + ".request.json");
            File.WriteAllText(file, body.ToJsonString(Evaluation.Pretty) + "\n", new UTF8Encoding(false));
            requests.Add(new JsonObject { ["caseId"] = id, ["requestFileSha256"] = Evaluation.Hash(File.ReadAllBytes(file)),
                ["requestSha256"] = Evaluation.Hash(Encoding.UTF8.GetBytes(body.ToJsonString())), ["sizing"] = sizing });
        }
        var manifestFile = Path.Combine(directory, "manifest.json");
        var entries = File.Exists(manifestFile) ? Evaluation.Read(manifestFile)["profiles"]!.AsArray().DeepClone().AsArray() : new JsonArray();
        foreach (var entry in entries.Where(p => Evaluation.Text(p!["profileId"]) == profileId).ToArray()) entries.Remove(entry);
        entries.Add(new JsonObject
        {
            ["profileId"] = profileId, ["route"] = route, ["endpoint"] = Endpoint(config),
            ["configuration"] = config.DeepClone(), ["configurationSha256"] = Evaluation.Hash(Encoding.UTF8.GetBytes(config.ToJsonString())),
            ["modelVerification"] = "not-verified", ["requests"] = requests
        });
        File.WriteAllText(Path.Combine(directory, "development-inputs.json"), fixtures.ToJsonString(Evaluation.Pretty) + "\n", new UTF8Encoding(false));
        File.WriteAllText(manifestFile, new JsonObject
        {
            ["fingerprint"] = fingerprint, ["profiles"] = entries, ["maximumRequests"] = 16,
            ["maximumGeneratedTokens"] = 16 * 8192, ["combinedProfileCeilingSeconds"] = 9660,
            ["status"] = "Offline preparation only; limits are per candidate, not execution approval; actual model/server not verified; acceptance reserved",
            ["promptPreparation"] = PromptPreparation
        }.ToJsonString(Evaluation.Pretty) + "\n", new UTF8Encoding(false));
        return directory;
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
