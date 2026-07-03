using Google.Apis.Auth;
using Microsoft.Extensions.Options;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Options;

namespace ReeTrack.Infrastructure.Auth;

public class GoogleTokenValidator : IGoogleTokenValidator
{
    private readonly GoogleAuthOptions _options;

    public GoogleTokenValidator(IOptions<GoogleAuthOptions> options)
    {
        _options = options.Value;
    }

    public async Task<GoogleTokenPayload> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId))
            throw new InvalidOperationException("Google ClientId is not configured.");

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = [_options.ClientId]
                });

            if (string.IsNullOrWhiteSpace(payload.Subject))
                throw new AuthException("Google token is missing a subject claim.");

            if (string.IsNullOrWhiteSpace(payload.Email))
                throw new AuthException("Google token is missing an email claim.");

            if (!payload.EmailVerified)
                throw new AuthException("Google account email is not verified.");

            return new GoogleTokenPayload
            {
                Subject = payload.Subject,
                Email = payload.Email,
                EmailVerified = payload.EmailVerified,
                Name = payload.Name,
                Picture = payload.Picture
            };
        }
        catch (InvalidJwtException)
        {
            throw new AuthException("Invalid or expired Google ID token.");
        }
    }
}
