using Scalar.AspNetCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using OncoMatch.Api.Features.Auth;
using Microsoft.AspNetCore.Mvc;
using OncoMatch.Api.Features.Patients;
using OncoMatch.Api.Features.Studies;
using Microsoft.Extensions.Options;
using OncoMatch.Api.Features.Match;
using OncoMatch.Api.Features.Match.Inference;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problem = new ValidationProblemDetails(context.ModelState)
        {
            Status = 400,
            Title = "Check the required fields"
        };
        problem.Extensions["code"] = "invalid_request";
        var result = new BadRequestObjectResult(problem);
        result.ContentTypes.Add("application/problem+json");
        return result;
    };
});
builder.Services.AddProblemDetails();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;
        options.Cookie.Name = "OncoMatch.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.CookieManager = new SessionCookieManager(options);
        options.Events.OnCheckSlidingExpiration = context =>
        {
            var path = context.Request.Path.Value?.TrimEnd('/');
            if (string.Equals(path, "/api/auth/csrf", StringComparison.OrdinalIgnoreCase)
                || string.Equals(path, "/api/auth/extend-session", StringComparison.OrdinalIgnoreCase)
                || (path?.StartsWith("/api/match/batches/", StringComparison.OrdinalIgnoreCase) == true
                    && path.EndsWith("/operations", StringComparison.OrdinalIgnoreCase)))
            {
                // Token acquisition and a rejected extension must not prolong
                // the login. A successful extension explicitly issues its ticket.
                context.ShouldRenew = false;
            }
            return Task.CompletedTask;
        };
        options.Events.OnValidatePrincipal = context =>
        {
            SessionMetadata.Capture(context.HttpContext, new Microsoft.AspNetCore.Authentication.AuthenticationTicket(
                context.Principal!, context.Properties, context.Scheme.Name));
            var session = context.Principal?.FindFirst("session_id")?.Value;
            if (session is not null && context.Properties.ExpiresUtc is { } expiry)
                context.HttpContext.RequestServices.GetRequiredService<OperationLifetime>().Prepare(context.HttpContext, session, expiry);
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
    .Configure<IConfiguration, IHostEnvironment>((options, configuration, environment) =>
    {
        if (environment.IsDevelopment() && configuration.GetValue<bool>("SessionTesting:Enabled"))
            options.ExpireTimeSpan = TimeSpan.FromMinutes(2);
    });
builder.Services.AddAuthorization(options => options.AddPolicy("ReadPatients",
    policy => policy.RequireAuthenticatedUser().RequireRole("Viewer", "Operator")));
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "OncoMatch.Csrf";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});
builder.Services.AddScoped<CsrfFilter>();
builder.Services.AddSingleton<DemoAccounts>();
builder.Services.AddSingleton<StudyStore>();
builder.Services.AddSingleton<MatchStore>();
builder.Services.AddSingleton<MatchExecution>();
builder.Services.AddSingleton<OllamaClient>();
builder.Services.AddHttpClient("Ollama", client => client.Timeout = Timeout.InfiniteTimeSpan)
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddSingleton<OperationLifetime>();
builder.Services.AddSingleton(provider =>
{
    var options = new MatchInferenceOptions();
    try { provider.GetRequiredService<IConfiguration>().GetSection("Match:Inference").Bind(options); }
    catch (InvalidOperationException) { options.BindingError = "Match:Inference contains an invalid value. Check the configuration keys and numeric timeout."; }
    return options;
});
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<RegistryClient>();
builder.Services.AddHttpClient("Registry", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Registry:BaseUrl"] ?? "https://clinicaltrials.gov/api/v2/");
    client.Timeout = Timeout.InfiniteTimeSpan;
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
await using (var patientJson = File.OpenRead(Path.Combine(builder.Environment.ContentRootPath,
    "Features", "Patients", "patients.json")))
{
    builder.Services.AddSingleton(await PatientCatalog.LoadAsync(patientJson));
}
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();
// Resolve once against final startup configuration, without runtime reload.
_ = app.Services.GetRequiredService<MatchInferenceOptions>();
var shortSessionTesting = app.Environment.IsDevelopment()
    && app.Configuration.GetValue<bool>("SessionTesting:Enabled");
var sessionLifetime = app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
    .Get(CookieAuthenticationDefaults.AuthenticationScheme).ExpireTimeSpan;
app.Logger.LogInformation("Session lifetime: {Minutes} minutes ({Mode}). Changing timing requires a fresh login.",
    sessionLifetime.TotalMinutes, shortSessionTesting ? "manual test mode" : "standard mode");
app.UseExceptionHandler(handler => handler.Run(context => Results.Problem(
    statusCode: 500, title: "An unexpected error occurred",
    extensions: new Dictionary<string, object?> { ["code"] = "server_error" }).ExecuteAsync(context)));
app.UseStatusCodePages(context =>
{
    var status = context.HttpContext.Response.StatusCode;
    var code = status switch
    {
        401 => "authentication_required",
        403 => "access_denied",
        404 => "not_found",
        _ => "request_failed"
    };
    return Results.Problem(statusCode: status,
        extensions: new Dictionary<string, object?> { ["code"] = code }).ExecuteAsync(context.HttpContext);
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.Headers.CacheControl = "no-store";
    }
    try { await next(context); }
    finally { context.RequestServices.GetRequiredService<OperationLifetime>().EndRequest(context); }
});

// Register before authentication: OnStarting callbacks run in reverse order,
// so the cookie handler finishes any actual renewal before metadata is emitted.
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        SessionMetadata.WriteHeaders(context);
        return Task.CompletedTask;
    });
    await next(context);
});
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
