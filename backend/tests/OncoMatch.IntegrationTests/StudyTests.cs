using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace OncoMatch.IntegrationTests;

public class StudyTests
{
    internal static string Record(string id, string title = "Study") => JsonSerializer.Serialize(new
    {
        protocolSection = new
        {
            identificationModule = new { nctId = id, briefTitle = title },
            conditionsModule = new { conditions = new[] { " Cancer ", "", " Cancer " } },
            eligibilityModule = new { minimumAge = "Unfamiliar age", sex = "ALL" }
        }
    });
    internal static void Page(TestApp app, string records, string? token = null) => app.Registry.Respond = (_, _) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent("{\"studies\":[" + records + "]" + (token is null ? "" : ",\"nextPageToken\":" + JsonSerializer.Serialize(token)) + "}") });
    internal static Task<HttpResponseMessage> Import(HttpClient client, string version) =>
        AuthTests.PostWithToken(client, "/api/study-imports", new { expectedVersion = version });
    internal static async Task<JsonElement> State(HttpClient client) => await client.GetFromJsonAsync<JsonElement>("/api/study-imports");
    internal static string Version(JsonElement state) => state.GetProperty("version").GetString()!;
    private static async Task Disconnected(Task<HttpResponseMessage> pending)
    {
        var error = await Xunit.Record.ExceptionAsync(async () => await pending.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(error is OperationCanceledException or HttpRequestException or IOException,
            $"Expected a disconnected request, got {error?.GetType().Name ?? "a response"}");
    }

    [Fact]
    public async Task A_committed_import_survives_logout_and_a_lost_response_without_replaying_its_page()
    {
        await using var app = new TestApp();
        using var client = app.Browser();
        await AuthTests.Login(client, "operator", "OperatorDemo1!");
        var initial = Version(await State(client));
        var hold = new ResponseBarrier();
        app.ImportResponse = hold;
        Page(app, Record("NCT00000001"), "second-page");
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/study-imports");
        request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("requestToken").GetString());
        request.Content = JsonContent.Create(new { expectedVersion = initial });
        using var disconnect = new CancellationTokenSource();
        var pending = client.SendAsync(request, disconnect.Token);
        await hold.Reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            Assert.False(pending.IsCompleted);
            var published = await State(client);
            Assert.Equal("completed", published.GetProperty("attempts")[0].GetProperty("status").GetString());
            Assert.False(published.GetProperty("isImporting").GetBoolean());
            Assert.Equal("next", published.GetProperty("nextAction").GetString());
            Assert.Single((await client.GetFromJsonAsync<JsonElement>("/api/studies")).EnumerateArray());
            await AuthTests.PostWithToken(client, "/api/auth/logout");
            disconnect.Cancel();
            hold.Release.TrySetResult();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending);
        }
        finally { hold.Release.TrySetResult(); app.ImportResponse = null; }
        await AuthTests.Login(client, "operator", "OperatorDemo1!");
        var state = await State(client);
        Assert.Single(state.GetProperty("attempts").EnumerateArray());
        Assert.Equal("completed", state.GetProperty("attempts")[0].GetProperty("status").GetString());
        Assert.Equal(1, state.GetProperty("totalImportedStudyCount").GetInt32());
        Page(app, Record("NCT00000002"));
        Assert.Equal(HttpStatusCode.OK, (await Import(client, Version(state))).StatusCode);
        Assert.Equal("second-page", Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(app.Registry.Requests[^1].Query)["pageToken"]);
        Assert.Equal(2, (await State(client)).GetProperty("totalImportedStudyCount").GetInt32());
    }

    [Fact]
    public async Task An_older_operator_cookie_without_session_identity_requires_sign_in_before_import_admission()
    {
        await using var app = new TestApp();
        using var client = app.Browser();
        var options = app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "operator"), new Claim(ClaimTypes.Role, "Operator")
        }, CookieAuthenticationDefaults.AuthenticationScheme));
        var ticket = new AuthenticationTicket(principal, new AuthenticationProperties
        { IssuedUtc = app.Clock.GetUtcNow(), ExpiresUtc = app.Clock.GetUtcNow().AddMinutes(30) }, CookieAuthenticationDefaults.AuthenticationScheme);
        client.DefaultRequestHeaders.Add("Cookie", $"OncoMatch.Auth={options.TicketDataFormat.Protect(ticket)}");
        var response = await Import(client, Version(await State(client)));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("authentication_required", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Empty(app.Registry.Requests);
        Assert.Empty((await State(client)).GetProperty("attempts").EnumerateArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Response_body_is_subject_to_deadline_and_cancellation_without_publishing_partial_studies(bool logout)
    {
        await using var app = new TestApp();
        using var client = app.Browser();
        await AuthTests.Login(client, "operator", "OperatorDemo1!");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        app.Registry.Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StreamContent(new HeldBody(entered)) });
        var pending = Import(client, Version(await State(client)));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Empty((await client.GetFromJsonAsync<JsonElement>("/api/studies")).EnumerateArray());
        Assert.Equal(JsonValueKind.Null, (await State(client)).GetProperty("attempts")[0].GetProperty("fetchedCount").ValueKind);
        if (logout) await AuthTests.PostWithToken(client, "/api/auth/logout");
        else app.Clock.Advance(TimeSpan.FromSeconds(30));
        if (!logout) Assert.Equal(HttpStatusCode.GatewayTimeout, (await pending.WaitAsync(TimeSpan.FromSeconds(5))).StatusCode);
        else { await Disconnected(pending); await AuthTests.Login(client, "operator", "OperatorDemo1!"); }
        var state = await State(client);
        Assert.Equal(logout ? "cancelled" : "failed", state.GetProperty("attempts")[0].GetProperty("status").GetString());
        Assert.Equal(0, state.GetProperty("totalImportedStudyCount").GetInt32());
        Assert.Equal("retry", state.GetProperty("nextAction").GetString());
        Page(app, Record("NCT00000001"));
        Assert.Equal(HttpStatusCode.OK, (await Import(client, Version(state))).StatusCode);
    }

    private class HeldBody(TaskCompletionSource entered) : MemoryStream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.Infinite, token);
            return 0;
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token) =>
            ReadAsync(buffer.AsMemory(offset, count), token).AsTask();
    }

    [Theory]
    [InlineData("{}", null)]
    [InlineData("{\"conditionsModule\":null}", null)]
    [InlineData("{\"conditionsModule\":{\"conditions\":null}}", null)]
    [InlineData("{\"conditionsModule\":{\"conditions\":[]}}", 0)]
    public async Task Optional_fields_and_absent_null_or_empty_conditions_remain_distinct(string fields, int? length)
    {
        await using var app = new TestApp();
        using var client = app.Browser();
        await AuthTests.Login(client, "operator", "OperatorDemo1!");
        var extra = fields.Length > 2 ? "," + fields[1..^1] : "";
        Page(app, "{\"protocolSection\":{\"identificationModule\":{\"nctId\":\"NCT00000001\"}" + extra + ",\"unrelated\":42},\"other\":true}");
        Assert.Equal(HttpStatusCode.OK, (await Import(client, Version(await State(client)))).StatusCode);
        var details = await client.GetFromJsonAsync<JsonElement>("/api/studies/NCT00000001");
        Assert.Equal(JsonValueKind.Null, details.GetProperty("title").ValueKind);
        Assert.Equal(JsonValueKind.Null, details.GetProperty("minimumAge").ValueKind);
        if (length is null) Assert.Equal(JsonValueKind.Null, details.GetProperty("conditions").ValueKind);
        else Assert.Equal(length, details.GetProperty("conditions").GetArrayLength());
    }

    [Fact]
    public async Task Registry_deadline_cancels_an_unfinished_response_and_releases_the_import_gate()
    {
        await using var app = new TestApp();
        using var client = app.Browser();
        await AuthTests.Login(client, "operator", "OperatorDemo1!");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        app.Registry.Respond = async (_, token) =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            throw new InvalidOperationException();
        };
        var pending = Import(client, Version(await State(client)));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        app.Clock.Advance(TimeSpan.FromSeconds(30));
        try
        {
            var response = await pending.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
            Assert.Equal("registry_timeout", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
            var state = await State(client);
            Assert.Equal("failed", state.GetProperty("attempts")[0].GetProperty("status").GetString());
            Assert.False(state.GetProperty("isImporting").GetBoolean());
            Page(app, "", "continue");
            Assert.Equal(HttpStatusCode.OK, (await Import(client, Version(state))).StatusCode);
        }
        finally { await AuthTests.PostWithToken(client, "/api/auth/logout"); }
    }

    [Theory]
    [InlineData("{\"studies\":null}", "registry_invalid_response")]
    [InlineData("{\"studies\":[],\"nextPageToken\":null}", "registry_invalid_response")]
    [InlineData("{\"studies\":[],\"nextPageToken\":\"\"}", "registry_invalid_response")]
    [InlineData("[]", "registry_invalid_response")]
    [InlineData("not-json", "registry_invalid_response")]
    [InlineData("transport", "registry_unavailable")]
    [InlineData("cancelled-transport", "registry_unavailable")]
    [InlineData("http", "registry_unavailable")]
    [InlineData("unexpected", "server_error")]
    public async Task Failed_attempts_preserve_data_and_cursor_and_explicit_retry_links_a_new_attempt(string body, string code)
    {
        await using var app = new TestApp();
        using var client = app.Browser();
        await AuthTests.Login(client, "operator", "OperatorDemo1!");
        Page(app, Record("NCT00000001"), "second");
        await Import(client, Version(await State(client)));
        app.Registry.Respond = (_, _) => body switch
        {
            "transport" => throw new HttpRequestException("Private transport detail"),
            "cancelled-transport" => throw new OperationCanceledException("Transport cancelled without application cancellation"),
            "unexpected" => throw new InvalidOperationException("Private exception detail"),
            _ => Task.FromResult(new HttpResponseMessage(body == "http" ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK)
                { Content = new StringContent(body) })
        };
        var response = await Import(client, Version(await State(client)));
        Assert.Equal(code == "server_error" ? HttpStatusCode.InternalServerError : HttpStatusCode.BadGateway, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(code, problem.GetProperty("code").GetString());
        Assert.DoesNotContain("Private", problem.ToString());
        var state = await State(client);
        var failed = state.GetProperty("attempts")[0];
        Assert.Equal("failed", failed.GetProperty("status").GetString());
        Assert.Equal("retry", state.GetProperty("nextAction").GetString());
        foreach (var count in new[] { "fetchedCount", "importedCount", "duplicateCount", "rejectedCount" })
            Assert.Equal(JsonValueKind.Null, failed.GetProperty(count).ValueKind);
        Assert.Empty(failed.GetProperty("rejections").EnumerateArray());
        Assert.Equal(1, state.GetProperty("totalImportedStudyCount").GetInt32());
        Page(app, "");
        var retryResponse = await Import(client, Version(state));
        Assert.Equal(HttpStatusCode.OK, retryResponse.StatusCode);
        state = await State(client);
        Assert.Equal(failed.GetProperty("id").GetString(), state.GetProperty("attempts")[0].GetProperty("retryOfAttemptId").GetString());
        Assert.Equal(problem.GetProperty("attemptId").GetString(), failed.GetProperty("id").GetString());
        Assert.Equal("second", Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(app.Registry.Requests[^1].Query)["pageToken"]);
        Assert.False(state.GetProperty("isImporting").GetBoolean());
    }

    [Theory]
    [InlineData("{\"protocolSection\":{\"identificationModule\":{\"nctId\":\"NCT00000001\",\"briefTitle\":5}}}", "invalid_field_shape", "NCT00000001")]
    [InlineData("{\"protocolSection\":{\"identificationModule\":{\"nctId\":\"NCT00000001\"},\"conditionsModule\":{\"conditions\":[null]}}}", "invalid_field_shape", "NCT00000001")]
    [InlineData("{\"protocolSection\":[]}", "invalid_field_shape", null)]
    [InlineData("{}", "invalid_nct_id", null)]
    [InlineData("{\"protocolSection\":{\"identificationModule\":{\"nctId\":\"NCT１２３４５６７８\"}}}", "invalid_nct_id", null)]
    public async Task Invalid_records_do_not_reserve_an_identity_or_prevent_a_completed_page(string record, string code, string? id)
    {
        await using var app = new TestApp();
        using var client = app.Browser();
        await AuthTests.Login(client, "operator", "OperatorDemo1!");
        Page(app, record, "more");
        await Import(client, Version(await State(client)));
        var state = await State(client);
        var rejection = state.GetProperty("attempts")[0].GetProperty("rejections")[0];
        Assert.Equal(code, rejection.GetProperty("code").GetString());
        Assert.Equal(id, rejection.GetProperty("nctId").GetString());
        Assert.Equal("next", state.GetProperty("nextAction").GetString());
        Page(app, Record("NCT00000001"));
        Assert.Equal(HttpStatusCode.OK, (await Import(client, Version(state))).StatusCode);
        Assert.Equal(1, (await State(client)).GetProperty("totalImportedStudyCount").GetInt32());
    }

    [Fact]
    public async Task Access_csrf_input_and_stale_state_rejections_never_contact_the_registry_or_create_attempts()
    {
        await using var app = new TestApp();
        using var client = app.Browser();
        foreach (var path in new[] { "/api/studies", "/api/studies/NCT00000001", "/api/study-imports" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Import(client, "x")).StatusCode);
        await AuthTests.Login(client);
        Assert.Equal(HttpStatusCode.Forbidden, (await Import(client, "x")).StatusCode);
        await AuthTests.Login(client, "operator", "OperatorDemo1!");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/study-imports", new { expectedVersion = "x" })).StatusCode);
        foreach (var input in new object[] { new { }, new { expectedVersion = " " }, new { expectedVersion = 2 }, new { expectedVersion = "x", pageToken = "client-token" } })
            Assert.Equal(HttpStatusCode.BadRequest, (await AuthTests.PostWithToken(client, "/api/study-imports", input)).StatusCode);
        var stale = await Import(client, "old-version");
        Assert.Equal("import_state_changed", (await stale.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        var missing = await client.GetAsync("/api/studies/not-valid");
        Assert.Equal("study_not_found", (await missing.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Empty(app.Registry.Requests);
        Assert.Empty((await State(client)).GetProperty("attempts").EnumerateArray());
    }

    [Fact]
    public async Task Busy_imports_reject_competitors_and_logout_cancels_only_the_initiating_session()
    {
        await using var app = new TestApp();
        using var owner = app.Browser();
        using var independent = app.Browser();
        await AuthTests.Login(owner, "operator", "OperatorDemo1!");
        await AuthTests.Login(independent, "operator", "OperatorDemo1!");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        app.Registry.Respond = async (_, token) =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            throw new InvalidOperationException();
        };
        var initialVersion = Version(await State(owner));
        var pending = Import(owner, initialVersion);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var active = await State(independent);
        Assert.NotEqual(initialVersion, Version(active));
        Assert.True(active.GetProperty("isImporting").GetBoolean());
        Assert.Equal("none", active.GetProperty("nextAction").GetString());
        var busy = await Import(independent, initialVersion);
        Assert.Equal("import_in_progress", (await busy.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        await AuthTests.PostWithToken(independent, "/api/auth/logout");
        Assert.False(pending.IsCompleted);
        // Same-cookie logout models another tab in the initiating session.
        await AuthTests.PostWithToken(owner, "/api/auth/logout");
        await Disconnected(pending);
        await AuthTests.Login(owner, "operator", "OperatorDemo1!");
        var state = await State(owner);
        Assert.Equal("cancelled", state.GetProperty("attempts")[0].GetProperty("status").GetString());
        Assert.Equal("retry", state.GetProperty("nextAction").GetString());
        Assert.Equal(0, state.GetProperty("totalImportedStudyCount").GetInt32());
        Page(app, Record("NCT00000001"));
        Assert.Equal(HttpStatusCode.OK, (await Import(owner, Version(state))).StatusCode);
        await AuthTests.PostWithToken(owner, "/api/auth/logout");
        await AuthTests.Login(owner);
        Assert.Single((await owner.GetFromJsonAsync<JsonElement>("/api/studies")).EnumerateArray());
        await using var restarted = new TestApp();
        using var fresh = restarted.Browser();
        await AuthTests.Login(fresh);
        Assert.Empty((await fresh.GetFromJsonAsync<JsonElement>("/api/studies")).EnumerateArray());
        Assert.NotEqual(initialVersion, Version(await State(fresh)));
    }

    [Fact]
    public async Task Mixed_pages_keep_usable_studies_report_rejections_and_advance_without_replacement_fetches()
    {
        await using var app = new TestApp();
        using var client = app.Browser();
        await AuthTests.Login(client, "operator", "OperatorDemo1!");
        Page(app, Record("NCT00000001", " Original ") + "," + Record("NCT00000001", "Replacement") +
            ",null," + Record("invalid") + "," + Record("NCT00000002"), "opaque +/=");
        var response = await Import(client, Version(await State(client)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var state = await response.Content.ReadFromJsonAsync<JsonElement>();
        var attempt = state.GetProperty("attempts")[0];
        Assert.Equal("next", state.GetProperty("nextAction").GetString());
        Assert.Equal(5, attempt.GetProperty("fetchedCount").GetInt32());
        Assert.Equal(2, attempt.GetProperty("importedCount").GetInt32());
        Assert.Equal(1, attempt.GetProperty("duplicateCount").GetInt32());
        Assert.Equal(2, attempt.GetProperty("rejectedCount").GetInt32());
        Assert.Equal(new[] { 3, 4 }, attempt.GetProperty("rejections").EnumerateArray().Select(r => r.GetProperty("recordNumber").GetInt32()));
        var studies = await client.GetFromJsonAsync<JsonElement>("/api/studies");
        Assert.Equal(new[] { "NCT00000001", "NCT00000002" }, studies.EnumerateArray().Select(s => s.GetProperty("nctId").GetString()));
        Assert.Equal(" Original ", studies[0].GetProperty("title").GetString());
        Assert.Equal(new[] { " Cancer ", "", " Cancer " }, studies[0].GetProperty("conditions").EnumerateArray().Select(c => c.GetString()));
        Assert.False(studies[0].TryGetProperty("minimumAge", out _));
        var details = await client.GetFromJsonAsync<JsonElement>("/api/studies/NCT00000001");
        Assert.Equal("Unfamiliar age", details.GetProperty("minimumAge").GetString());
        Assert.Equal("https://clinicaltrials.gov/study/NCT00000001", details.GetProperty("registryUrl").GetString());
        var first = Assert.Single(app.Registry.Requests);
        Assert.Equal("clinicaltrials.gov", first.Host);
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(first.Query);
        Assert.Equal("json", query["format"]);
        Assert.Equal("cancer", query["query.cond"]);
        Assert.Equal("RECRUITING,NOT_YET_RECRUITING", query["filter.overallStatus"]);
        Assert.Equal("AREA[StudyType](INTERVENTIONAL OR OBSERVATIONAL)", query["filter.advanced"]);
        Assert.Equal("@relevance", query["sort"]);
        Assert.Equal("10", query["pageSize"]);
        Assert.False(query.ContainsKey("pageToken"));
        Page(app, Record("NCT00000001", "Changed"));
        Assert.Equal(HttpStatusCode.OK, (await Import(client, Version(state))).StatusCode);
        Assert.Equal(2, app.Registry.Requests.Count);
        Assert.Equal("opaque +/=", Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(app.Registry.Requests[1].Query)["pageToken"]);
        state = await State(client);
        Assert.True(state.GetProperty("isExhausted").GetBoolean());
        Assert.Equal(0, state.GetProperty("attempts")[0].GetProperty("importedCount").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, (await Import(client, Version(state))).StatusCode);
        Assert.Equal(2, app.Registry.Requests.Count);
    }

    [Fact]
    public async Task Both_roles_start_with_shared_empty_studies_and_an_initial_import_action()
    {
        await using var app = new TestApp();
        foreach (var role in new[] { "viewer", "operator" })
        {
            using var client = app.Browser();
            await AuthTests.Login(client, role, role == "viewer" ? "ViewerDemo1!" : "OperatorDemo1!");
            Assert.Empty((await client.GetFromJsonAsync<JsonElement>("/api/studies")).EnumerateArray());
            var state = await client.GetFromJsonAsync<JsonElement>("/api/study-imports");
            Assert.Equal("initial", state.GetProperty("nextAction").GetString());
            Assert.False(state.GetProperty("isImporting").GetBoolean());
            Assert.False(state.GetProperty("isExhausted").GetBoolean());
            Assert.Equal(0, state.GetProperty("totalImportedStudyCount").GetInt32());
            Assert.Empty(state.GetProperty("attempts").EnumerateArray());
            Assert.False(string.IsNullOrEmpty(state.GetProperty("version").GetString()));
        }
    }
}
