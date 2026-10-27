using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OncoMatch.Api.Features.Patients;

[ApiController]
[Authorize(Policy = "ReadPatients")]
[Route("api/patients")]
public class PatientsController(PatientCatalog catalog) : ControllerBase
{
    [HttpGet]
    public IActionResult List()
    {
        return Ok(catalog.Patients.Select(p => new PatientSummary(p.Id, p.AgeYears, p.Sex, p.CancerType)));
    }

    [HttpGet("{id}")]
    public IActionResult Details(string id)
    {
        var patient = catalog.Patients.FirstOrDefault(p => p.Id == id);
        return patient is not null ? Ok(patient) : Problem(statusCode: 404, title: "Patient not found",
            extensions: new Dictionary<string, object?> { ["code"] = "patient_not_found" });
    }
}
