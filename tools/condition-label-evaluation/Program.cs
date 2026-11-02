using System.Text.Json.Nodes;

try
{
    var repository = new DirectoryInfo(Environment.CurrentDirectory);
    while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "docs/planning/condition-label-evaluation-design.md"))) repository = repository.Parent;
    var root = Path.Combine(repository?.FullName ?? throw new InvalidDataException("Run inside repository"), "tools/condition-label-evaluation");
    var fixtures = ComparisonProfile.Fixtures(root); var prompt = ComparisonProfile.Prompt(root); var profiles = ComparisonProfile.Profiles(root);
    if (args.Length == 3 && args[0] is "run" or "smoke")
    {
        var candidates = profiles.Concat(ComparisonProfile.Profiles(root, "openai-compatible"));
        var config = candidates.Single(p => Evaluation.Text(p!["profileId"]) == args[1])!;
        var smoke = args[0] == "smoke";
        await ComparisonPilot.RunAsync(root, fixtures, prompt, config, args[2], liveSmoke: smoke);
        if (smoke)
        {
            var approval = Evaluation.Read(args[2]);
            var checkpoint = Evaluation.Read(Path.Combine(root, "results", Evaluation.Text(approval["runId"]), "checkpoint.json"));
            if (checkpoint["completed"]?.GetValue<int>() != 1 || checkpoint["failed"]?.GetValue<int>() != 0 || checkpoint["interrupted"]?.GetValue<int>() != 0)
                throw new InvalidDataException("Smoke failed or interrupted; no retry authorized");
        }
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
    if (args.Length is < 1 or > 2 || args[0] != "prepare")
        throw new InvalidDataException("Use prepare [native|openai-compatible], self-check, run <profile> <approval> or smoke <profile> <approval>");
    var route = args.Length == 2 ? args[1] : "native";
    directory = ComparisonProfile.Prepare(root, fixtures, prompt, route);
    Console.WriteLine($"Prepared 16 {route} development requests offline; actual model/server not verified: {directory}");
    return 0;
}
catch (Exception ex) when (ex is not OutOfMemoryException) { Console.Error.WriteLine("Stopped: " + ex.Message); return 1; }
