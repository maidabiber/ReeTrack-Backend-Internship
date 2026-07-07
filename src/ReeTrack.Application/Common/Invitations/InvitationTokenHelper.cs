using System.Security.Cryptography;
using System.Text;

namespace ReeTrack.Application.Common.Invitations;

public static class InvitationTokenHelper
{
    public static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static string HashToken(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string NormalizeEmail(string email) =>
        email.Trim().ToLowerInvariant();

    /// <summary>Returns the lowercased domain of an email, or empty if it has none.</summary>
    public static string GetEmailDomain(string email)
    {
        var atIndex = email.LastIndexOf('@');
        return atIndex < 0 || atIndex == email.Length - 1
            ? string.Empty
            : email[(atIndex + 1)..].Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Whether an email is allowed given the configured domains. An empty list
    /// allows everything so the restriction stays opt-in. Leading '@' on a
    /// configured domain is tolerated (e.g. "@reeinvent.com").
    /// </summary>
    public static bool IsEmailDomainAllowed(string email, IReadOnlyCollection<string> allowedDomains)
    {
        if (allowedDomains.Count == 0)
            return true;

        var domain = GetEmailDomain(email);
        if (domain.Length == 0)
            return false;

        return allowedDomains.Any(allowed =>
            string.Equals(
                allowed.Trim().TrimStart('@'),
                domain,
                StringComparison.OrdinalIgnoreCase));
    }

    public static string DisplayNameFromEmail(string email)
    {
        var localPart = email.Split('@')[0];
        var words = localPart
            .Replace('.', ' ')
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (words.Length == 0)
            return localPart;

        return string.Join(' ', words.Select(static word =>
            word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..]));
    }
}
