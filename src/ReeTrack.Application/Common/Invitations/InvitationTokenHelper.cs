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
