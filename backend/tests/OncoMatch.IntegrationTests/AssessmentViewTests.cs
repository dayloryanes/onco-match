using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OncoMatch.IntegrationTests;

public class AssessmentViewTests
{
    [Theory]
    [InlineData("viewer", "ViewerDemo1!")]
    [InlineData("operator", "OperatorDemo1!")]
    public async Task Perspectives_read_the_same_saved_pairs_in_source_order(string username, string password)
    {
        await using var app = new TestApp();
        using var writer = app.Browser();
        using var reader = app.Browser();
        await AuthTests.Login(writer, "operator", "OperatorDemo1!");
        await AuthTests.Login(reader, username, password);
        var patients = await reader.GetFromJsonAsync<JsonElement>("/api/patients");
        var patientId = patients[0].GetProperty("id").GetString();
        var empty = await reader.GetFromJsonAsync<JsonElement>($"/api/match/pairs?patientId={patientId}");
        Assert.Empty(empty.EnumerateArray());
        // No condition text: deterministic completion without any model request.
        string NoConditions(string id)
        {
            var record = JsonNode.Parse(StudyTests.Record(id))!;
            record["protocolSection"]!["conditionsModule"]!["conditions"] = new JsonArray();
            return record.ToJsonString();
        }
        StudyTests.Page(app, NoConditions("NCT00000002") + "," + NoConditions("NCT00000001"));
        (await StudyTests.Import(writer, StudyTests.Version(await StudyTests.State(writer)))).EnsureSuccessStatusCode();
        var batch = (await writer.GetFromJsonAsync<JsonElement>("/api/match/batches")).GetProperty("batches")[0];
        var batchId = batch.GetProperty("batchId").GetString();
        using var run = await AuthTests.PostWithToken(writer, $"/api/match/batches/{batchId}/operations",
            new { action = "start", expectedVersion = batch.GetProperty("version").GetString() });
        run.EnsureSuccessStatusCode();
        Assert.Contains("operationFinished", await run.Content.ReadAsStringAsync());
        var dashboard = await reader.GetFromJsonAsync<JsonElement>($"/api/match/batches/{batchId}");
        var patientPairs = await reader.GetFromJsonAsync<JsonElement>($"/api/match/pairs?patientId={patientId}");
        Assert.Equal(new[] { "NCT00000002", "NCT00000001" }, patientPairs.EnumerateArray().Select(p => p.GetProperty("nctId").GetString()));
        var studyPairs = await reader.GetFromJsonAsync<JsonElement>("/api/match/pairs?nctId=NCT00000002");
        Assert.Equal(patients.EnumerateArray().Select(p => p.GetProperty("id").GetString()), studyPairs.EnumerateArray().Select(p => p.GetProperty("patientId").GetString()));
        Assert.Equal(dashboard.GetProperty("pairs")[0].GetRawText(), patientPairs[0].GetRawText());
        Assert.Equal(patientPairs[0].GetRawText(), studyPairs[0].GetRawText());
        var detail = await reader.GetFromJsonAsync<JsonElement>($"/api/match/pairs/{patientId}/NCT00000002");
        Assert.Equal("completed", detail.GetProperty("state").GetString());
        Assert.Equal(patientId, detail.GetProperty("inputSnapshot").GetProperty("patient").GetProperty("id").GetString());
        Assert.Single(detail.GetProperty("attemptHistory").EnumerateArray());
        Assert.Empty(app.Ollama.Requests);
    }

    [Theory]
    [InlineData("", 400)]
    [InlineData("?patientId=P001&nctId=NCT00000001", 400)]
    [InlineData("?patientId=P001&state=completed", 400)]
    [InlineData("?patientId=P001&patientId=P002", 400)]
    [InlineData("?patientId=", 400)]
    [InlineData("?nctId=", 400)]
    [InlineData("?patientId=unknown", 404)]
    [InlineData("?nctId=NCT99999999", 404)]
    public async Task Pair_filters_reject_invalid_or_unknown_resources(string query, int status)
    {
        await using var app = new TestApp();
        using var client = app.Browser();
        await AuthTests.Login(client);
        using var response = await client.GetAsync("/api/match/pairs" + query);
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Empty(app.Ollama.Requests);
    }

    [Fact]
    public async Task Pair_lists_require_a_valid_session()
    {
        await using var app = new TestApp();
        using var client = app.Browser();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/match/pairs?patientId=P001")).StatusCode);
        await AuthTests.Login(client);
        app.Clock.Advance(TimeSpan.FromMinutes(31));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/match/pairs?patientId=P001")).StatusCode);
    }
}
