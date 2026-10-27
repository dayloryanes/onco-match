using Microsoft.AspNetCore.Identity;

namespace OncoMatch.Api.Features.Auth;

public class DemoAccounts
{
    private readonly PasswordHasher<CurrentUser> hasher = new();
    private readonly Dictionary<string, (CurrentUser User, string PasswordHash)> accounts =
        new(StringComparer.OrdinalIgnoreCase);

    public DemoAccounts()
    {
        Add("viewer", "Viewer", "ViewerDemo!1");
        Add("operator", "Operator", "OperatorDemo!1");
    }

    private void Add(string username, string role, string password)
    {
        var user = new CurrentUser(username, role);
        accounts.Add(username, (user, hasher.HashPassword(user, password)));
    }

    public CurrentUser? Authenticate(string username, string password)
    {
        if (!accounts.TryGetValue(username.Trim(), out var account))
        {
            return null;
        }
        return hasher.VerifyHashedPassword(account.User, account.PasswordHash, password)
            == PasswordVerificationResult.Failed ? null : account.User;
    }
}
