using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OncoMatch.IntegrationTests;

public class MatchConfigurationTests
{
    [Theory]
    [InlineData("RequestTimeoutSeconds", "not-a-number")]
    [InlineData("RequestTimeoutSeconds", "0")]
    [InlineData("Route", "cloud")]
    [InlineData("BaseUrl", "https://example.com")]
    public async Task Invalid_match_configuration_does_not_prevent_login_or_patient_browsing(string key, string value)
    {
        await using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { [$"Match:Inference:{key}"] = value }));
            builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(new TestClock()));
        });
        using var client = app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await AuthTests.Login(client);
        Assert.Equal(10, (await client.GetFromJsonAsync<JsonElement>("/api/patients")).GetArrayLength());
        var state = await client.GetFromJsonAsync<JsonElement>("/api/match/batches");
        Assert.Equal(JsonValueKind.String, state.GetProperty("configurationError").ValueKind);
    }
}
