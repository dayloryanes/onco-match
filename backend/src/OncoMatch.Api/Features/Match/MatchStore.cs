using System.Text.Json.Nodes;
using OncoMatch.Api.Features.Auth;
using OncoMatch.Api.Features.Match.Inference;
using OncoMatch.Api.Features.Patients;
using OncoMatch.Api.Features.Studies;

namespace OncoMatch.Api.Features.Match;

public sealed class MatchStore(StudyStore studies, PatientCatalog patients, MatchInferenceOptions options, TimeProvider clock)
{
    private readonly object sync = new();
    private readonly Dictionary<string, MatchBatch> batches = [];
    private readonly Dictionary<string, ActiveMatch> operations = [];
    private ActiveMatch? active;
    public const string Limits = "This assessment checks structured age and sex requirements and compares cancer type, stage, and disease state with the study's listed conditions. Biomarkers, treatment history, other requirements, and free-text eligibility criteria are not assessed. Results do not establish clinical eligibility or enrollment approval.";

    private void Discover()
    {
        foreach (var import in studies.State().Attempts.Where(a => a.Status == "completed").Reverse())
        {
            if (batches.ContainsKey(import.Id)) continue;
            var pairs = import.ImportedNctIds.SelectMany(id => patients.Patients.Select(patient =>
                new MatchPair(import.Id, patient, studies.Find(id)!, studies.Source(id)))).ToArray();
            batches.Add(import.Id, new MatchBatch(import.Id, pairs));
        }
    }

    public object Batches(string? session, bool isOperator)
    {
        lock (sync)
        {
            Discover();
            return new { configurationError = options.ValidationError, activeOperation = ActiveView(active, session),
                batches = batches.Values.Select(b => View(b, session, isOperator, false)).ToArray() };
        }
    }
    public object Batch(string id, string? session, bool isOperator)
    {
        lock (sync) { Discover(); return View(Find(id), session, isOperator, true); }
    }
    public object Pair(string patientId, string nctId)
    {
        lock (sync)
        {
            Discover();
            var pair = batches.Values.SelectMany(b => b.Pairs).FirstOrDefault(p => p.Patient.Id == patientId && p.Study.NctId == nctId)
                ?? throw new MatchConflict(404, "pair_not_found");
            return PairView(pair, true);
        }
    }
    public object Pairs(string filter, string id)
    {
        lock (sync)
        {
            if (filter == "patientId" && !patients.Patients.Any(p => p.Id == id))
                throw new MatchConflict(404, "patient_not_found");
            if (filter == "nctId" && studies.Find(id) is null)
                throw new MatchConflict(404, "study_not_found");
            Discover();
            return batches.Values.SelectMany(b => b.Pairs)
                .Where(p => filter == "patientId" ? p.Patient.Id == id : p.Study.NctId == id)
                .Select(p => PairView(p, false)).ToArray();
        }
    }
    private MatchBatch Find(string id) => batches.GetValueOrDefault(id) ?? throw new MatchConflict(404, "batch_not_found");

