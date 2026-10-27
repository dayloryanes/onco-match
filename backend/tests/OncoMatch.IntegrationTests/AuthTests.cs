using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace OncoMatch.IntegrationTests;

public class AuthTests
{
    [Theory]
    [InlineData("viewer", "wrong", 401, "invalid_credentials")]
    [InlineData("nobody", "wrong", 401, "invalid_credentials")]
    [InlineData("viewer", " ViewerDemo!1 ", 401, "invalid_credentials")]
    [InlineData("", "ViewerDemo!1", 400, "invalid_request")]
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
    public async Task Mutations_reject_missing_or_invalid_csrf_without_changing_the_session(string action, bool invalidToken)
    {
        await using var app = new TestApp();
        using var client = app.Browser();
        await Login(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/auth/{action}");
        request.Content = JsonContent.Create(new { username = "operator", password = "OperatorDemo!1" });
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
        app.Clock.Advance(TimeSpan.FromMinutes(31 - minutes));
        Assert.Equal(renews ? HttpStatusCode.OK : HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/auth/me")).StatusCode);
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

    internal static async Task Login(HttpClient client, string username = "viewer", string password = "ViewerDemo!1")
    {
        var response = await PostWithToken(client, "/api/auth/login", new { username, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("viewer", "ViewerDemo!1", "Viewer")]
    [InlineData(" OPERATOR ", "OperatorDemo!1", "Operator")]
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
