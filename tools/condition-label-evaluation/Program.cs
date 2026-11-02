using System.Text;
using System.Text.Json.Nodes;

try
{
    var repository = new DirectoryInfo(Environment.CurrentDirectory);
    while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "docs/planning/condition-label-evaluation-design.md"))) repository = repository.Parent;
    var root = Path.Combine(repository?.FullName ?? throw new InvalidDataException("Run inside repository"), "tools/condition-label-evaluation");
    var fixtures = ComparisonProfile.Fixtures(root); var prompt = ComparisonProfile.Prompt(root); var profiles = ComparisonProfile.Profiles(root);
    if (args.Length == 3 && args[0] == "run")
    {
        var config = profiles.Single(p => Evaluation.Text(p!["profileId"]) == args[1])!;
        await ComparisonPilot.RunAsync(root, fixtures, prompt, config, args[2]);
        return 0;
    }
    var fingerprint = ComparisonProfile.Fingerprint(root);
    var directory = Path.Combine(root, "results/preparation", Evaluation.Text(fingerprint["sha256"]));
    Directory.CreateDirectory(directory);
    if (args.SequenceEqual(new[] { "self-check" }))
    {
        var report = await ReasoningChecks.Run(root, fixtures, prompt, profiles);
        File.WriteAllText(Path.Combine(directory, "offline-checks.json"), report.ToJsonString(Evaluation.Pretty) + "\n");
        Console.WriteLine(report.ToJsonString(Evaluation.Pretty)); return 0;
    }
    if (!args.SequenceEqual(new[] { "prepare" })) throw new InvalidDataException("Use prepare, self-check or run <profile> <approval>");
    var prepared = new JsonArray();
    foreach (var config in profiles)
    {
        ComparisonProfile.ValidateConfig(root, config!, fixtures);
        var id = Evaluation.Text(config!["profileId"]); Directory.CreateDirectory(Path.Combine(directory, id));
        File.WriteAllText(Path.Combine(directory, id, "configuration.json"), config.ToJsonString(Evaluation.Pretty) + "\n");
        var requests = new JsonArray();
        foreach (var fixture in fixtures)
        {
            var caseId = Evaluation.Text(fixture!["id"]); var body = ComparisonProfile.Request(fixture, config, prompt);
            var reference = ComparisonProfile.BaseRequest(fixture, config, prompt);
            SchemaGuidance.Apply(reference["format"]!);
            reference["messages"]![0]!["content"] = prompt.TrimEnd() + "\nResponse schema:\n" + body["format"]!.ToJsonString();
            if (!JsonNode.DeepEquals(body, reference)) throw new InvalidDataException("Retained messages/schema changed");
            var sizing = Evaluation.Sizing(body, config);
            if (sizing["withinConservativeBound"]?.GetValue<bool>() != true) throw new InvalidDataException("Input too large; never truncate");
            var file = Path.Combine(directory, id, caseId + ".request.json"); File.WriteAllText(file, body.ToJsonString(Evaluation.Pretty) + "\n", new UTF8Encoding(false));
            requests.Add(new JsonObject { ["caseId"] = caseId, ["requestFileSha256"] = Evaluation.Hash(File.ReadAllBytes(file)), ["sizing"] = sizing });
        }
        prepared.Add(new JsonObject { ["profileId"] = id, ["configuration"] = config.DeepClone(), ["requests"] = requests });
    }
    File.WriteAllText(Path.Combine(directory, "development-inputs.json"), fixtures.ToJsonString(Evaluation.Pretty) + "\n");
    File.WriteAllText(Path.Combine(directory, "manifest.json"), new JsonObject
    {
        ["fingerprint"] = fingerprint, ["profiles"] = prepared, ["maximumRequests"] = 16,
        ["maximumGeneratedTokens"] = 16 * 8192, ["combinedProfileCeilingSeconds"] = 9660,
        ["status"] = "Offline preparation only; no live authorization; previous approvals consumed; acceptance reserved", ["promptPreparation"] = ComparisonProfile.PromptPreparation
    }.ToJsonString(Evaluation.Pretty) + "\n", new UTF8Encoding(false));
    Console.WriteLine("Prepared all 16 development requests on unchanged successful reference: " + directory); return 0;
}
catch (Exception ex) when (ex is not OutOfMemoryException) { Console.Error.WriteLine("Stopped: " + ex.Message); return 1; }
