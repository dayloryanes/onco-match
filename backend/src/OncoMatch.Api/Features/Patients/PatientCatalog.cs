using System.Text.Json;
using System.Text.Json.Serialization;

namespace OncoMatch.Api.Features.Patients;

public record PatientSummary(string Id, int? AgeYears, string Sex, string? CancerType);
public record StageInfo(string Status, string? Value, string? ClassificationSystem);
public record DiseaseStateInfo(string Status, string? Value);
public record PatientDetails(string Id, int? AgeYears, string Sex, string? CancerType,
    StageInfo Stage, DiseaseStateInfo DiseaseState);

public class PatientCatalog
{
    public IReadOnlyList<PatientDetails> Patients { get; }

    private PatientCatalog(List<PatientDetails> patients)
    {
        Patients = patients.OrderBy(p => p.Id, StringComparer.Ordinal).ToList().AsReadOnly();
    }

    public static async Task<PatientCatalog> LoadAsync(Stream json, CancellationToken cancellationToken = default)
    {
        List<PatientDetails>? patients;
        try
        {
            patients = await JsonSerializer.DeserializeAsync<List<PatientDetails>>(json,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    RespectRequiredConstructorParameters = true,
                    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
                }, cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Patient fixture JSON is invalid at {exception.Path}: {exception.Message}", exception);
        }
        if (patients is null || patients.Count != 10)
        {
            throw new InvalidDataException("Patient fixture must contain exactly 10 records");
        }
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var patient in patients)
        {
            if (patient is null || string.IsNullOrWhiteSpace(patient.Id) || !identifiers.Add(patient.Id))
            {
                throw new InvalidDataException($"Patient fixture has a missing or duplicate id: {patient?.Id}");
            }
            void Invalid(string field) => throw new InvalidDataException($"Patient {patient.Id}: invalid {field}");
            if (patient.AgeYears < 0) Invalid("ageYears");
            if (patient.Sex is not ("female" or "male" or "unknown")) Invalid("sex");
            if (patient.CancerType is not null && string.IsNullOrWhiteSpace(patient.CancerType)) Invalid("cancerType");
            if (patient.Stage is null || !ValidStatus(patient.Stage.Status)) Invalid("stage.status");
            var stage = patient.Stage!;
            if (stage.Status == "known")
            {
                if (string.IsNullOrWhiteSpace(stage.Value)) Invalid("stage.value");
                if (stage.ClassificationSystem is not null && string.IsNullOrWhiteSpace(stage.ClassificationSystem))
                    Invalid("stage.classificationSystem");
            }
            else if (stage.Value is not null || stage.ClassificationSystem is not null) Invalid("stage");

            if (patient.DiseaseState is null || !ValidStatus(patient.DiseaseState.Status)) Invalid("diseaseState.status");
            var diseaseState = patient.DiseaseState!;
            if (diseaseState.Status == "known")
            {
                if (diseaseState.Value is not ("newlyDiagnosed" or "recurrent" or "refractory")) Invalid("diseaseState.value");
            }
            else if (diseaseState.Value is not null) Invalid("diseaseState.value");
        }
        return new PatientCatalog(patients);
    }

    private static bool ValidStatus(string status) => status is "known" or "unknown" or "notApplicable";
}
