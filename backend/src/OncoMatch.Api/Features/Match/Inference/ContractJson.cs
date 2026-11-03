using System.Text.Json;
using System.Text.Json.Nodes;

namespace OncoMatch.Api.Features.Match.Inference;

public sealed class InferenceFailure(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

internal static class ContractJson
{
    public static string Text(JsonNode? node) => node?.GetValue<string>() ?? throw new JsonException("Expected string");
    public static JsonNode Parse(string text)
    {
        using var document = JsonDocument.Parse(text);
        Check(document.RootElement);
        return JsonNode.Parse(text) ?? throw new JsonException("Expected JSON value");
    }
    private static void Check(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new JsonException("Duplicate JSON property");
                Check(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array) foreach (var item in element.EnumerateArray()) Check(item);
    }
    public static void ExactKeys(JsonNode node, params string[] expected)
    {
        if (node is not JsonObject obj || !obj.Select(p => p.Key).Order().SequenceEqual(expected.Order()))
            throw new InferenceFailure("invalidResponse", "Missing or unexpected response fields.");
    }
}
