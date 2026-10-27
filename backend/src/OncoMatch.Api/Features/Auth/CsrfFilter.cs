using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace OncoMatch.Api.Features.Auth;

public class CsrfFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            var problem = new ProblemDetails { Status = 400, Title = "Refresh the page and try again" };
            problem.Extensions["code"] = "csrf_invalid";
            var result = new ObjectResult(problem) { StatusCode = 400 };
            result.ContentTypes.Add("application/problem+json");
            context.Result = result;
        }
    }
}
