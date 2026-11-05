using System.Text.Json;

namespace OncoMatch.Api.Features.Studies;

public record StudySummary(string NctId, string? Title, string? RecruitmentStatus, string? StudyType,
    string[]? Conditions);
public record StudyDetails(string NctId, string? Title, string? RecruitmentStatus, string? StudyType,
    string[]? Conditions, string? BriefSummary, string? MinimumAge, string? MaximumAge, string? Sex,
    string RegistryUrl);
public record StoredStudy(StudyDetails Details, JsonElement Source);
public record Rejection(int RecordNumber, string? NctId, string Code, string Reason);
public record ImportAttempt(string Id, DateTimeOffset StartedAt, DateTimeOffset? FinishedAt, string Status,
    string? RetryOfAttemptId, int? FetchedCount, int? ImportedCount, int? DuplicateCount, int? RejectedCount,
    string[] ImportedNctIds, string[] DuplicateNctIds, Rejection[] Rejections, string? FailureCode);
public record ImportState(string Version, bool IsImporting, bool IsExhausted, string NextAction,
    int TotalImportedStudyCount, ImportAttempt[] Attempts);

// All state is guarded by one short-lived lock. Network I/O never holds it.
public class StudyStore
{
    private readonly object sync = new();
    private readonly List<StoredStudy> studies = [];
    private readonly List<ImportAttempt> attempts = [];
    private string version = Guid.NewGuid().ToString("N");
    private bool exhausted;
    private bool importing;
    private string? cursor;
    private ActiveImport? active;

    public ActiveImport Admit(string expectedVersion, string session, DateTimeOffset now)
    {
        lock (sync)
        {
            if (importing) throw new ImportConflict("import_in_progress");
            if (version != expectedVersion) throw new ImportConflict("import_state_changed");
            if (exhausted) throw new ImportConflict("imports_exhausted");
            var retry = attempts.LastOrDefault();
            var attempt = new ImportAttempt(Guid.NewGuid().ToString("N"), now, null, "running",
                retry?.Status is "failed" or "cancelled" ? retry.Id : null, null, null, null, null, [], [], [], null);
            attempts.Add(attempt);
            importing = true;
            version = Guid.NewGuid().ToString("N");
            active = new(attempt.Id, session, cursor, studies.Select(s => s.Details.NctId).ToHashSet(StringComparer.Ordinal), new());
            return active;
        }
    }

    public ImportState Complete(RegistryPage page, DateTimeOffset now, CancellationToken token)
    {
        lock (sync)
        {
            token.ThrowIfCancellationRequested();
            studies.AddRange(page.Studies);
            cursor = page.NextToken;
            exhausted = cursor is null;
            attempts[^1] = attempts[^1] with { FinishedAt = now, Status = "completed", FetchedCount = page.FetchedCount,
                ImportedCount = page.Studies.Length, DuplicateCount = page.Duplicates.Length, RejectedCount = page.Rejections.Length,
                ImportedNctIds = page.Studies.Select(s => s.Details.NctId).ToArray(), DuplicateNctIds = page.Duplicates, Rejections = page.Rejections };
            Release();
            return State();
        }
    }

    public void Fail(string status, string? code, DateTimeOffset now)
    {
        lock (sync)
        {
            attempts[^1] = attempts[^1] with { FinishedAt = now, Status = status, FailureCode = code };
            Release();
        }
    }

    public async Task CancelSession(string? session)
    {
        if (string.IsNullOrEmpty(session)) return;
        CancellationTokenSource? cancellation;
        lock (sync) cancellation = active?.Session == session ? active?.Cancellation : null;
        if (cancellation is not null)
        {
            try { await cancellation.CancelAsync(); }
            catch (ObjectDisposedException) { /* The operation already finished. */ }
        }
    }

    private void Release()
    {
        active = null;
        importing = false;
        version = Guid.NewGuid().ToString("N");
    }

    public ImportState State()
    {
        lock (sync)
            return new(version, importing, exhausted, importing || exhausted ? "none" :
                attempts.Count == 0 ? "initial" : attempts[^1].Status is "failed" or "cancelled" ? "retry" : "next",
                studies.Count, attempts.AsEnumerable().Reverse().ToArray());
    }

    public StudySummary[] List()
    {
        lock (sync)
            return studies.Select(s => new StudySummary(s.Details.NctId, s.Details.Title,
                s.Details.RecruitmentStatus, s.Details.StudyType, s.Details.Conditions)).ToArray();
    }

    public StudyDetails? Find(string nctId)
    {
        lock (sync) return studies.FirstOrDefault(s => s.Details.NctId == nctId)?.Details;
    }

    public JsonElement Source(string nctId)
    {
        lock (sync) return studies.Single(s => s.Details.NctId == nctId).Source.Clone();
    }
}

public record ActiveImport(string Id, string Session, string? Cursor, HashSet<string> Known,
    CancellationTokenSource Cancellation);
public class ImportConflict(string code) : Exception { public string Code { get; } = code; }
