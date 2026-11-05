using OncoMatch.Api.Features.Match.Inference;

namespace OncoMatch.Api.Features.Match;

public sealed class MatchExecution(MatchStore store, OllamaClient ollama, TimeProvider clock, ILogger<MatchExecution> logger)
{
    public async Task Run(ActiveMatch operation, Func<object, Task> emit)
    {
        string? reason = null;
        var available = false;
        try
        {
            await emit(store.Message(operation, "operationStarted"));
            foreach (var pair in operation.Batch.Pairs)
            {
                store.BeginPair(operation, pair);
                await emit(store.Message(operation, "pairStarted", pair));
                if (pair.StructuredChecks!.Excludes)
                    store.Complete(operation, pair, "excludedByAgeSex", [], "Reliable structured age/sex exclusion; inference skipped.");
                else if (!(pair.Study.Conditions ?? []).Any(c => !string.IsNullOrWhiteSpace(c)))
                    store.Complete(operation, pair, "conditionLabelAlignmentUndetermined", [], "No condition text supplied; inference skipped.");
                else
                {
                    try
                    {
                        if (!available)
                        {
                            await WithHeartbeats(ollama.CheckAvailability(operation.Lease.Token), operation, emit);
                            available = true;
                        }
                        var labels = await WithHeartbeats(ollama.Assess(pair, operation.Lease.Token), operation, emit);
                        store.Complete(operation, pair, ConditionLabelContract.Aggregate(labels.Select(label => label!["result"]!.GetValue<string>())), labels, null);
                    }
                    catch (InferenceFailure error)
                    {
                        if (error.Code == "inferenceUnavailable") { reason = error.Code; break; }
                        store.Fail(operation, pair, error.Code, error.Message);
                        await emit(store.Message(operation, "pairFailed", pair));
                        continue;
                    }
                }
                await emit(store.Message(operation, "pairCompleted", pair));
            }
        }
        catch (OperationCanceledException) { reason = operation.Lease.StopReason ?? "disconnected"; }
        catch (Exception error)
        {
            reason = "internalError";
            operation.Lease.Stop(reason);
            logger.LogError("Match operation {OperationId} stopped: {ErrorType}", operation.Id, error.GetType().Name);
        }
        finally { store.Finish(operation, reason); }
        await emit(store.Message(operation, reason is null ? "operationFinished" : "operationStopped", reason: reason));
    }

    private async Task WithHeartbeats(Task pending, ActiveMatch operation, Func<object, Task> emit)
    {
        using var heartbeatCancellation = CancellationTokenSource.CreateLinkedTokenSource(operation.Lease.Token);
        try
        {
            while (!pending.IsCompleted)
            {
                var heartbeat = Task.Delay(TimeSpan.FromSeconds(15), clock, heartbeatCancellation.Token);
                if (await Task.WhenAny(pending, heartbeat) == pending) break;
                await heartbeat;
                await emit(store.Message(operation, "heartbeat"));
            }
        }
        catch
        {
            operation.Lease.Stop(operation.Lease.StopReason ?? "disconnected");
            try { await pending; } catch { /* Observe cleanup before releasing admission. */ }
            throw;
        }
        finally { await heartbeatCancellation.CancelAsync(); }
        await pending;
    }
    private async Task<T> WithHeartbeats<T>(Task<T> pending, ActiveMatch operation, Func<object, Task> emit)
    {
        await WithHeartbeats((Task)pending, operation, emit);
        return await pending;
    }
    public void FinishIfNeeded(ActiveMatch operation)
    {
        if (operation.Batch.Status is "running" or "stopping") store.Finish(operation, operation.Lease.StopReason ?? "disconnected");
    }
}
