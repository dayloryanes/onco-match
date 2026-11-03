using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OncoMatch.IntegrationTests;

public class MatchTests
{
    [Fact]
    public async Task Logout_before_stream_headers_suppresses_the_already_queued_renewal_cookie()
    {
        await using var app = new TestApp();
        using var client = app.Browser();
        await AuthTests.Login(client, "operator", "OperatorDemo1!");
        StudyTests.Page(app, StudyTests.Record("NCT00000001"));
        (await StudyTests.Import(client, StudyTests.Version(await StudyTests.State(client)))).EnsureSuccessStatusCode();
        var batch = (await client.GetFromJsonAsync<JsonElement>("/api/match/batches")).GetProperty("batches")[0];
        var barrier = new ResponseBarrier(); app.MatchResponseStart = barrier;
        var pending = AuthTests.PostWithToken(client, $"/api/match/batches/{batch.GetProperty("batchId").GetString()}/operations", new { action = "start", expectedVersion = batch.GetProperty("version").GetString() });
        await barrier.Reached.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try { (await AuthTests.PostWithToken(client, "/api/auth/logout")).EnsureSuccessStatusCode(); }
        finally { barrier.Release.TrySetResult(); }
        using var response = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(response.Headers.TryGetValues("Set-Cookie", out _));
        Assert.False(response.Headers.TryGetValues("X-Session-Renewed", out _));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Empty(app.Ollama.Requests);
    }

