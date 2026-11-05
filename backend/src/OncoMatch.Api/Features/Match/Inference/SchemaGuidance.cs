using System.Text.Json.Nodes;
namespace OncoMatch.Api.Features.Match.Inference;
internal static class SchemaGuidance
{
    internal const string Direct = "Compare the stated supported qualifier with the supplied patient value, independently of branch applicability. Comparable equal values yield alignmentSupported; comparable unequal values yield conflictIdentified. Unknown unsupported selectors cannot make known comparable values undetermined. Use undetermined only for missing supported comparison facts, ambiguous staging context or unestablished classification equivalence. Keep this finding even when the complete label is undetermined.";
    internal const string Whole = "Assess the complete label using its explicit relationships and applicable requirements. An unknown unsupported selector that determines which branch applies yields undetermined, unless an independent applicable conflict establishes conflict. A direct mismatch in a branch with unknown applicability is not an independent applicable conflict. Preserve the direct findings without changing them. Respect mandatory AND and satisfied OR alternatives.";

    internal static void Apply(JsonNode schema)
    {
        schema["$defs"]!["finding"]!["properties"]!["result"]!["description"] = Direct;
        schema["$defs"]!["label"]!["properties"]!["result"]!["description"] = Whole;
    }

}
