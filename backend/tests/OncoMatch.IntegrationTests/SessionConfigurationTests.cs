using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OncoMatch.IntegrationTests;

public class SessionConfigurationTests
{
    [Theory]
    [InlineData("Development", null, "2026-10-27T12:30:00+00:00")]
    [InlineData("Development", "true", "2026-10-27T12:02:00+00:00")]
    [InlineData("Production", "true", "2026-10-27T12:30:00+00:00")]
    public async Task Actual_startup_configuration_controls_login_lifetime_only_in_development(
        string environment, string? shortSessions, string expectedExpiry)
    {
        // Use the real application configuration, without TestApp's cookie
        // lifetime override, so changing normal startup defaults fails here.
        var clock = new TestClock();
        await using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["SessionTesting:Enabled"] = shortSessions }));
            builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(clock));
        });
        using var client = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });

        await AuthTests.Login(client);
        var session = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal(expectedExpiry, session.GetProperty("expiresAt").GetString());
    }
}
