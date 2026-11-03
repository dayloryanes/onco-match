using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OncoMatch.Api.Features.Match.Inference;

public sealed class OllamaClient(IHttpClientFactory clients, MatchInferenceOptions options, TimeProvider clock)
{
    private static readonly string Instructions = ReadInstructions();
    private static string ReadInstructions()
    {
        using var stream = typeof(OllamaClient).Assembly.GetManifestResourceStream("OncoMatch.Api.Features.Match.Inference.instructions.txt")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().TrimEnd();
    }

    public async Task CheckAvailability(CancellationToken token)
    {
        try
        {
            var show = await Send("api/show", new JsonObject { ["model"] = options.Model }, token);
            if (show is not JsonObject || show["remote_host"] is not null || show["remote_model"] is not null
                || show["model_info"]?["tokenizer.ggml.model"]?.GetValue<string>() != "gpt2"
                || show["model_info"]?["tokenizer.ggml.pre"]?.GetValue<string>() != "gpt-4o"
                || show["template_source"]?.GetValue<string>() != "harmony"
                || show["thinking"]?["values"] is not JsonArray levels || !levels.Any(level => level?.GetValue<string>() == "low"))
                throw new JsonException("The model does not expose the reviewed local tokenizer/template/low-reasoning profile.");
            if (options.Route == "openai-compatible")
            {
                var required = new Dictionary<string, double> { ["num_ctx"] = 24576, ["top_k"] = 20, ["min_p"] = 0, ["repeat_penalty"] = 1 };
                var found = new HashSet<string>();
                foreach (var line in ContractJson.Text(show["parameters"]).Split('\n'))
                {
                    var fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                    if (fields.Length == 0 || !required.TryGetValue(fields[0], out var expected)) continue;
                    if (fields.Length != 2 || !double.TryParse(fields[1], CultureInfo.InvariantCulture, out var value) || value != expected || !found.Add(fields[0]))
                        throw new JsonException("Incompatible local model preset.");
                }
                if (found.Count != required.Count) throw new JsonException("Missing compatible preset controls.");
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception error) when (error is InferenceFailure or JsonException or InvalidOperationException)
        { throw new InferenceFailure("inferenceUnavailable", "The selected local Ollama model or required preset is unavailable. Check the configured model and route."); }
    }

    public async Task<JsonArray> Assess(MatchPair pair, CancellationToken token)
    {
        var sources = ConditionLabelContract.Sources(pair.Patient, pair.Study);
        var schema = ConditionLabelContract.Schema(sources);
        SchemaGuidance.Apply(schema);
        var system = Instructions + "\nResponse schema:\n" + schema.ToJsonString();
        var user = new JsonObject { ["sources"] = sources }.ToJsonString();
        if ((long)Encoding.UTF8.GetByteCount(system) + Encoding.UTF8.GetByteCount(user) + 4096 + 8192 > 24576)
            throw new InferenceFailure("inputTooLarge", "Input exceeds the conservative context bound; no text was truncated.");
        var body = new JsonObject
        {
            ["model"] = options.Model, ["stream"] = false, ["keep_alive"] = "5m",
            ["messages"] = new JsonArray(new JsonObject { ["role"] = "system", ["content"] = system }, new JsonObject { ["role"] = "user", ["content"] = user })
        };
        if (options.Route == "native")
        {
            body["format"] = schema;
            body["think"] = "low";
            body["options"] = new JsonObject { ["temperature"] = 0.7, ["top_p"] = 0.8, ["top_k"] = 20, ["min_p"] = 0,
                ["seed"] = 42, ["num_ctx"] = 24576, ["num_predict"] = 8192, ["repeat_penalty"] = 1, ["presence_penalty"] = 0 };
        }
        else
        {
            body["response_format"] = new JsonObject { ["type"] = "json_schema", ["json_schema"] = new JsonObject
            { ["name"] = "condition_label_assessment", ["strict"] = true, ["schema"] = schema } };
            body["reasoning_effort"] = "low"; body["max_tokens"] = 8192; body["temperature"] = 0.7;
            body["top_p"] = 0.8; body["seed"] = 42; body["presence_penalty"] = 0;
        }
        try
        {
            var envelope = await Send(options.Route == "native" ? "api/chat" : "v1/chat/completions", body, token);
            JsonNode? message;
            if (options.Route == "native")
            {
                if (envelope["done"]?.GetValue<bool>() != true || ContractJson.Text(envelope["done_reason"]) != "stop") throw new JsonException();
                message = envelope["message"];
            }
            else
            {
                if (envelope["choices"] is not JsonArray choices || choices.Count != 1 || ContractJson.Text(choices[0]?["finish_reason"]) != "stop") throw new JsonException();
                message = choices[0]?["message"];
            }
            if (message is not JsonObject || ContractJson.Text(message["role"]) != "assistant" || message["refusal"] is not null
                || message["tool_calls"] is not null || message["function_call"] is not null) throw new JsonException();
            // Reasoning fields are never copied into application state or logs.
            return ConditionLabelContract.Validate(ContractJson.Text(message["content"]), sources);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException)
        { throw new InferenceFailure("invalidResponse", "The provider returned an incomplete or invalid answer."); }
    }

    private async Task<JsonNode> Send(string path, JsonObject body, CancellationToken token)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(options.RequestTimeoutSeconds), clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, deadline.Token);
        using var client = clients.CreateClient("Ollama");
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(options.BaseUrl.TrimEnd('/') + "/"), path)) { Content = JsonContent.Create(body) };
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token);
            if (!response.IsSuccessStatusCode) throw new InferenceFailure("inferenceError", "Local inference returned an unsuccessful response.");
            return ContractJson.Parse(await response.Content.ReadAsStringAsync(linked.Token));
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        { throw new InferenceFailure("inferenceTimeout", "The configured inference request deadline was reached."); }
        catch (Exception error) when (error is HttpRequestException or IOException)
        { throw new InferenceFailure("inferenceError", "The local inference connection failed."); }
    }
}
