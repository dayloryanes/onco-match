using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace OncoMatch.Api.Features.Studies;

public record RegistryPage(StoredStudy[] Studies, string[] Duplicates, Rejection[] Rejections,
    int FetchedCount, string? NextToken);
public class RegistryException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

public class RegistryClient(IHttpClientFactory clients)
{
    public async Task<RegistryPage> Fetch(string? cursor, HashSet<string> known, CancellationToken token)
    {
        var query = new Dictionary<string, string?>
        {
            ["format"] = "json", ["query.cond"] = "cancer",
            ["filter.overallStatus"] = "RECRUITING,NOT_YET_RECRUITING",
            ["filter.advanced"] = "AREA[StudyType](INTERVENTIONAL OR OBSERVATIONAL)",
            ["sort"] = "@relevance", ["pageSize"] = "10"
        };
        if (cursor is not null) query["pageToken"] = cursor;
        using var client = clients.CreateClient("Registry");
        using var response = await client.GetAsync(QueryHelpers.AddQueryString("studies", query),
            HttpCompletionOption.ResponseHeadersRead, token);
        if (!response.IsSuccessStatusCode) throw new RegistryException("registry_unavailable");
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: token);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("studies", out var records) ||
            records.ValueKind != JsonValueKind.Array) throw new RegistryException("registry_invalid_response");
        string? next = null;
        if (root.TryGetProperty("nextPageToken", out var nextValue))
        {
            if (nextValue.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(nextValue.GetString()))
                throw new RegistryException("registry_invalid_response");
            next = nextValue.GetString();
        }
        var accepted = new List<StoredStudy>();
        var duplicates = new List<string>();
        var rejected = new List<Rejection>();
        var position = 0;
        foreach (var record in records.EnumerateArray())
        {
            token.ThrowIfCancellationRequested();
            position++;
            string? id = null;
            try
            {
                if (record.ValueKind != JsonValueKind.Object) throw new RecordException("invalid_record");
                var protocol = Module(record, "protocolSection");
                var identification = Module(protocol, "identificationModule");
                id = Text(identification, "nctId");
                if (!ValidId(id)) { id = null; throw new RecordException("invalid_nct_id"); }
                var title = Text(identification, "briefTitle");
                var status = Text(Module(protocol, "statusModule"), "overallStatus");
                var type = Text(Module(protocol, "designModule"), "studyType");
                var summary = Text(Module(protocol, "descriptionModule"), "briefSummary");
                var eligibility = Module(protocol, "eligibilityModule");
                var minimum = Text(eligibility, "minimumAge");
                var maximum = Text(eligibility, "maximumAge");
                var sex = Text(eligibility, "sex");
                var conditionsModule = Module(protocol, "conditionsModule");
                string[]? conditions = null;
                if (conditionsModule.ValueKind == JsonValueKind.Object && conditionsModule.TryGetProperty("conditions", out var values) && values.ValueKind != JsonValueKind.Null)
                {
                    if (values.ValueKind != JsonValueKind.Array || values.EnumerateArray().Any(v => v.ValueKind != JsonValueKind.String))
                        throw new RecordException("invalid_field_shape");
                    conditions = values.EnumerateArray().Select(v => v.GetString()!).ToArray();
                }
                if (!known.Add(id!)) { duplicates.Add(id!); continue; }
                accepted.Add(new(new(id!, title, status, type, conditions, summary, minimum, maximum, sex,
                    "https://clinicaltrials.gov/study/" + id), record.Clone()));
            }
            catch (RecordException error)
            {
                var reason = error.Code switch
                {
                    "invalid_record" => "Record must be an object.",
                    "invalid_nct_id" => "NCT identifier must contain NCT followed by eight ASCII digits.",
                    _ => "A selected field or module has an incompatible shape."
                };
                rejected.Add(new(position, id, error.Code, reason));
            }
        }
        return new(accepted.ToArray(), duplicates.ToArray(), rejected.ToArray(), position, next);
    }

    private static bool ValidId(string? id) => id is { Length: 11 } && id.StartsWith("NCT", StringComparison.Ordinal)
        && id.AsSpan(3).ContainsAnyExceptInRange('0', '9') == false;

    private static JsonElement Module(JsonElement parent, string name)
    {
        if (parent.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null || !parent.TryGetProperty(name, out var value)) return default;
        if (value.ValueKind is not (JsonValueKind.Object or JsonValueKind.Null)) throw new RecordException("invalid_field_shape");
        return value;
    }

    private static string? Text(JsonElement parent, string name)
    {
        if (parent.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null || !parent.TryGetProperty(name, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Null) return null;
        if (value.ValueKind != JsonValueKind.String) throw new RecordException("invalid_field_shape");
        return value.GetString();
    }
    private class RecordException(string code) : Exception { public string Code { get; } = code; }
}
