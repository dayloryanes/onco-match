namespace OncoMatch.Api.Features.Auth;

// Tracks only active work. Cookie authentication remains the request authority.
public sealed class OperationLifetime(TimeProvider clock)
{
    private readonly object sync = new();
    private readonly Dictionary<string, OperationLease> active = [];
    private readonly Dictionary<string, HashSet<OperationLease>> pending = [];
    private static readonly object PreparedKey = new();

    public void Prepare(HttpContext context, string session, DateTimeOffset expiry)
    {
        var path = context.Request.Path.Value?.TrimEnd('/');
        if (context.Request.Method != "POST" || path is null || !path.StartsWith("/api/match/batches/", StringComparison.OrdinalIgnoreCase)
            || !path.EndsWith("/operations", StringComparison.OrdinalIgnoreCase) || context.Items.ContainsKey(PreparedKey)) return;
        lock (sync)
        {
            var lease = new OperationLease(clock, expiry);
            if (!pending.TryGetValue(session, out var leases)) pending[session] = leases = [];
            leases.Add(lease);
            context.Items[PreparedKey] = (session, lease);
        }
    }

    public void EndRequest(HttpContext context)
    {
        lock (sync)
        {
            if (context.Items[PreparedKey] is not ValueTuple<string, OperationLease> prepared) return;
            if (pending.TryGetValue(prepared.Item1, out var leases) && leases.Remove(prepared.Item2))
            {
                if (leases.Count == 0) pending.Remove(prepared.Item1);
                prepared.Item2.Dispose();
            }
        }
    }

    public void PublishCookie(HttpContext context, Action publish)
    {
        lock (sync)
        {
            if (context.Items[PreparedKey] is ValueTuple<string, OperationLease> prepared)
                prepared.Item2.Publish(publish);
            else publish();
        }
    }

    public OperationLease Register(HttpContext context, string session)
    {
        lock (sync)
        {
            if (context.Items[PreparedKey] is not ValueTuple<string, OperationLease> prepared || prepared.Item1 != session)
                throw new OperationCanceledException("The initiating request was not prepared.");
            var lease = prepared.Item2;
            lease.Publish(() => { });
            if (pending.TryGetValue(session, out var leases))
            {
                leases.Remove(lease);
                if (leases.Count == 0) pending.Remove(session);
            }
            active.Add(session, lease);
            return lease;
        }
    }

    public void Renew(string session, DateTimeOffset expiry)
    {
        lock (sync)
        {
            if (active.TryGetValue(session, out var lease)) lease.Renew(expiry);
            if (pending.TryGetValue(session, out var leases)) foreach (var prepared in leases) prepared.Renew(expiry);
        }
    }

    public bool ResponseAllowed(HttpContext context)
    {
        lock (sync)
        {
            if (context.Items[PreparedKey] is not ValueTuple<string, OperationLease> prepared) return true;
            try { prepared.Item2.Publish(() => { }); return true; }
            catch (OperationCanceledException) { return false; }
        }
    }

    public void Logout(string? session)
    {
        lock (sync)
        {
            if (session is null) return;
            if (active.TryGetValue(session, out var lease)) lease.Stop("logout");
            if (pending.TryGetValue(session, out var leases)) foreach (var prepared in leases) prepared.Stop("logout");
        }
    }

    public void Release(string session)
    {
        lock (sync) if (active.Remove(session, out var lease)) lease.Dispose();
    }
}

public sealed class OperationLease : IDisposable
{
    private readonly object sync = new();
    private readonly TimeProvider clock;
    private readonly CancellationTokenSource cancellation = new();
    private readonly ITimer timer;
    private DateTimeOffset expiry;
    public string? StopReason { get; private set; }
    public CancellationToken Token => cancellation.Token;

    public OperationLease(TimeProvider clock, DateTimeOffset expiry)
    {
        this.clock = clock;
        this.expiry = expiry;
        timer = clock.CreateTimer(_ => CheckExpiry(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        Renew(expiry);
    }

    public void Renew(DateTimeOffset value)
    {
        lock (sync)
        {
            CheckExpiry();
            if (StopReason is not null) return;
            if (value > expiry) expiry = value;
            timer.Change(expiry - clock.GetUtcNow(), Timeout.InfiniteTimeSpan);
        }
    }

    private void CheckExpiry()
    {
        lock (sync) if (clock.GetUtcNow() >= expiry) Stop("sessionExpired");
    }

    public void Stop(string reason)
    {
        lock (sync)
        {
            if (StopReason is not null) return;
            StopReason = reason;
            // The flag is set under the publication lock; callbacks run outside it.
            _ = cancellation.CancelAsync();
        }
    }

    public void Publish(Action action)
    {
        lock (sync)
        {
            CheckExpiry();
            if (StopReason is not null) throw new OperationCanceledException(Token);
            action();
        }
    }

    public void Dispose() { timer.Dispose(); cancellation.Dispose(); }
}
