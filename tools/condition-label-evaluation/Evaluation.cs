using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;

internal sealed class EvaluationFailure(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

internal static class Evaluation
{
    internal static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    internal static string Text(JsonNode? value) => value?.GetValue<string>() ?? throw new InvalidDataException("Expected a string");
    internal static JsonNode Read(string path) => Parse(File.ReadAllText(path));
    internal static JsonNode Parse(string text)
    {
        using var document = JsonDocument.Parse(text);
        CheckDuplicates(document.RootElement);
        return JsonNode.Parse(text) ?? throw new JsonException("JSON null is not an object");
    }

    private static void CheckDuplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new JsonException($"Duplicate property: {property.Name}");
                CheckDuplicates(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) CheckDuplicates(item);
    }

    internal static void ExactKeys(JsonNode node, params string[] keys)
    {
        if (node is not JsonObject obj || !obj.Select(p => p.Key).Order().SequenceEqual(keys.Order()))
            throw new EvaluationFailure("invalidResponse", "Missing or unexpected response fields");
    }

    internal static JsonObject Sizing(JsonObject request, JsonNode config)
    {
        // Reviewed byte-level BPE models: decoded message bytes bound content tokens.
        // The schema embedded in the system message counts once. API-only format/schema,
        // HTTP JSON escaping, and inference options are not rendered into the prompt.
        // This bound covers only the reviewed two-message, text-only, no-tools path.
        if (request["messages"] is not JsonArray messages || messages.Count != 2 || request["tools"] is not null)
            throw new InvalidDataException("Sizing requires two text-only messages and no tools");
        var contentBytes = 0;
        for (var i = 0; i < messages.Count; i++)
        {
            var message = messages[i] ?? throw new InvalidDataException("Missing message");
            ExactKeys(message, "role", "content");
            if (Text(message["role"]) != (i == 0 ? "system" : "user"))
                throw new InvalidDataException("Sizing requires system then user messages");
            contentBytes = checked(contentBytes + Encoding.UTF8.GetByteCount(Text(message["content"])));
        }
        var framing = config["framingReserveTokens"]!.GetValue<int>();
        var output = config["maxOutputTokens"]!.GetValue<int>();
        if (framing <= 0 || output <= 0) throw new InvalidDataException("Sizing reserves must be positive");
        var bound = checked(contentBytes + framing + output);
        return new JsonObject
        {
            ["requestUtf8Bytes"] = Encoding.UTF8.GetByteCount(request.ToJsonString()),
            ["messageContentUtf8Bytes"] = contentBytes, ["framingReserveTokens"] = framing,
            ["maxOutputTokens"] = output, ["conservativeTotalTokenBound"] = bound,
            ["contextTokens"] = config["contextTokens"]!.DeepClone(),
            ["withinConservativeBound"] = bound <= config["contextTokens"]!.GetValue<int>(),
            ["method"] = "Decoded message-content UTF-8 byte bound plus reviewed chat framing reserve and output allowance; API-only fields excluded; not an exact token count or VRAM estimate"
        };
    }

    internal static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    internal static (string Content, JsonObject Metrics) Unpack(string response, string route)
    {
        var envelope = Parse(response);
        if (route == "native")
        {
            if (envelope["done"]?.GetValue<bool>() != true || Text(envelope["done_reason"]) != "stop")
                throw new EvaluationFailure("invalidResponse", "Incomplete or output-limited native response");
            var metrics = NativeMetrics(envelope);
            return (Text(envelope["message"]?["content"]), metrics);
        }
        if (envelope["choices"] is not JsonArray choices || choices.Count != 1 || Text(choices[0]?["finish_reason"]) != "stop")
            throw new EvaluationFailure("invalidResponse", "Incomplete/refused/ambiguous compatible response");
        return (Text(choices[0]!["message"]?["content"]), new JsonObject
        { ["usage"] = envelope["usage"]?.DeepClone(), ["loadTimingAvailable"] = false });
    }

    internal static JsonObject NativeMetrics(JsonNode envelope)
    {
        var metrics = new JsonObject();
        foreach (var key in new[] { "total_duration", "load_duration", "prompt_eval_count", "prompt_eval_duration", "eval_count", "eval_duration" })
            metrics[key] = envelope[key]?.DeepClone();
        // Retain only whether a separated reasoning field was observed, never its text.
        metrics["reasoningObserved"] = envelope["message"]?["thinking"] is JsonValue value
            && value.TryGetValue<string>(out var reasoning) && !string.IsNullOrWhiteSpace(reasoning);
        return metrics;
    }

    internal static async Task<string> SendAsync(HttpClient client, string path, JsonNode body, TimeSpan timeout, CancellationToken stop)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stop);
        deadline.CancelAfter(timeout);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path)
            { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
            using var response = await client.SendAsync(request, deadline.Token);
            if (!response.IsSuccessStatusCode) throw new EvaluationFailure("inferenceError", $"HTTP {(int)response.StatusCode}");
            return await response.Content.ReadAsStringAsync(deadline.Token);
        }
        catch (OperationCanceledException) when (!stop.IsCancellationRequested)
        { throw new EvaluationFailure("inferenceTimeout", "Request deadline exceeded; server cancellation is not instantaneous"); }
    }
}
