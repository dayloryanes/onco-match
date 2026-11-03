using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace OncoMatch.Api.Features.Auth;

// Per-response metadata only. Cookie validation remains the authentication authority.
public record SessionMetadata(DateTimeOffset ExpiresAt, DateTimeOffset ServerTime)
{
    private static readonly object Key = new();

    public static void Capture(HttpContext context, AuthenticationTicket ticket)
    {
        if (ticket.Properties.ExpiresUtc is { } expiry)
            context.Items[Key] = expiry;
    }

    public static SessionMetadata? Read(HttpContext context)
    {
        if (context.Items[Key] is not DateTimeOffset expiry) return null;
        return new(expiry, context.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow());
    }

    public static void WriteHeaders(HttpContext context)
    {
        if (!context.RequestServices.GetRequiredService<OperationLifetime>().ResponseAllowed(context))
        {
            // Sign-in queues headers before StartAsync. Suppress that queued
            // renewal if logout/expiry won before the response publication point.
            context.Response.Headers.SetCookie = new Microsoft.Extensions.Primitives.StringValues(
                context.Response.Headers.SetCookie.Where(cookie => cookie is not null
                    && !System.Text.RegularExpressions.Regex.IsMatch(cookie, @"^OncoMatch\.Auth(?:C[0-9]+)?=")).ToArray());
            context.Response.Headers.Remove("X-Session-Renewed");
            context.Items.Remove(Key);
            return;
        }
        var metadata = Read(context);
        if (metadata is null) return;
        context.Response.Headers["X-Session-Expires-At"] = metadata.ExpiresAt.ToString("O", CultureInfo.InvariantCulture);
        context.Response.Headers["X-Server-Time"] = metadata.ServerTime.ToString("O", CultureInfo.InvariantCulture);
    }
}

// Observe the actual protected ticket emitted by the framework, including sliding
// renewal at response start. Delegate cookie chunking/security behavior unchanged.
public sealed class SessionCookieManager(CookieAuthenticationOptions authenticationOptions) : ICookieManager
{
    private readonly ChunkingCookieManager cookies = new();

    public string? GetRequestCookie(HttpContext context, string key) => cookies.GetRequestCookie(context, key);

    public void AppendResponseCookie(HttpContext context, string key, string? value, CookieOptions options)
    {
        context.RequestServices.GetRequiredService<OperationLifetime>().PublishCookie(context, () =>
        {
            cookies.AppendResponseCookie(context, key, value, options);
            var ticket = authenticationOptions.TicketDataFormat.Unprotect(value);
            if (ticket is not null)
            {
                SessionMetadata.Capture(context, ticket);
                var session = ticket.Principal.FindFirst("session_id")?.Value;
                if (session is not null && ticket.Properties.ExpiresUtc is { } expiry)
                    context.RequestServices.GetRequiredService<OperationLifetime>().Renew(session, expiry);
                context.Response.Headers["X-Session-Renewed"] = "true";
            }
        });
    }

    public void DeleteCookie(HttpContext context, string key, CookieOptions options) => cookies.DeleteCookie(context, key, options);
}

// Sliding cookies are issued in OnStarting. Start headers before serializing the
// small session response so its JSON and headers describe the same issued ticket.
public sealed class SessionUserResult(string username, string role) : IActionResult
{
    public async Task ExecuteResultAsync(ActionContext context)
    {
        var response = context.HttpContext.Response;
        response.ContentType = "application/json; charset=utf-8";
        await response.StartAsync(context.HttpContext.RequestAborted);
        var metadata = SessionMetadata.Read(context.HttpContext)!;
        await System.Text.Json.JsonSerializer.SerializeAsync(response.Body,
            new CurrentUser(username, role, metadata.ExpiresAt, metadata.ServerTime),
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web),
            context.HttpContext.RequestAborted);
    }
}
