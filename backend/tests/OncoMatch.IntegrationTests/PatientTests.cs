using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OncoMatch.Api.Features.Patients;

namespace OncoMatch.IntegrationTests;

public class PatientTests
{
    // Independent expected values from the human-reviewed specification, not the fixture file.
    private static readonly PatientDetails[] ApprovedPatients =
    {
        new("P001", 7, "female", "Acute lymphoblastic leukemia", new("notApplicable", null, null), new("known", "newlyDiagnosed")),
        new("P002", 15, "male", "Acute myeloid leukemia", new("notApplicable", null, null), new("known", "refractory")),
        new("P003", 12, "female", "Hodgkin lymphoma", new("unknown", null, null), new("known", "newlyDiagnosed")),
        new("P004", 17, "male", "Osteosarcoma", new("unknown", null, null), new("known", "recurrent")),
        new("P005", 42, "female", "Breast cancer", new("known", "II", "TNM stage group; edition unspecified"), new("known", "newlyDiagnosed")),
        new("P006", 68, "male", "Non-small cell lung cancer", new("known", "IV", "TNM stage group; edition unspecified"), new("known", "newlyDiagnosed")),
        new("P007", 56, "female", "Colon cancer", new("known", "III", "TNM stage group; edition unspecified"), new("known", "recurrent")),
        new("P008", 73, "male", "Prostate cancer", new("known", "II", "TNM stage group; edition unspecified"), new("unknown", null)),
        new("P009", null, "unknown", "Melanoma", new("known", "III", "TNM stage group; edition unspecified"), new("known", "recurrent")),
        new("P010", 61, "female", "Ovarian cancer", new("unknown", null, null), new("unknown", null))
    };

    [Theory]
    [InlineData("/api/patients")]
    [InlineData("/api/patients/P001")]
    [InlineData("/api/patients/missing")]
    public async Task Anonymous_patient_requests_do_not_reveal_record_existence(string path)
    {
        await using var app = new TestApp();
        using var client = app.Browser();
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("authentication_required", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Authenticated_unknown_patient_returns_a_distinct_not_found_error()
    {
        await using var app = new TestApp();
        using var client = app.Browser();
        await AuthTests.Login(client);
        var response = await client.GetAsync("/api/patients/missing");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("patient_not_found", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_real_cookie_with_an_unsupported_role_is_forbidden()
    {
        await using var app = new TestApp();
        using var client = app.Browser();
        // Issue a ticket with the real cookie protector; do not replace authentication.
        var options = app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "unsupported-role-test"), new Claim(ClaimTypes.Role, "Other")
        }, CookieAuthenticationDefaults.AuthenticationScheme));
        var ticket = new AuthenticationTicket(principal, new AuthenticationProperties
        {
            IssuedUtc = app.Clock.GetUtcNow(), ExpiresUtc = app.Clock.GetUtcNow().AddMinutes(30)
        }, CookieAuthenticationDefaults.AuthenticationScheme);
        client.DefaultRequestHeaders.Add("Cookie", $"OncoMatch.Auth={options.TicketDataFormat.Protect(ticket)}");

        var response = await client.GetAsync("/api/patients");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("access_denied", problem.GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("viewer", "ViewerDemo!1")]
    [InlineData("operator", "OperatorDemo!1")]
    public async Task Both_roles_can_browse_the_same_ten_patients_and_details(string username, string password)
    {
        await using var app = new TestApp();
        using var client = app.Browser();
        await AuthTests.Login(client, username, password);

        var response = await client.GetAsync("/api/patients");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        var summaries = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(new[] { "P001", "P002", "P003", "P004", "P005", "P006", "P007", "P008", "P009", "P010" },
            summaries.EnumerateArray().Select(p => p.GetProperty("id").GetString()));
        Assert.False(summaries[0].TryGetProperty("stage", out _));
        Assert.Equal("Acute lymphoblastic leukemia", summaries[0].GetProperty("cancerType").GetString());
        Assert.Equal(JsonValueKind.Null, summaries[8].GetProperty("ageYears").ValueKind);

        var actualSummaries = summaries.Deserialize<PatientSummary[]>(JsonSerializerOptions.Web);
        Assert.Equal(ApprovedPatients.Select(p => new PatientSummary(p.Id, p.AgeYears, p.Sex, p.CancerType)), actualSummaries!);
        foreach (var expected in ApprovedPatients)
        {
            var actual = await client.GetFromJsonAsync<PatientDetails>($"/api/patients/{expected.Id}");
            Assert.Equal(expected, actual);
        }

        var details = await client.GetFromJsonAsync<JsonElement>("/api/patients/P001");
        Assert.Equal(7, details.GetProperty("ageYears").GetInt32());
        Assert.Equal("notApplicable", details.GetProperty("stage").GetProperty("status").GetString());
        var unknown = await client.GetFromJsonAsync<JsonElement>("/api/patients/P003");
        Assert.Equal("unknown", unknown.GetProperty("stage").GetProperty("status").GetString());
        await AuthTests.PostWithToken(client, "/api/auth/logout");
        await AuthTests.Login(client, username, password);
        Assert.Equal(10, (await client.GetFromJsonAsync<JsonElement>("/api/patients")).GetArrayLength());
    }
}
