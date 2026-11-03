using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace OncoMatch.IntegrationTests;

public class AuthTests
{
    [Fact]
    public async Task A_focus_check_can_renew_after_a_read_only_token_fetch()
    {
        await using var app = new TestApp();
        using var client = app.Browser();
        await Login(client);
        var options = app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        app.Clock.Advance(options.ExpireTimeSpan / 2 + TimeSpan.FromSeconds(1));
        var token = await client.GetAsync("/api/auth/csrf");
        Assert.False(token.Headers.TryGetValues("Set-Cookie", out _));
        var confirmed = DateTimeOffset.Parse(Assert.Single(token.Headers.GetValues("X-Session-Expires-At")));
        var refresh = await client.GetAsync("/api/auth/me");
        var body = await refresh.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("expiresAt").GetDateTimeOffset() > confirmed);
        Assert.Equal("true", Assert.Single(refresh.Headers.GetValues("X-Session-Renewed")));
    }

    [Fact]
    public async Task A_focus_session_check_can_renew_without_an_extension_action()
    {
        await using var app = new TestApp();
        using var client = app.Browser();
        await Login(client);
        var options = app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        app.Clock.Advance(options.ExpireTimeSpan / 2 + TimeSpan.FromSeconds(1));
        var check = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, check.StatusCode);
        Assert.Equal("true", Assert.Single(check.Headers.GetValues("X-Session-Renewed")));
        Assert.Contains(check.Headers.GetValues("Set-Cookie"), value => value.StartsWith("OncoMatch.Auth="));
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(2, true)]
    [InlineData(30, false)]
    [InlineData(30, true)]
    public async Task A_rejected_extension_does_not_renew_the_cookie(int minutes, bool invalidToken)
    {
        await using var app = new TestApp(TimeSpan.FromMinutes(minutes));
        using var client = app.Browser();
        await Login(client);
        var options = app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        app.Clock.Advance(options.ExpireTimeSpan / 2 + TimeSpan.FromSeconds(1));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/extend-session");
        if (invalidToken) request.Headers.Add("X-CSRF-TOKEN", "invalid-token");
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(response.Headers.TryGetValues("X-Session-Renewed", out _));
        Assert.False(response.Headers.TryGetValues("Set-Cookie", out _));
        app.Clock.Advance(options.ExpireTimeSpan / 2);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Theory]
    [InlineData(2, "/api/auth/csrf")]
    [InlineData(30, "/API/AUTH/CSRF/")]
    public async Task Fetching_a_csrf_token_does_not_renew_the_cookie(int minutes, string path)
    {
        await using var app = new TestApp(TimeSpan.FromMinutes(minutes));
        using var client = app.Browser();
        await Login(client);
        var options = app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        app.Clock.Advance(options.ExpireTimeSpan / 2 + TimeSpan.FromSeconds(1));
        var csrf = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, csrf.StatusCode);
        Assert.False(csrf.Headers.TryGetValues("Set-Cookie", out _));
        Assert.False(csrf.Headers.TryGetValues("X-Session-Renewed", out _));
        // A browser-blocked POST never arrives; acquiring its token must not
        // prolong the existing ticket on its own.
        app.Clock.Advance(options.ExpireTimeSpan / 2);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Theory]
    [InlineData(2, "2026-10-27T12:03:01+00:00")]
    [InlineData(30, "2026-10-27T12:45:01+00:00")]
    public async Task A_successful_extension_renews_explicitly_after_the_sliding_threshold(int minutes, string expectedExpiry)
    {
        await using var app = new TestApp(TimeSpan.FromMinutes(minutes));
        using var client = app.Browser();
        await Login(client);
        app.Clock.Advance(TimeSpan.FromMinutes(minutes / 2) + TimeSpan.FromSeconds(1));
        var response = await PostWithToken(client, "/API/AUTH/EXTEND-SESSION/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(expectedExpiry, body.GetProperty("expiresAt").GetString());
        Assert.Equal("true", Assert.Single(response.Headers.GetValues("X-Session-Renewed")));
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), value => value.StartsWith("OncoMatch.Auth="));
    }

    [Theory]
    [InlineData("viewer", "ViewerDemo1!")]
    [InlineData("operator", "OperatorDemo1!")]
    public async Task Explicit_extension_issues_a_fresh_cookie_before_sliding_threshold(string username, string password)
    {
        await using var app = new TestApp();
        using var client = app.Browser();
        var login = await PostWithToken(client, "/api/auth/login", new { username, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        app.Clock.Advance(TimeSpan.FromMinutes(5));
        var extension = await PostWithToken(client, "/api/auth/extend-session");
        Assert.Equal(HttpStatusCode.OK, extension.StatusCode);
        var session = await extension.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("2026-10-27T12:35:00+00:00", session.GetProperty("expiresAt").GetString());
        Assert.Equal("2026-10-27T12:05:00+00:00", session.GetProperty("serverTime").GetString());
        Assert.Contains(extension.Headers.GetValues("Set-Cookie"), value => value.StartsWith("OncoMatch.Auth="));
        var options = app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        string? SessionId(HttpResponseMessage response)
        {
            var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), value => value.StartsWith("OncoMatch.Auth="));
            var value = cookie.Split(';')[0]["OncoMatch.Auth=".Length..];
            return options.TicketDataFormat.Unprotect(value)!.Principal.FindFirst("session_id")?.Value;
        }
        Assert.False(string.IsNullOrWhiteSpace(SessionId(login)));
        Assert.Equal(SessionId(login), SessionId(extension));
        app.Clock.Advance(TimeSpan.FromMinutes(26));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Theory]
    [InlineData("viewer", "wrong", 401, "invalid_credentials")]
    [InlineData("nobody", "wrong", 401, "invalid_credentials")]
    [InlineData("viewer", "ViewerDemo!1", 401, "invalid_credentials")]
    [InlineData("operator", "OperatorDemo!1", 401, "invalid_credentials")]
    [InlineData("viewer", " ViewerDemo1! ", 401, "invalid_credentials")]
    [InlineData("", "ViewerDemo1!", 400, "invalid_request")]
    [InlineData("viewer", "", 400, "invalid_request")]
    public async Task Failed_login_reports_a_stable_error_without_creating_a_session(
        string username, string password, int status, string code)
    {
        await using var app = new TestApp();
        using var client = app.Browser();
        var response = await PostWithToken(client, "/api/auth/login", new { username, password });
        Assert.Equal((HttpStatusCode)status, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(code, problem.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Theory]
    [InlineData("login", false)]
    [InlineData("logout", false)]
    [InlineData("login", true)]
    [InlineData("logout", true)]
    [InlineData("extend-session", false)]
    [InlineData("extend-session", true)]
    public async Task Mutations_reject_missing_or_invalid_csrf_without_changing_the_session(string action, bool invalidToken)
    {
        await using var app = new TestApp();
        using var client = app.Browser();
        await Login(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/auth/{action}");
        request.Content = JsonContent.Create(new { username = "operator", password = "OperatorDemo1!" });
        if (invalidToken) request.Headers.Add("X-CSRF-TOKEN", "invalid-token");
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("csrf_invalid", problem.GetProperty("code").GetString());
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal("viewer", me.GetProperty("username").GetString());
    }

    [Fact]
    public async Task Anonymous_csrf_token_is_not_valid_for_authenticated_logout()
    {
        await using var app = new TestApp();
        using var client = app.Browser();
        var oldToken = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        await Login(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        request.Headers.Add("X-CSRF-TOKEN", oldToken.GetProperty("requestToken").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(request)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Theory]
    [InlineData(10, false)]
    [InlineData(16, true)]
    public async Task Cookie_renews_only_after_half_of_its_lifetime(int minutes, bool renews)
    {
        await using var app = new TestApp();
        using var client = app.Browser();
        await Login(client);
        app.Clock.Advance(TimeSpan.FromMinutes(minutes));
        var response = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(renews, response.Headers.TryGetValues("Set-Cookie", out _));
        var metadata = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(renews ? "2026-10-27T12:46:00+00:00" : "2026-10-27T12:30:00+00:00",
            metadata.GetProperty("expiresAt").GetString());
        Assert.Equal(metadata.GetProperty("expiresAt").GetDateTimeOffset(),
            DateTimeOffset.Parse(Assert.Single(response.Headers.GetValues("X-Session-Expires-At"))));
        app.Clock.Advance(TimeSpan.FromMinutes(31 - minutes));
        Assert.Equal(renews ? HttpStatusCode.OK : HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Ordinary_protected_responses_report_actual_renewal_and_independent_logins_keep_their_deadlines()
    {
        await using var app = new TestApp();
        using var first = app.Browser();
        using var second = app.Browser();
        await Login(first);
        await Login(second);
        app.Clock.Advance(TimeSpan.FromMinutes(16));
        var patients = await first.GetAsync("/api/patients");
        Assert.Equal("2026-10-27T12:46:00.0000000+00:00",
            Assert.Single(patients.Headers.GetValues("X-Session-Expires-At")));
        Assert.Contains(patients.Headers.GetValues("Set-Cookie"), value => value.StartsWith("OncoMatch.Auth="));
        app.Clock.Advance(TimeSpan.FromMinutes(15));
        Assert.Equal(HttpStatusCode.OK, (await first.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await second.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostWithToken(second, "/api/auth/extend-session")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await first.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Anonymous_extension_requires_login()
    {
        await using var app = new TestApp();
        using var client = app.Browser();
        var response = await PostWithToken(client, "/api/auth/extend-session");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(response.Headers.TryGetValues("Set-Cookie", out _));
    }

    [Fact]
    public async Task An_expired_cookie_cannot_renew_itself()
    {
        await using var app = new TestApp();
        using var client = app.Browser();
        await Login(client);
        app.Clock.Advance(TimeSpan.FromMinutes(31));
        var response = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(response.Headers.TryGetValues("Set-Cookie", out _));
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("authentication_required", problem.GetProperty("code").GetString());
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
    }

    [Fact]
    public async Task Logout_clears_this_browser_but_not_another_browser()
    {
        await using var app = new WebApplicationFactory<Program>();
        using var first = CreateBrowser(app);
        using var second = CreateBrowser(app);
        await Login(first);
        await Login(second);

        var logout = await PostWithToken(first, "/api/auth/logout");

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await first.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await second.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await PostWithToken(first, "/api/auth/logout")).StatusCode);
    }

    internal static async Task<HttpResponseMessage> PostWithToken(HttpClient client, string url, object? body = null)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("X-CSRF-TOKEN", token.GetProperty("requestToken").GetString());
        if (body is not null) request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    internal static async Task Login(HttpClient client, string username = "viewer", string password = "ViewerDemo1!")
    {
        var response = await PostWithToken(client, "/api/auth/login", new { username, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("viewer", "ViewerDemo1!", "Viewer")]
    [InlineData(" OPERATOR ", "OperatorDemo1!", "Operator")]
    public async Task Demo_users_can_login_and_read_their_session(string username, string password, string role)
    {
        await using var app = new WebApplicationFactory<Program>();
        using var client = CreateBrowser(app);

        var token = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login");
        request.Headers.Add("X-CSRF-TOKEN", token.GetProperty("requestToken").GetString());
        request.Content = JsonContent.Create(new { username, password });
        var login = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal(username.Trim().ToLowerInvariant(), me.GetProperty("username").GetString());
        Assert.Equal(role, me.GetProperty("role").GetString());
        var cookie = Assert.Single(login.Headers.GetValues("Set-Cookie"), value => value.StartsWith("OncoMatch.Auth="));
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("expires=", cookie, StringComparison.OrdinalIgnoreCase);
    }

    private static HttpClient CreateBrowser(WebApplicationFactory<Program> app) =>
        app.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

    [Fact]
    public async Task Anonymous_requests_cannot_read_the_current_user()
    {
        await using var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