    public ActiveMatch Admit(string id, string action, string version, string session, OperationLifetime lifetime, HttpContext context)
    {
        lock (sync)
        {
            Discover();
            var batch = Find(id);
            if (options.ValidationError is { } error) throw new MatchConflict(503, "inference_configuration_invalid", error);
            if (active is not null) throw new MatchConflict(409, "operation_in_progress");
            if (batch.Version != version) throw new MatchConflict(409, "batch_state_changed");
            var selected = SelectPairs(batch, action);
            if (selected.Length == 0) throw new MatchConflict(409, "no_work_for_action");
            OperationLease lease;
            try { lease = lifetime.Register(context, session); }
            catch (OperationCanceledException) { throw new MatchConflict(401, "session_stopped", "Sign in again before starting match work."); }
            var operation = new ActiveMatch(Guid.NewGuid().ToString("N"), batch, action, selected, session, clock.GetUtcNow(), lease);
            operations.Add(operation.Id, operation);
            active = operation;
            batch.Status = "running";
            Change(batch);
            return operation;
        }
    }
    public int Cancel(string id, string session)
    {
        lock (sync)
        {
            if (!operations.TryGetValue(id, out var operation)) throw new MatchConflict(404, "operation_not_found");
            if (operation.Session != session) throw new MatchConflict(403, "access_denied");
            if (active != operation || operation.Finished) return 204;
            operation.Lease.Stop("cancelled");
            operation.Batch.Status = "stopping";
            Change(operation.Batch);
            return 202;
        }
    }
    public void BeginPair(ActiveMatch operation, MatchPair pair)
    {
        lock (sync) operation.Lease.Publish(() =>
        {
            pair.State = "running";
            pair.Attempts.Add(new(Guid.NewGuid().ToString("N"), operation.Id, clock.GetUtcNow(), null,
                "running", null, null, options.Route, options.Model, options.RequestTimeoutSeconds, null));
            pair.StructuredChecks = StructuredChecks.Assess(pair.Patient, pair.Study);
            Change(operation.Batch);
        });
    }
    public void Complete(ActiveMatch operation, MatchPair pair, string summary, JsonArray labels, string? skip)
    {
        lock (sync) operation.Lease.Publish(() =>
        {
            pair.State = "completed"; pair.PairSummary = summary; pair.ConditionLabels = labels; pair.SkipReason = skip;
            FinishAttempt(pair, "completed", null, null);
            Change(operation.Batch);
        });
    }
    public void Fail(ActiveMatch operation, MatchPair pair, string code, string explanation)
    {
        lock (sync) operation.Lease.Publish(() =>
        {
            pair.State = "failed"; FinishAttempt(pair, "failed", code, explanation); Change(operation.Batch);
        });
    }
    public void Finish(ActiveMatch operation, string? reason)
    {
        lock (sync)
        {
            foreach (var pair in operation.Batch.Pairs.Where(p => p.State == "running"))
            {
                pair.State = "unfinished"; FinishAttempt(pair, "interrupted", null, reason);
            }
            operation.StopReason = reason;
            operation.Finished = true;
            operation.Batch.Status = operation.Batch.Pairs.Any(p => p.State == "unfinished") ? "unfinished"
                : operation.Batch.Pairs.Any(p => p.State == "failed") ? "finishedWithFailures" : "completed";
            Change(operation.Batch);
        }
    }
    public void Release(ActiveMatch operation, OperationLifetime lifetime)
    {
        lock (sync)
        {
            lifetime.Release(operation.Session);
            if (active == operation) active = null;
        }
    }
    public object Message(ActiveMatch operation, string type, MatchPair? pair = null, string? reason = null)
    {
        lock (sync)
        {
            var batch = operation.Batch;
            return new { type, operationId = operation.Id, batchId = batch.Id, sequence = ++operation.Sequence,
                serverTime = clock.GetUtcNow(), version = batch.Version, totalPairCount = batch.Pairs.Length,
                completedPairCount = batch.Pairs.Count(p => p.State == "completed"), failedPairCount = batch.Pairs.Count(p => p.State == "failed"),
                unfinishedPairCount = batch.Pairs.Count(p => p.State == "unfinished"), activePairCount = batch.Pairs.Count(p => p.State == "running"),
                patientId = pair?.Patient.Id, nctId = pair?.Study.NctId, pairSummary = pair?.PairSummary,
                structuredChecks = pair?.StructuredChecks, failureCode = pair?.Attempts.LastOrDefault()?.FailureCode,
                failureExplanation = pair?.Attempts.LastOrDefault()?.FailureExplanation, stopReason = reason, status = batch.Status };
        }
    }
    private object View(MatchBatch batch, string? session, bool isOperator, bool includePairs) => new
    {
        batchId = batch.Id, version = batch.Version, status = batch.Status,
        totalPairCount = batch.Pairs.Length, completedPairCount = batch.Pairs.Count(p => p.State == "completed"),
        failedPairCount = batch.Pairs.Count(p => p.State == "failed"), unfinishedPairCount = batch.Pairs.Count(p => p.State == "unfinished"),
        activePairCount = batch.Pairs.Count(p => p.State == "running"), activeOperation = ActiveView(active?.Batch == batch ? active : null, session),
        availableActions = isOperator && active is null && options.ValidationError is null
            ? new[] { "start", "continue", "retryFailed" }.Where(action => SelectPairs(batch, action).Length > 0).ToArray() : [],
        pairs = includePairs ? batch.Pairs.Select(p => PairView(p, false)).ToArray() : null
    };
    private static object? ActiveView(ActiveMatch? operation, string? session) => operation is null ? null : new
    { operationId = operation.Id, action = operation.Action, startedAt = operation.StartedAt, selectedPairCount = operation.SelectedPairs.Length, canCancel = session == operation.Session };
    private static MatchPair[] SelectPairs(MatchBatch batch, string action) => action switch
    {
        "start" when batch.Status == "notStarted" => batch.Pairs.ToArray(),
        "continue" when batch.Status is not ("notStarted" or "noWork") => batch.Pairs.Where(pair => pair.State == "unfinished").ToArray(),
        "retryFailed" when batch.Status is not ("notStarted" or "noWork") => batch.Pairs.Where(pair => pair.State == "failed").ToArray(),
        _ => []
    };
    private static object PairView(MatchPair pair, bool detailed) => new
    {
        batchId = pair.BatchId, patientId = pair.Patient.Id, nctId = pair.Study.NctId, state = pair.State,
        pairSummary = pair.PairSummary, structuredChecks = pair.StructuredChecks,
        latestAttempt = pair.Attempts.LastOrDefault(), attemptHistory = detailed ? pair.Attempts.ToArray() : null,
        inputSnapshot = detailed ? new { patient = pair.Patient, study = pair.Study, registrySource = pair.RegistrySource } : null,
        conditionLabels = detailed ? pair.ConditionLabels?.DeepClone() : null,
        skipReason = detailed ? pair.SkipReason : null, assessmentLimits = detailed ? Limits : null
    };
    private void FinishAttempt(MatchPair pair, string status, string? code, string? explanation)
    {
        var previous = pair.Attempts[^1]; var now = clock.GetUtcNow();
        pair.Attempts[^1] = previous with { FinishedAt = now, Status = status, FailureCode = code,
            FailureExplanation = explanation, ElapsedMilliseconds = (now - previous.StartedAt).TotalMilliseconds };
    }
    private static void Change(MatchBatch batch) => batch.Version = Guid.NewGuid().ToString("N");
}

