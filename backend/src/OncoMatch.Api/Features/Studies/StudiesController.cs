using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OncoMatch.Api.Features.Auth;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

namespace OncoMatch.Api.Features.Studies;

[ApiController]
[Authorize(Roles = "Viewer,Operator")]
[Route("api/studies")]
public class StudiesController(StudyStore store) : ControllerBase
{
    [HttpGet]
    public IActionResult List() => Ok(store.List());

    [HttpGet("{nctId}")]
    public IActionResult Details(string nctId) => store.Find(nctId) is { } study ? Ok(study) :
        Problem(statusCode: 404, title: "Study not found",
            extensions: new Dictionary<string, object?> { ["code"] = "study_not_found" });
}

[ApiController]
[Authorize(Roles = "Viewer,Operator")]
[Route("api/study-imports")]
public class StudyImportsController(StudyStore store, RegistryClient registry, TimeProvider clock,
    ILogger<StudyImportsController> logger) : ControllerBase
{
    [HttpGet]
    public IActionResult History() => Ok(store.State());

    [HttpPost]
    [Authorize(Roles = "Operator")]
    [ServiceFilter(typeof(CsrfFilter))]
    public async Task<IActionResult> Import(ImportRequest request, CancellationToken cancellationToken)
    {
        var session = User.FindFirstValue("session_id");
        if (string.IsNullOrEmpty(session)) return Error(401, "authentication_required");
        ActiveImport operation;
        try { operation = store.Admit(request.ExpectedVersion, session, clock.GetUtcNow()); }
        catch (ImportConflict conflict) { return Error(409, conflict.Code); }
        using var sessionCancellation = operation.Cancellation;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30), clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, sessionCancellation.Token, deadline.Token);
        try
        {
            var page = await registry.Fetch(operation.Cursor, operation.Known, linked.Token);
            return Ok(store.Complete(page, clock.GetUtcNow(), linked.Token));
        }
        catch (OperationCanceledException)
        {
            var cancelled = cancellationToken.IsCancellationRequested || sessionCancellation.IsCancellationRequested;
            var code = deadline.IsCancellationRequested ? "registry_timeout" : "registry_unavailable";
            store.Fail(cancelled ? "cancelled" : "failed", cancelled ? null : code, clock.GetUtcNow());
            if (cancelled)
            {
                // Cancellation has no success response. History remains the source
                // of truth after navigation, logout or a disconnected caller.
                HttpContext.Abort();
                return new EmptyResult();
            }
            return Error(deadline.IsCancellationRequested ? 504 : 502, code, operation.Id);
        }
        catch (Exception error)
        {
            var code = error switch
            {
                RegistryException registryError => registryError.Code,
                JsonException => "registry_invalid_response",
                HttpRequestException or IOException => "registry_unavailable",
                _ => "server_error"
            };
            logger.LogWarning("Study import {AttemptId} failed: {FailureCode}", operation.Id, code);
            store.Fail("failed", code, clock.GetUtcNow());
            return Error(code == "server_error" ? 500 : 502, code, operation.Id);
        }
    }

    private ObjectResult Error(int status, string code, string? attemptId = null)
    {
        var extensions = new Dictionary<string, object?> { ["code"] = code };
        if (attemptId is not null) extensions["attemptId"] = attemptId;
        return Problem(statusCode: status, title: code switch
        {
            "authentication_required" => "Sign in again before importing studies",
            "import_in_progress" => "An import is already running",
            "import_state_changed" => "Import state changed. Refresh before trying again",
            "imports_exhausted" => "No more registry pages",
            "registry_timeout" => "The registry request timed out",
            "registry_invalid_response" => "The registry returned an invalid response",
            "server_error" => "An unexpected error occurred",
            _ => "The registry is unavailable"
        }, extensions: extensions);
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class ImportRequest
{
    [Required]
    public string ExpectedVersion { get; set; } = "";
}
