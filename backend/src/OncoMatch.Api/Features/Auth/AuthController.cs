using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using OncoMatch.Api.Features.Studies;

namespace OncoMatch.Api.Features.Auth;

[ApiController]
[Route("api/auth")]
public class AuthController(DemoAccounts accounts, IAntiforgery antiforgery, StudyStore studies, OperationLifetime lifetime) : ControllerBase
{
    [HttpGet("csrf")]
    public IActionResult Csrf()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new { requestToken = tokens.RequestToken });
    }

    [HttpPost("login")]
    [ServiceFilter(typeof(CsrfFilter))]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var account = accounts.Authenticate(request.Username, request.Password);
        if (account is null)
        {
            return Problem(statusCode: 401, title: "Invalid username or password",
                extensions: new Dictionary<string, object?> { ["code"] = "invalid_credentials" });
        }

        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, account.Username),
            new Claim(ClaimTypes.Role, account.Role),
            new Claim("session_id", Guid.NewGuid().ToString("N"))
        }, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = false });
        return new SessionUserResult(account.Username, account.Role);
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        return new SessionUserResult(User.Identity!.Name!, User.FindFirstValue(ClaimTypes.Role)!);
    }

    [Authorize]
    [HttpPost("extend-session")]
    [ServiceFilter(typeof(CsrfFilter))]
    public async Task<IActionResult> ExtendSession()
    {
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            User, new AuthenticationProperties { IsPersistent = false });
        return new SessionUserResult(User.Identity!.Name!, User.FindFirstValue(ClaimTypes.Role)!);
    }

    [HttpPost("logout")]
    [ServiceFilter(typeof(CsrfFilter))]
    public async Task<IActionResult> Logout()
    {
        lifetime.Logout(User.FindFirstValue("session_id"));
        await studies.CancelSession(User.FindFirstValue("session_id"));
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }
}

public record CurrentUser(string Username, string Role, DateTimeOffset ExpiresAt, DateTimeOffset ServerTime);

public class LoginRequest
{
    [Required]
    public string Username { get; set; } = "";

    [Required]
    public string Password { get; set; } = "";
}
