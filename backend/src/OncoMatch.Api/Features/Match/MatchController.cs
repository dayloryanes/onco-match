using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OncoMatch.Api.Features.Auth;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OncoMatch.Api.Features.Match;

[ApiController]
[Authorize(Roles = "Viewer,Operator")]
[Route("api/match")]
public class MatchController(MatchStore store, OperationLifetime lifetime, MatchExecution execution) : ControllerBase
{
    private string? Session => User.FindFirstValue("session_id");
    private bool IsOperator => User.IsInRole("Operator");
    [HttpGet("batches")]
    public IActionResult Batches() => Ok(store.Batches(Session, IsOperator));
    [HttpGet("batches/{batchId}")]
    public IActionResult Batch(string batchId) => Read(() => store.Batch(batchId, Session, IsOperator));
    [HttpGet("pairs/{patientId}/{nctId}")]
    public IActionResult Pair(string patientId, string nctId) => Read(() => store.Pair(patientId, nctId));
    [HttpGet("pairs")]
    public IActionResult Pairs()
    {
        var query = Request.Query;
        if (query.Count != 1 || query.Single().Key is not ("patientId" or "nctId") ||
            query.Single().Value.Count != 1 || string.IsNullOrWhiteSpace(query.Single().Value[0]))
            return Error(new(400, "invalid_filter", "Supply exactly one patientId or nctId filter."));
        var filter = query.Single();
        return Read(() => store.Pairs(filter.Key, filter.Value[0]!));
    }

    [HttpPost("operations/{operationId}/cancel")]
    [Authorize(Roles = "Operator")]
    [ServiceFilter(typeof(CsrfFilter))]
    public IActionResult Cancel(string operationId)
    {
        if (Session is null) return Error(new(401, "authentication_required"));
        try { return StatusCode(store.Cancel(operationId, Session)); }
        catch (MatchConflict error) { return Error(error); }
    }
    [HttpPost("batches/{batchId}/operations")]
    [Authorize(Roles = "Operator")]
    [ServiceFilter(typeof(CsrfFilter))]
    public async Task<IActionResult> Start(string batchId, MatchRequest request)
    {
        var session = Session;
        if (session is null) return Error(new(401, "authentication_required"));
        if (request.Action is not ("start" or "continue" or "retryFailed")) return Error(new(400, "invalid_request", "Choose start, continue, or retryFailed."));
        ActiveMatch operation;
        try { operation = store.Admit(batchId, request.Action, request.ExpectedVersion, session, lifetime, HttpContext); }
        catch (MatchConflict error) { return Error(error); }
        using var disconnect = HttpContext.RequestAborted.Register(() => operation.Lease.Stop("disconnected"));
        try
        {
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, User,
                new AuthenticationProperties { IsPersistent = false });
            Response.ContentType = "application/x-ndjson";
            Response.Headers["X-Accel-Buffering"] = "no";
            await Response.StartAsync(HttpContext.RequestAborted);
            await execution.Run(operation, async message =>
            {
                await JsonSerializer.SerializeAsync(Response.Body, message, new JsonSerializerOptions(JsonSerializerDefaults.Web), HttpContext.RequestAborted);
                await Response.WriteAsync("\n", HttpContext.RequestAborted);
                await Response.Body.FlushAsync(HttpContext.RequestAborted);
            });
        }
        catch (OperationCanceledException) when (!Response.HasStarted)
        {
            return Error(new(401, "session_stopped", "The initiating session stopped before work began."));
        }
        finally
        {
            execution.FinishIfNeeded(operation);
            disconnect.Dispose();
            store.Release(operation, lifetime);
        }
        return new EmptyResult();
    }
    private IActionResult Read(Func<object> read)
    {
        try { return Ok(read()); }
        catch (MatchConflict error) { return Error(error); }
    }
    private ObjectResult Error(MatchConflict error) => Problem(statusCode: error.Status, title: error.Message,
        extensions: new Dictionary<string, object?> { ["code"] = error.Code });
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class MatchRequest
{
    [Required] public string Action { get; set; } = "";
    [Required] public string ExpectedVersion { get; set; } = "";
}
