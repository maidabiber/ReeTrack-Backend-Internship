using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Google.Apis.Auth;
using Microsoft.Extensions.Options;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Options;

namespace ReeTrack.Infrastructure.Auth;

public class GoogleCodeExchanger : IGoogleCodeExchanger
{
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";

    private readonly HttpClient _httpClient;
    private readonly GoogleAuthOptions _options;

    public GoogleCodeExchanger(HttpClient httpClient, IOptions<GoogleAuthOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<GoogleTokenPayload> ExchangeAsync(string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId))
            throw new InvalidOperationException("Google ClientId is not configured.");

        if (string.IsNullOrWhiteSpace(_options.ClientSecret))
            throw new InvalidOperationException("Google ClientSecret is not configured.");

        var tokenResponse = await ExchangeCodeForTokensAsync(code, cancellationToken);

        if (string.IsNullOrWhiteSpace(tokenResponse.IdToken))
            throw new AuthException("Google did not return an ID token.", 401, ErrorCode.Unauthorized);

        return await ValidateIdTokenAsync(tokenResponse.IdToken);
    }

    private async Task<GoogleTokenResponse> ExchangeCodeForTokensAsync(string code, CancellationToken cancellationToken)
    {
        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["redirect_uri"] = _options.RedirectUri,
            ["grant_type"] = "authorization_code"
        });

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsync(TokenEndpoint, formContent, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new AuthException($"Failed to contact Google token endpoint: {ex.Message}", 401, ErrorCode.Unauthorized);
        }

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new AuthException($"Google token exchange failed: {error}", 401, ErrorCode.Unauthorized);
        }

        var tokenResponse = await response.Content.ReadFromJsonAsync<GoogleTokenResponse>(cancellationToken: cancellationToken);
        if (tokenResponse is null)
            throw new AuthException("Google returned an empty token response.", 401, ErrorCode.Unauthorized);

        return tokenResponse;
    }

    private async Task<GoogleTokenPayload> ValidateIdTokenAsync(string idToken)
    {
        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = [_options.ClientId]
                });

            if (string.IsNullOrWhiteSpace(payload.Subject))
                throw new AuthException("Google token is missing a subject claim.", 401, ErrorCode.Unauthorized);

            if (string.IsNullOrWhiteSpace(payload.Email))
                throw new AuthException("Google token is missing an email claim.", 401, ErrorCode.Unauthorized);

            if (!payload.EmailVerified)
                throw new AuthException("Google account email is not verified.", 401, ErrorCode.Unauthorized);

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
            throw new AuthException("Invalid or expired Google ID token.", 401, ErrorCode.Unauthorized);
        }
    }

    private sealed class GoogleTokenResponse
    {
        [JsonPropertyName("id_token")]
        public string? IdToken { get; init; }

        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }
    }
}