    [Fact]
    public async Task Cancellation_preserves_committed_assessments_and_holds_admission_until_inference_unwinds()
    {
        await using var app = new TestApp();
        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;
        app.Ollama.Respond = async (request, token) =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/show") return Show();
            var answer = ValidAnswer(JsonNode.Parse(await request.Content!.ReadAsStringAsync(token))!);
            if (++count == 2) { held.TrySetResult(); await release.Task; }
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { done = true, done_reason = "stop", message = new { role = "assistant", content = answer } }) };
        };
        using var client = app.Browser();
        await AuthTests.Login(client, "operator", "OperatorDemo1!");
        StudyTests.Page(app, StudyTests.Record("NCT00000001"));
        (await StudyTests.Import(client, StudyTests.Version(await StudyTests.State(client)))).EnsureSuccessStatusCode();
        var batch = (await client.GetFromJsonAsync<JsonElement>("/api/match/batches")).GetProperty("batches")[0];
        var id = batch.GetProperty("batchId").GetString();
        var pending = AuthTests.PostWithToken(client, $"/api/match/batches/{id}/operations", new { action = "start", expectedVersion = batch.GetProperty("version").GetString() });
        await held.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var active = await client.GetFromJsonAsync<JsonElement>($"/api/match/batches/{id}");
        Assert.Equal(1, active.GetProperty("completedPairCount").GetInt32());
        try
        {
            Assert.Equal(HttpStatusCode.Accepted, (await AuthTests.PostWithToken(client, $"/api/match/operations/{active.GetProperty("activeOperation").GetProperty("operationId").GetString()}/cancel")).StatusCode);
            var stopping = await client.GetFromJsonAsync<JsonElement>($"/api/match/batches/{id}");
            Assert.Equal("stopping", stopping.GetProperty("status").GetString());
            Assert.Equal(HttpStatusCode.Conflict, (await AuthTests.PostWithToken(client, $"/api/match/batches/{id}/operations", new { action = "start", expectedVersion = stopping.GetProperty("version").GetString() })).StatusCode);
        }
        finally { release.TrySetResult(); }
        using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Contains("operationStopped", await result.Content.ReadAsStringAsync());
        var saved = await client.GetFromJsonAsync<JsonElement>($"/api/match/batches/{id}");
        Assert.Equal(1, saved.GetProperty("completedPairCount").GetInt32());
        Assert.Equal(9, saved.GetProperty("unfinishedPairCount").GetInt32());
        Assert.Equal(0, saved.GetProperty("failedPairCount").GetInt32());
    }

    [Fact]
    public async Task Duplicate_only_batches_have_no_work_and_rejected_stale_starts_do_not_renew()
    {
        await using var app = new TestApp();
        using var client = app.Browser();
        await AuthTests.Login(client, "operator", "OperatorDemo1!");
        StudyTests.Page(app, StudyTests.Record("NCT00000001"), "next");
        (await StudyTests.Import(client, StudyTests.Version(await StudyTests.State(client)))).EnsureSuccessStatusCode();
        StudyTests.Page(app, StudyTests.Record("NCT00000001"));
        (await StudyTests.Import(client, StudyTests.Version(await StudyTests.State(client)))).EnsureSuccessStatusCode();
        var batches = (await client.GetFromJsonAsync<JsonElement>("/api/match/batches")).GetProperty("batches");
        Assert.Equal("noWork", batches[1].GetProperty("status").GetString());
        Assert.Empty(batches[1].GetProperty("availableActions").EnumerateArray());
        Assert.Equal(0, batches[1].GetProperty("totalPairCount").GetInt32());
        app.Clock.Advance(TimeSpan.FromMinutes(16));
        using var rejected = await AuthTests.PostWithToken(client, $"/api/match/batches/{batches[0].GetProperty("batchId").GetString()}/operations", new { action = "start", expectedVersion = "stale" });
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        Assert.False(rejected.Headers.TryGetValues("Set-Cookie", out _));
        Assert.Empty(app.Ollama.Requests);
    }

    [Fact]
    public async Task Logout_cancels_a_start_already_authenticated_but_not_yet_admitted()
    {
        await using var app = new TestApp();
        using var client = app.Browser();
        await AuthTests.Login(client, "operator", "OperatorDemo1!");
        StudyTests.Page(app, StudyTests.Record("NCT00000001"));
        (await StudyTests.Import(client, StudyTests.Version(await StudyTests.State(client)))).EnsureSuccessStatusCode();
        var batch = (await client.GetFromJsonAsync<JsonElement>("/api/match/batches")).GetProperty("batches")[0];
        var barrier = new ResponseBarrier(); app.MatchAdmission = barrier;
        var pending = AuthTests.PostWithToken(client, $"/api/match/batches/{batch.GetProperty("batchId").GetString()}/operations",
            new { action = "start", expectedVersion = batch.GetProperty("version").GetString() });
        await barrier.Reached.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try { (await AuthTests.PostWithToken(client, "/api/auth/logout")).EnsureSuccessStatusCode(); }
        finally { barrier.Release.TrySetResult(); }
        using var rejected = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        Assert.False(rejected.Headers.TryGetValues("Set-Cookie", out _));
        Assert.Empty(app.Ollama.Requests);
        await AuthTests.Login(client, "operator", "OperatorDemo1!");
        var saved = await client.GetFromJsonAsync<JsonElement>($"/api/match/batches/{batch.GetProperty("batchId").GetString()}");
        Assert.Equal("notStarted", saved.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Pending_admission_observes_a_same_session_renewal_before_its_original_deadline()
    {
        await using var app = new TestApp();
        using var client = app.Browser();
        await AuthTests.Login(client, "operator", "OperatorDemo1!");
        StudyTests.Page(app, StudyTests.Record("NCT00000001").Replace("[\" Cancer \",\"\",\" Cancer \"]", "[]"));
        (await StudyTests.Import(client, StudyTests.Version(await StudyTests.State(client)))).EnsureSuccessStatusCode();
        var batch = (await client.GetFromJsonAsync<JsonElement>("/api/match/batches")).GetProperty("batches")[0];
        var barrier = new ResponseBarrier(); app.MatchAdmission = barrier;
        var pending = AuthTests.PostWithToken(client, $"/api/match/batches/{batch.GetProperty("batchId").GetString()}/operations", new { action = "start", expectedVersion = batch.GetProperty("version").GetString() });
        await barrier.Reached.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            app.Clock.Advance(TimeSpan.FromMinutes(16));
            (await AuthTests.PostWithToken(client, "/api/auth/extend-session")).EnsureSuccessStatusCode();
            app.Clock.Advance(TimeSpan.FromMinutes(15));
        }
        finally { barrier.Release.TrySetResult(); }
        using var response = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("operationFinished", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("unavailable")]
    [InlineData("remote")]
    [InlineData("oversized")]
    public async Task Unavailable_or_unsupported_inference_and_oversized_inputs_never_generate_an_answer(string scenario)
    {
        await using var app = new TestApp();
        app.Ollama.Respond = (request, _) =>
        {
            Assert.Equal("/api/show", request.RequestUri!.AbsolutePath);
            return Task.FromResult(scenario == "unavailable" ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : scenario == "remote" ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"remote_host\":\"https://example.com\"}") } : Show());
        };
        using var client = app.Browser();
        await AuthTests.Login(client, "operator", "OperatorDemo1!");
        var record = StudyTests.Record("NCT00000001");
        if (scenario == "oversized") record = record.Replace(" Cancer ", new string('x', 30000));
        StudyTests.Page(app, record);
        (await StudyTests.Import(client, StudyTests.Version(await StudyTests.State(client)))).EnsureSuccessStatusCode();
        var batch = (await client.GetFromJsonAsync<JsonElement>("/api/match/batches")).GetProperty("batches")[0];
        using var result = await AuthTests.PostWithToken(client, $"/api/match/batches/{batch.GetProperty("batchId").GetString()}/operations",
            new { action = "start", expectedVersion = batch.GetProperty("version").GetString() });
        var text = await result.Content.ReadAsStringAsync();
        Assert.Contains(scenario == "oversized" ? "inputTooLarge" : "inferenceUnavailable", text);
        Assert.Single(app.Ollama.Requests);
        var saved = await client.GetFromJsonAsync<JsonElement>($"/api/match/batches/{batch.GetProperty("batchId").GetString()}");
        Assert.Equal(0, saved.GetProperty("completedPairCount").GetInt32());
        Assert.Equal(scenario == "oversized" ? 10 : 0, saved.GetProperty("failedPairCount").GetInt32());
    }

    private static HttpResponseMessage Show() => new(HttpStatusCode.OK) { Content = JsonContent.Create(new
    {
        parameters = "num_ctx 24576\ntop_k 20\nmin_p 0\nrepeat_penalty 1",
        model_info = new Dictionary<string, string> { ["tokenizer.ggml.model"] = "gpt2", ["tokenizer.ggml.pre"] = "gpt-4o" },
        template_source = "harmony", thinking = new { values = new[] { "low", "medium", "high" } }
    }) };
    [Theory]
    [InlineData("invalidEvidence")]
    [InlineData("invalidResponse")]
    [InlineData("inferenceError")]
    [InlineData("inferenceTimeout")]
    public async Task A_failed_pair_preserves_successes_and_does_not_prevent_later_pairs(string failure)
    {
        await using var app = new TestApp();
        app.Inference.RequestTimeoutSeconds = 1;
        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var generated = 0;
        app.Ollama.Respond = async (request, token) =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/show") return Show();
            generated++;
            if (generated == 2 && failure == "inferenceError") return new(HttpStatusCode.ServiceUnavailable);
            if (generated == 2 && failure == "inferenceTimeout")
            {
                held.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            var answer = ValidAnswer(JsonNode.Parse(await request.Content!.ReadAsStringAsync(token))!);
            if (generated == 2 && failure == "invalidEvidence") answer = answer.Replace("\"quote\":\" Cancer \"", "\"quote\":\"Invented cancer\"");
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { done = true, done_reason = generated == 2 && failure == "invalidResponse" ? "length" : "stop", message = new { role = "assistant", content = answer } }) };
        };
        using var client = app.Browser();
        await AuthTests.Login(client, "operator", "OperatorDemo1!");
        StudyTests.Page(app, StudyTests.Record("NCT00000001"));
        (await StudyTests.Import(client, StudyTests.Version(await StudyTests.State(client)))).EnsureSuccessStatusCode();
        var batch = (await client.GetFromJsonAsync<JsonElement>("/api/match/batches")).GetProperty("batches")[0];
        var pending = AuthTests.PostWithToken(client, $"/api/match/batches/{batch.GetProperty("batchId").GetString()}/operations",
            new { action = "start", expectedVersion = batch.GetProperty("version").GetString() });
        if (failure == "inferenceTimeout") { await held.Task.WaitAsync(TimeSpan.FromSeconds(5)); app.Clock.Advance(TimeSpan.FromSeconds(2)); }
        using var response = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        var events = await response.Content.ReadAsStringAsync();
        Assert.Contains(failure, events);
        var saved = await client.GetFromJsonAsync<JsonElement>($"/api/match/batches/{batch.GetProperty("batchId").GetString()}");
        Assert.Equal("finishedWithFailures", saved.GetProperty("status").GetString());
        Assert.Equal(9, saved.GetProperty("completedPairCount").GetInt32());
        Assert.Equal(1, saved.GetProperty("failedPairCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, saved.GetProperty("pairs")[1].GetProperty("pairSummary").ValueKind);
        Assert.Equal(10, generated);
    }

    [Theory]
    [InlineData("Viewer", true, HttpStatusCode.Forbidden)]
    [InlineData("Operator", false, HttpStatusCode.BadRequest)]
    public async Task Start_enforces_role_and_csrf_before_admitting_work(string role, bool csrf, HttpStatusCode expected)
    {
        await using var app = new TestApp();
        using var client = app.Browser();
        await AuthTests.Login(client, role.ToLowerInvariant(), role + "Demo1!");
        var result = csrf ? await AuthTests.PostWithToken(client, "/api/match/batches/missing/operations", new { action = "start", expectedVersion = "v1" })
            : await client.PostAsJsonAsync("/api/match/batches/missing/operations", new { action = "start", expectedVersion = "v1" });
        Assert.Equal(expected, result.StatusCode);
        Assert.Empty(app.Ollama.Requests);
    }

    private static string ValidAnswer(JsonNode request)
    {
        var sources = JsonNode.Parse(request["messages"]![1]!["content"]!.GetValue<string>())!["sources"]!.AsObject();
        var labels = new JsonArray();
        foreach (var source in sources.Where(p => p.Key.StartsWith("study.conditions.", StringComparison.Ordinal)))
        {
            JsonObject Finding(bool cancer) => new()
            {
                ["result"] = cancer ? "alignmentSupported" : "notSpecified", ["explanation"] = "Controlled transport answer.",
                ["patientEvidence"] = cancer ? new JsonArray(new JsonObject { ["sourceId"] = "patient.cancerType", ["value"] = sources["patient.cancerType"]?.DeepClone() }) : new JsonArray(),
                ["studyEvidence"] = cancer ? new JsonArray(new JsonObject { ["sourceId"] = source.Key, ["quote"] = source.Value!.DeepClone() }) : new JsonArray(),
                ["examinedSources"] = new JsonArray(source.Key)
            };
            labels.Add(new JsonObject { ["conditionLabelId"] = source.Key, ["result"] = "alignmentSupported", ["requirementSummary"] = "Cancer",
                ["cancerType"] = Finding(true), ["stage"] = Finding(false), ["diseaseState"] = Finding(false), ["unassessedQualifiers"] = new JsonArray(), ["explanation"] = "Controlled transport answer." });
        }
        return new JsonObject { ["conditionLabels"] = labels }.ToJsonString();
    }

    [Theory]
    [InlineData("native")]
    [InlineData("openai-compatible")]
    public async Task Valid_answers_are_persisted_and_shared_and_start_renews_the_cookie(string route)
    {
        await using var app = new TestApp();
        app.Inference.Route = route;
        app.Ollama.Respond = async (request, token) =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/show") return Show();
            var answer = ValidAnswer(JsonNode.Parse(await request.Content!.ReadAsStringAsync(token))!);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(route == "native" ? (object)new
                { done = true, done_reason = "stop", message = new { role = "assistant", content = answer, thinking = "Must not be stored" } }
                : new { choices = new[] { new { finish_reason = "stop", message = new { role = "assistant", content = answer, reasoning = "Must not be stored" } } } }) };
        };
        using var client = app.Browser();
        await AuthTests.Login(client, "operator", "OperatorDemo1!");
        StudyTests.Page(app, StudyTests.Record("NCT00000001"));
        (await StudyTests.Import(client, StudyTests.Version(await StudyTests.State(client)))).EnsureSuccessStatusCode();
        var batch = (await client.GetFromJsonAsync<JsonElement>("/api/match/batches")).GetProperty("batches")[0];
        app.Clock.Advance(TimeSpan.FromMinutes(10));
        using var response = await AuthTests.PostWithToken(client, $"/api/match/batches/{batch.GetProperty("batchId").GetString()}/operations",
            new { action = "start", expectedVersion = batch.GetProperty("version").GetString() });
        Assert.Equal(app.Clock.GetUtcNow().AddMinutes(30), DateTimeOffset.Parse(Assert.Single(response.Headers.GetValues("X-Session-Expires-At"))));
        Assert.Contains("operationFinished", await response.Content.ReadAsStringAsync());
        using var viewer = app.Browser();
        await AuthTests.Login(viewer);
        var saved = await viewer.GetFromJsonAsync<JsonElement>($"/api/match/batches/{batch.GetProperty("batchId").GetString()}");
        Assert.Equal(10, saved.GetProperty("completedPairCount").GetInt32());
        Assert.Empty(saved.GetProperty("availableActions").EnumerateArray());
        var patientId = saved.GetProperty("pairs")[0].GetProperty("patientId").GetString();
        var detail = await viewer.GetFromJsonAsync<JsonElement>($"/api/match/pairs/{patientId}/NCT00000001");
        Assert.Equal("conditionLabelAlignmentSupported", detail.GetProperty("pairSummary").GetString());
        Assert.Equal(2, detail.GetProperty("conditionLabels").GetArrayLength());
        Assert.Equal("study.conditions.2", detail.GetProperty("conditionLabels")[1].GetProperty("conditionLabelId").GetString());
        Assert.DoesNotContain("Must not be stored", detail.GetRawText());
        Assert.Equal("completed", detail.GetProperty("attemptHistory")[0].GetProperty("status").GetString());
    }

    [Theory]
    [InlineData("expiry")]
    [InlineData("renewal")]
    [InlineData("logout")]
    [InlineData("cancel")]
    [InlineData("disconnect")]
    public async Task Active_work_obeys_session_lifetime_and_exclusive_admission(string stop)
    {
        await using var app = new TestApp();
        app.Inference.RequestTimeoutSeconds = 3600;
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        app.Ollama.Respond = async (request, token) =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/show") return Show();
            reached.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException();
        };
        using var client = app.Browser();
        using var other = app.Browser();
        await AuthTests.Login(client, "operator", "OperatorDemo1!");
        await AuthTests.Login(other, "operator", "OperatorDemo1!");
        StudyTests.Page(app, StudyTests.Record("NCT00000001"));
        (await StudyTests.Import(client, StudyTests.Version(await StudyTests.State(client)))).EnsureSuccessStatusCode();
        var batch = (await client.GetFromJsonAsync<JsonElement>("/api/match/batches")).GetProperty("batches")[0];
        var id = batch.GetProperty("batchId").GetString();
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/match/batches/{id}/operations");
        request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("requestToken").GetString());
        request.Content = JsonContent.Create(new { action = "start", expectedVersion = batch.GetProperty("version").GetString() });
        using var disconnect = new CancellationTokenSource();
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, disconnect.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await reached.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var active = await client.GetFromJsonAsync<JsonElement>($"/api/match/batches/{id}");
        var operationId = active.GetProperty("activeOperation").GetProperty("operationId").GetString();
        Assert.Equal(HttpStatusCode.Forbidden, (await AuthTests.PostWithToken(other, $"/api/match/operations/{operationId}/cancel")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await AuthTests.PostWithToken(other, $"/api/match/batches/{id}/operations",
            new { action = "start", expectedVersion = active.GetProperty("version").GetString() })).StatusCode);
        app.Clock.Advance(TimeSpan.FromMinutes(16));
        if (stop == "renewal")
        {
            (await client.GetAsync("/api/auth/me")).EnsureSuccessStatusCode();
            app.Clock.Advance(TimeSpan.FromMinutes(15));
            var stillRunning = await client.GetFromJsonAsync<JsonElement>($"/api/match/batches/{id}");
            Assert.Equal("running", stillRunning.GetProperty("status").GetString());
            (await AuthTests.PostWithToken(client, $"/api/match/operations/{operationId}/cancel")).EnsureSuccessStatusCode();
        }
        else if (stop == "logout") (await AuthTests.PostWithToken(client, "/api/auth/logout")).EnsureSuccessStatusCode();
        else if (stop == "cancel") Assert.Equal(HttpStatusCode.Accepted, (await AuthTests.PostWithToken(client, $"/api/match/operations/{operationId}/cancel")).StatusCode);
        else if (stop == "disconnect") { disconnect.Cancel(); response.Dispose(); }
        else
        {
            // Another independent login's renewal cannot extend this operation.
            (await other.GetAsync("/api/auth/me")).EnsureSuccessStatusCode();
            app.Clock.Advance(TimeSpan.FromMinutes(15));
        }
        if (stop != "disconnect")
        {
            var events = await response.Content.ReadAsStringAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Contains("operationStopped", events);
        }
        // Wait for server cleanup, using the public read boundary and an independent login.
        await AuthTests.Login(other, "operator", "OperatorDemo1!");
        JsonElement saved = default;
        for (var i = 0; i < 100; i++)
        {
            saved = await other.GetFromJsonAsync<JsonElement>($"/api/match/batches/{id}");
            if (saved.GetProperty("activeOperation").ValueKind == JsonValueKind.Null) break;
            await Task.Delay(10);
        }
        Assert.Equal(JsonValueKind.Null, saved.GetProperty("activeOperation").ValueKind);
        Assert.Equal("unfinished", saved.GetProperty("status").GetString());
        Assert.Equal(10, saved.GetProperty("unfinishedPairCount").GetInt32());
        Assert.Equal("interrupted", saved.GetProperty("pairs")[0].GetProperty("latestAttempt").GetProperty("status").GetString());
    }

    [Theory]
    [InlineData("native", "/api/chat")]
    [InlineData("openai-compatible", "/v1/chat/completions")]
    public async Task Each_local_route_uses_the_application_contract_and_records_invalid_answers_as_failures(string route, string path)
    {
        await using var app = new TestApp();
        app.Inference.Route = route;
        app.Ollama.Respond = async (request, token) =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/show") return Show();
            Assert.Equal(path, request.RequestUri.AbsolutePath);
            var input = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token)).RootElement;
            Assert.False(input.GetProperty("stream").GetBoolean());
            Assert.Contains("conditionLabelId", input.GetRawText());
            Assert.Contains("study.conditions.2", input.GetRawText());
            Assert.DoesNotContain("patient.age", input.GetRawText());
            Assert.DoesNotContain("patient.sex", input.GetRawText());
            return new(HttpStatusCode.OK) { Content = new StringContent(route == "native"
                ? "{\"done\":true,\"done_reason\":\"stop\",\"message\":{\"role\":\"assistant\",\"content\":\"{}\"}}"
                : "{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"role\":\"assistant\",\"content\":\"{}\"}}]}") };
        };
        using var client = app.Browser();
        await AuthTests.Login(client, "operator", "OperatorDemo1!");
        StudyTests.Page(app, StudyTests.Record("NCT00000001"));
        (await StudyTests.Import(client, StudyTests.Version(await StudyTests.State(client)))).EnsureSuccessStatusCode();
        var batch = (await client.GetFromJsonAsync<JsonElement>("/api/match/batches")).GetProperty("batches")[0];
        using var result = await AuthTests.PostWithToken(client, $"/api/match/batches/{batch.GetProperty("batchId").GetString()}/operations",
            new { action = "start", expectedVersion = batch.GetProperty("version").GetString() });
        var text = await result.Content.ReadAsStringAsync();
        Assert.Contains("pairFailed", text);
        Assert.Contains("invalidResponse", text);
        Assert.Contains("operationFinished", text);
        Assert.Equal(11, app.Ollama.Requests.Count);
    }

    [Fact]
    public async Task A_no_condition_batch_streams_stored_undetermined_assessments_and_cannot_restart()
    {
        await using var app = new TestApp();
        using var client = app.Browser();
        await AuthTests.Login(client, "operator", "OperatorDemo1!");
        StudyTests.Page(app, StudyTests.Record("NCT00000001").Replace("[\" Cancer \",\"\",\" Cancer \"]", "[]"));
        (await StudyTests.Import(client, StudyTests.Version(await StudyTests.State(client)))).EnsureSuccessStatusCode();
        var batch = (await client.GetFromJsonAsync<JsonElement>("/api/match/batches")).GetProperty("batches")[0];
        var id = batch.GetProperty("batchId").GetString();
        using var response = await AuthTests.PostWithToken(client, $"/api/match/batches/{id}/operations",
            new { action = "start", expectedVersion = batch.GetProperty("version").GetString() });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/x-ndjson", response.Content.Headers.ContentType?.MediaType);
        var events = (await response.Content.ReadAsStringAsync()).Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(s => JsonDocument.Parse(s).RootElement.Clone()).ToArray();
        Assert.Equal("operationStarted", events[0].GetProperty("type").GetString());
        Assert.Equal("operationFinished", events[^1].GetProperty("type").GetString());
        Assert.Equal(10, events.Count(e => e.GetProperty("type").GetString() == "pairCompleted"));
        var saved = await client.GetFromJsonAsync<JsonElement>($"/api/match/batches/{id}");
        Assert.Equal("completed", saved.GetProperty("status").GetString());
        var pair = saved.GetProperty("pairs")[0];
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/match/pairs/{pair.GetProperty("patientId").GetString()}/NCT00000001");
        Assert.Equal("conditionLabelAlignmentUndetermined", detail.GetProperty("pairSummary").GetString());
        Assert.True(detail.TryGetProperty("inputSnapshot", out _));
        Assert.Equal(HttpStatusCode.Conflict, (await AuthTests.PostWithToken(client, $"/api/match/batches/{id}/operations",
            new { action = "start", expectedVersion = saved.GetProperty("version").GetString() })).StatusCode);
    }

    [Fact]
    public async Task Completed_import_exposes_ten_fixed_pairs_without_starting_work()
    {
        await using var app = new TestApp();
        using var client = app.Browser();
        await AuthTests.Login(client, "operator", "OperatorDemo1!");
        StudyTests.Page(app, StudyTests.Record("NCT00000001"), "next");
        (await StudyTests.Import(client, StudyTests.Version(await StudyTests.State(client)))).EnsureSuccessStatusCode();
        var batches = await client.GetFromJsonAsync<JsonElement>("/api/match/batches");
        var batch = Assert.Single(batches.GetProperty("batches").EnumerateArray());
        Assert.Equal(10, batch.GetProperty("totalPairCount").GetInt32());
        Assert.Equal("notStarted", batch.GetProperty("status").GetString());
        var id = batch.GetProperty("batchId").GetString();
        StudyTests.Page(app, StudyTests.Record("NCT00000002"));
        (await StudyTests.Import(client, StudyTests.Version(await StudyTests.State(client)))).EnsureSuccessStatusCode();
        var original = await client.GetFromJsonAsync<JsonElement>($"/api/match/batches/{id}");
        Assert.Equal(10, original.GetProperty("totalPairCount").GetInt32());
        Assert.All(original.GetProperty("pairs").EnumerateArray(), pair =>
        {
            Assert.Equal("NCT00000001", pair.GetProperty("nctId").GetString());
            Assert.Equal("unfinished", pair.GetProperty("state").GetString());
        });
    }
}
