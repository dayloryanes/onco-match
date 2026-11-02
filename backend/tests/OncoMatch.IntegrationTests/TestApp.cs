using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace OncoMatch.IntegrationTests;

public class TestApp : WebApplicationFactory<Program>
{
    public TestClock Clock { get; } = new();
    public RegistryHandler Registry { get; } = new();
    public ResponseBarrier? ImportResponse { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<TimeProvider>(Clock);
            services.AddHttpClient("Registry").ConfigurePrimaryHttpMessageHandler(() => Registry);
            services.AddSingleton<IStartupFilter>(new ImportResponseFilter(() => ImportResponse));
        });
    }

    public HttpClient Browser() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false
    });
}

public class ResponseBarrier
{
    public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}

// Hold only the outbound application response, after routing/controller work has
// finished. Tests observe the publication point through normal API reads.
public class ImportResponseFilter(Func<ResponseBarrier?> barrier) : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, proceed) =>
        {
            var hold = barrier();
            if (hold is null || context.Request.Method != "POST" || context.Request.Path != "/api/study-imports")
            { await proceed(); return; }
            var original = context.Response.Body;
            await using var buffer = new MemoryStream();
            context.Response.Body = buffer;
            try
            {
                await proceed();
                hold.Reached.TrySetResult();
                await hold.Release.Task;
                buffer.Position = 0;
                await buffer.CopyToAsync(original, context.RequestAborted);
            }
            finally { context.Response.Body = original; }
        });
        next(app);
    };
}

public class RegistryHandler : HttpMessageHandler
{
    public List<Uri> Requests { get; } = [];
    public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Respond { get; set; } =
        (_, _) => throw new InvalidOperationException("No registry response configured");
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        Requests.Add(request.RequestUri!);
        return Respond(request, token);
    }
}

public class TestClock : TimeProvider
{
    private readonly object sync = new();
    private readonly List<ClockTimer> timers = [];
    private DateTimeOffset now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() { lock (sync) return now; }
    public void Advance(TimeSpan interval)
    {
        ClockTimer[] due;
        lock (sync)
        {
            now += interval;
            due = timers.Where(t => t.Due <= now).ToArray();
            foreach (var timer in due) timer.Due = timer.Period == Timeout.InfiniteTimeSpan ? DateTimeOffset.MaxValue : now + timer.Period;
        }
        foreach (var timer in due) timer.Fire();
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ClockTimer(this, callback, state);
        lock (sync) { timers.Add(timer); timer.Change(dueTime, period); }
        return timer;
    }

    private sealed class ClockTimer(TestClock clock, TimerCallback callback, object? state) : ITimer
    {
        public DateTimeOffset Due { get; set; } = DateTimeOffset.MaxValue;
        public TimeSpan Period { get; private set; }
        private bool disposed;
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (clock.sync)
            {
                if (disposed) return false;
                Due = dueTime == Timeout.InfiniteTimeSpan ? DateTimeOffset.MaxValue : clock.now + dueTime;
                Period = period;
                return true;
            }
        }
        public void Fire() { if (!disposed) callback(state); }
        public void Dispose() { lock (clock.sync) { disposed = true; clock.timers.Remove(this); } }
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
