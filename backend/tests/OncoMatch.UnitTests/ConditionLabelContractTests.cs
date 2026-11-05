using System.Text.Json.Nodes;
using OncoMatch.Api.Features.Match.Inference;

namespace OncoMatch.UnitTests;

public class ConditionLabelContractTests
{
    private static readonly JsonObject Sources = new() { ["patient.cancerType"] = "Cancer", ["patient.stage.status"] = "unknown", ["study.conditions.2"] = "Cancer" };
    private static string Answer => """
        {"conditionLabels":[{"conditionLabelId":"study.conditions.2","result":"alignmentSupported","requirementSummary":"Cancer","cancerType":{"result":"alignmentSupported","patientEvidence":[{"sourceId":"patient.cancerType","value":"Cancer"}],"studyEvidence":[{"sourceId":"study.conditions.2","quote":"Cancer"}],"examinedSources":["study.conditions.2"],"explanation":"Cancer type aligns."},"stage":{"result":"notSpecified","patientEvidence":[],"studyEvidence":[],"examinedSources":["study.conditions.2"],"explanation":"Stage is absent from this label."},"diseaseState":{"result":"notSpecified","patientEvidence":[],"studyEvidence":[],"examinedSources":["study.conditions.2"],"explanation":"Disease state is absent from this label."},"unassessedQualifiers":[],"explanation":"Cancer type aligns within scope."}]}
        """;

    [Fact]
    public void Valid_complete_label_preserves_original_source_position()
    {
        var result = ConditionLabelContract.Validate(Answer, Sources);
        Assert.Equal("study.conditions.2", result[0]!["conditionLabelId"]!.GetValue<string>());
        Assert.Equal("conditionLabelAlignmentSupported", ConditionLabelContract.Aggregate(["alignmentSupported"]));
    }

    [Theory]
    [InlineData("cancerType", "satisfied")]
    [InlineData("cancerType", "notSpecified")]
    [InlineData("stage", "excluded")]
    [InlineData("diseaseState", "Alignment supported")]
    public void Unsupported_direct_result_values_produce_the_reported_failure(string aspect, string result)
    {
        var answer = JsonNode.Parse(Answer)!;
        answer["conditionLabels"]![0]![aspect]!["result"] = result;
        var error = Assert.Throws<InferenceFailure>(() => ConditionLabelContract.Validate(answer.ToJsonString(), Sources));
        Assert.Equal("invalidResponse", error.Code);
        Assert.Equal("Invalid direct finding", error.Message);
    }

    [Theory]
    [InlineData("quote", "Invented", "invalidEvidence")]
    [InlineData("source", "study.conditions.0", "invalidEvidence")]
    [InlineData("value", "Other cancer", "invalidEvidence")]
    [InlineData("missing", "", "invalidResponse")]
    [InlineData("duplicate", "", "invalidResponse")]
    [InlineData("extra", "", "invalidResponse")]
    [InlineData("absenceEvidence", "", "invalidEvidence")]
    [InlineData("irrelevant", "", "invalidEvidence")]
    public void Invalid_answers_are_rejected_as_a_whole(string change, string value, string code)
    {
        var answer = JsonNode.Parse(Answer)!;
        var label = answer["conditionLabels"]![0]!;
        switch (change)
        {
            case "quote": label["cancerType"]!["studyEvidence"]![0]!["quote"] = value; break;
            case "source": label["cancerType"]!["studyEvidence"]![0]!["sourceId"] = value; break;
            case "value": label["cancerType"]!["patientEvidence"]![0]!["value"] = value; break;
            case "missing": answer["conditionLabels"]!.AsArray().Clear(); break;
            case "duplicate": answer["conditionLabels"]!.AsArray().Add(label.DeepClone()); break;
            case "extra": label["score"] = 1; break;
            case "absenceEvidence": label["stage"]!["patientEvidence"]!.AsArray().Add(new JsonObject { ["sourceId"] = "patient.cancerType", ["value"] = "Cancer" }); break;
            case "irrelevant": label["cancerType"]!["patientEvidence"] = new JsonArray(new JsonObject { ["sourceId"] = "patient.stage.status", ["value"] = "unknown" }); break;
        }
        Assert.Equal(code, Assert.Throws<InferenceFailure>(() => ConditionLabelContract.Validate(answer.ToJsonString(), Sources)).Code);
    }
}
