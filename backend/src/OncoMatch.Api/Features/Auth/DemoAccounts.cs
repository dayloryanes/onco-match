using Microsoft.AspNetCore.Identity;

namespace OncoMatch.Api.Features.Auth;

public class DemoAccounts
{
    private readonly PasswordHasher<DemoAccount> hasher = new();
    private readonly Dictionary<string, (DemoAccount User, string PasswordHash)> accounts =
        new(StringComparer.OrdinalIgnoreCase);

    public DemoAccounts()
    {
        Add("viewer", "Viewer", "ViewerDemo1!");
        Add("operator", "Operator", "OperatorDemo1!");
    }

    private void Add(string username, string role, string password)
    {
        var user = new DemoAccount(username, role);
        accounts.Add(username, (user, hasher.HashPassword(user, password)));
    }

    public DemoAccount? Authenticate(string username, string password)
    {
        if (!accounts.TryGetValue(username.Trim(), out var account))
        {
            return null;
        }
        return hasher.VerifyHashedPassword(account.User, account.PasswordHash, password)
            == PasswordVerificationResult.Failed ? null : account.User;
    }
}

public record DemoAccount(string Username, string Role);