public sealed class MatchBatch(string id, MatchPair[] pairs)
{
    public string Id { get; } = id;
    public string Version { get; set; } = Guid.NewGuid().ToString("N");
    public string Status { get; set; } = pairs.Length == 0 ? "noWork" : "notStarted";
    public MatchPair[] Pairs { get; } = pairs;
}
public sealed class MatchPair(string batchId, PatientDetails patient, StudyDetails study, System.Text.Json.JsonElement source)
{
    public string BatchId { get; } = batchId;
    public PatientDetails Patient { get; } = patient;
    public StudyDetails Study { get; } = study;
    public System.Text.Json.JsonElement RegistrySource { get; } = source;
    public string State { get; set; } = "unfinished";
    public string? PairSummary { get; set; }
    public StructuredCheckResult? StructuredChecks { get; set; }
    public JsonArray? ConditionLabels { get; set; }
    public string? SkipReason { get; set; }
    public List<MatchAttempt> Attempts { get; } = [];
}
public record MatchAttempt(string Id, string OperationId, DateTimeOffset StartedAt, DateTimeOffset? FinishedAt,
    string Status, string? FailureCode, string? FailureExplanation, string Route, string Model, int RequestTimeoutSeconds, double? ElapsedMilliseconds);
public sealed class ActiveMatch(string id, MatchBatch batch, string action, MatchPair[] selectedPairs, string session, DateTimeOffset startedAt, OperationLease lease)
{
    public string Id { get; } = id;
    public MatchBatch Batch { get; } = batch;
    public string Action { get; } = action;
    public MatchPair[] SelectedPairs { get; } = selectedPairs;
    public string Session { get; } = session;
    public DateTimeOffset StartedAt { get; } = startedAt;
    public OperationLease Lease { get; } = lease;
    public long Sequence { get; set; }
    public string? StopReason { get; set; }
    public bool Finished { get; set; }
}
public sealed class MatchConflict(int status, string code, string? message = null) : Exception(message ?? code)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}
