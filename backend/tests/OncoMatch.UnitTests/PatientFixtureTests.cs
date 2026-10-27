using System.Text;
using System.Text.Json.Nodes;
using OncoMatch.Api.Features.Patients;

namespace OncoMatch.UnitTests;

public class PatientFixtureTests
{
    [Theory]
    [InlineData("negative_age", "ageYears")]
    [InlineData("invalid_sex", "sex")]
    [InlineData("missing_stage_value", "stage")]
    [InlineData("unknown_stage_with_value", "stage")]
    [InlineData("invalid_disease_state", "diseaseState")]
    [InlineData("duplicate_id", "id")]
    [InlineData("wrong_count", "10")]
    public async Task Invalid_fixtures_are_rejected_with_a_useful_location(string fault, string expectedLocation)
    {
        var fixture = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "patients.json")))!.AsArray();
        switch (fault)
        {
            case "negative_age": fixture[0]!["ageYears"] = -1; break;
            case "invalid_sex": fixture[0]!["sex"] = "invented"; break;
            case "missing_stage_value": fixture[4]!["stage"]!["value"] = null; break;
            case "unknown_stage_with_value": fixture[2]!["stage"]!["value"] = "II"; break;
            case "invalid_disease_state": fixture[0]!["diseaseState"]!["value"] = "invented"; break;
            case "duplicate_id": fixture[1]!["id"] = "P001"; break;
            case "wrong_count": fixture.RemoveAt(9); break;
        }
        using var json = new MemoryStream(Encoding.UTF8.GetBytes(fixture.ToJsonString()));

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => PatientCatalog.LoadAsync(json));

        Assert.Contains(expectedLocation, error.Message);
    }
}
