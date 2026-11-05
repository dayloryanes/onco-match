using OncoMatch.Api.Features.Match;
using OncoMatch.Api.Features.Patients;
using OncoMatch.Api.Features.Studies;

namespace OncoMatch.UnitTests;

public class StructuredCheckTests
{
    [Theory]
    [InlineData(18, "18 Years", "65 Years", "satisfied")]
    [InlineData(65, "18 Years", "65 Years", "satisfied")]
    [InlineData(17, "18 Years", "65 Years", "excluded")]
    [InlineData(66, "18 Years", "65 Years", "excluded")]
    [InlineData(1, "18 Months", "100 Years", "undetermined")]
    [InlineData(2, "18 Months", "100 Years", "satisfied")]
    [InlineData(1, "365 Days", "100 Years", "satisfied")]
    [InlineData(1, "366 Days", "100 Years", "undetermined")]
    [InlineData(1, "0 Days", "730 Days", "satisfied")]
    [InlineData(1, "0 Days", "729 Days", "undetermined")]
    [InlineData(0, "0 Hours", "8783 Hours", "satisfied")]
    [InlineData(0, "8784 Hours", "100 Years", "excluded")]
    [InlineData(12, "18 Years", "unknown", "excluded")]
    [InlineData(40, "70 Years", "18 Years", "undetermined")]
    [InlineData(40, "1 Year", "11 Months", "undetermined")]
    [InlineData(null, "18 Years", "65 Years", "undetermined")]
    [InlineData(400, "146097 Days", "400 Years", "satisfied")]
    [InlineData(100, "36525 Days", "100 Years", "undetermined")]
    [InlineData(1, "52 Weeks", "1 Year", "satisfied")]
    [InlineData(0, "1 Year", "365 Days", "undetermined")]
    [InlineData(0, "1 Year", "366 Days", "excluded")]
    [InlineData(1, "0 Minutes", "525599 Minutes", "excluded")]
    [InlineData(1, "0 Minutes", "1052639 Minutes", "satisfied")]
    [InlineData(1, "18.5 Years", "100 Years", "undetermined")]
    [InlineData(1, "+0 Years", "100 Years", "undetermined")]
    [InlineData(1, "١ Year", "100 Years", "undetermined")]
    [InlineData(1, "9223372036854775807 Months", "unknown", "excluded")]
    [InlineData(1, "9223372036854775808 Months", "100 Years", "undetermined")]
    [InlineData(2147483647, "0 Minutes", "9223372036854775807 Minutes", "satisfied")]
    [InlineData(1, "  12\tMONTHS  ", "23 months", "satisfied")]
    public void Age_preserves_the_approved_calendar_uncertainty(int? age, string minimum, string maximum, string expected)
    {
        var patient = new PatientDetails("P", age, "Female", "Cancer", new("unknown", null, null), new("unknown", null));
        var study = new StudyDetails("NCT00000001", null, null, null, [], null, minimum, maximum, "ALL", "");
        var checks = StructuredChecks.Assess(patient, study);
        Assert.Equal(expected, checks.Age.Result);
        Assert.Equal("noRestrictionIdentified", checks.Sex.Result);
    }

    [Theory]
    [InlineData("female", "FEMALE", "satisfied")]
    [InlineData("male", "FEMALE", "excluded")]
    [InlineData("unknown", "FEMALE", "undetermined")]
    [InlineData("unknown", "ALL", "noRestrictionIdentified")]
    [InlineData("female", null, "undetermined")]
    public void Sex_is_computed_even_when_age_is_unknown(string sex, string? required, string expected)
    {
        var patient = new PatientDetails("P", null, sex, "Cancer", new("unknown", null, null), new("unknown", null));
        var study = new StudyDetails("NCT00000001", null, null, null, [], null, null, null, required, "");
        Assert.Equal(expected, StructuredChecks.Assess(patient, study).Sex.Result);
        Assert.Equal("undetermined", StructuredChecks.Assess(patient, study).Age.Result);
    }
}
