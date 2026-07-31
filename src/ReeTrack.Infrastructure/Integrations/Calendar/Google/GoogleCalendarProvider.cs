using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Microsoft.Extensions.Options;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Options;
using ReeTrack.Application.Integrations.Calendar;
using ReeTrack.Application.Integrations.Calendar.Models;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Infrastructure.Integrations.Calendar.Google;

public class GoogleCalendarProvider : ICalendarProvider
{
    private const string AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string UserInfoEndpoint = "https://www.googleapis.com/oauth2/v2/userinfo";
    private const string CalendarReadonlyScope = "https://www.googleapis.com/auth/calendar.readonly";

    private readonly HttpClient _httpClient;
    private readonly GoogleAuthOptions _options;

    public GoogleCalendarProvider(HttpClient httpClient, IOptions<GoogleAuthOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public CalendarProviderType ProviderType => CalendarProviderType.Google;

    public string BuildAuthorizationUrl(string state)
    {
        EnsureConfigured();

        var query = string.Join("&", new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = _options.CalendarRedirectUri,
            ["response_type"] = "code",
            ["scope"] = CalendarReadonlyScope,
            ["state"] = state,
            ["access_type"] = "offline",
            ["prompt"] = "consent"
        }.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));

        return $"{AuthorizationEndpoint}?{query}";
    }

    public async Task<OAuthTokenSet> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var tokenResponse = await RequestTokensAsync(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["redirect_uri"] = _options.CalendarRedirectUri,
            ["grant_type"] = "authorization_code"
        }, cancellationToken);

        return await BuildTokenSetAsync(tokenResponse, cancellationToken);
    }

    public async Task<OAuthTokenSet> RefreshAccessTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var tokenResponse = await RequestTokensAsync(new Dictionary<string, string>
        {
            ["refresh_token"] = refreshToken,
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["grant_type"] = "refresh_token"
        }, cancellationToken);

        return await BuildTokenSetAsync(tokenResponse, cancellationToken, refreshToken);
    }

    public async Task<IReadOnlyList<ExternalCalendarEvent>> FetchEventsAsync(
        string accessToken,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        var credential = GoogleCredential.FromAccessToken(accessToken);
        var service = new CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "ReeTrack"
        });

        var request = service.Events.List("primary");
        request.TimeMinDateTimeOffset = new DateTimeOffset(fromUtc, TimeSpan.Zero);
        request.TimeMaxDateTimeOffset = new DateTimeOffset(toUtc, TimeSpan.Zero);
        request.SingleEvents = true;
        request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;
        request.ShowDeleted = false;

        var events = new List<ExternalCalendarEvent>();
        string? pageToken = null;

        do
        {
            request.PageToken = pageToken;
            var response = await request.ExecuteAsync(cancellationToken);

            if (response.Items is not null)
            {
                foreach (var item in response.Items)
                    events.Add(MapEvent(item));
            }

            pageToken = response.NextPageToken;
        } while (!string.IsNullOrEmpty(pageToken));

        return events;
    }

    private async Task<GoogleOAuthTokenResponse> RequestTokensAsync(
        Dictionary<string, string> form,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();

        using var formContent = new FormUrlEncodedContent(form);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsync(TokenEndpoint, formContent, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new CalendarIntegrationException($"Failed to contact Google token endpoint: {ex.Message}", 502, ErrorCode.ServiceUnavailable);
        }

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new CalendarIntegrationException($"Google token request failed: {error}", 502, ErrorCode.ServiceUnavailable);
        }

        var tokenResponse = await response.Content.ReadFromJsonAsync<GoogleOAuthTokenResponse>(cancellationToken: cancellationToken);
        if (tokenResponse is null || string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
            throw new CalendarIntegrationException("Google returned an invalid token response.", 502, ErrorCode.ServiceUnavailable);

        return tokenResponse;
    }

    private async Task<OAuthTokenSet> BuildTokenSetAsync(
        GoogleOAuthTokenResponse tokenResponse,
        CancellationToken cancellationToken,
        string? existingRefreshToken = null)
    {
        var refreshToken = !string.IsNullOrWhiteSpace(tokenResponse.RefreshToken)
            ? tokenResponse.RefreshToken
            : existingRefreshToken;

        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new CalendarIntegrationException("Google did not return a refresh token. Reconnect with consent.", 400, ErrorCode.Validation);

        var expiresAtUtc = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn > 0 ? tokenResponse.ExpiresIn : 3600);
        var accountId = await TryGetUserEmailAsync(tokenResponse.AccessToken!, cancellationToken);

        return new OAuthTokenSet
        {
            AccessToken = tokenResponse.AccessToken!,
            RefreshToken = refreshToken,
            ExpiresAtUtc = expiresAtUtc,
            ProviderAccountId = accountId
        };
    }

    private async Task<string?> TryGetUserEmailAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, UserInfoEndpoint);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            var userInfo = await response.Content.ReadFromJsonAsync<GoogleUserInfoResponse>(cancellationToken: cancellationToken);
            return userInfo?.Email;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private static ExternalCalendarEvent MapEvent(Event item)
    {
        var isAllDay = item.Start?.Date is not null;
        var startUtc = ParseEventDateTime(item.Start, isAllDay, isStart: true);
        var endUtc = ParseEventDateTime(item.End, isAllDay, isStart: false);

        DateTime? rawUpdatedAtUtc = null;
        if (item.UpdatedRaw is not null)
            rawUpdatedAtUtc = item.UpdatedDateTimeOffset?.UtcDateTime;

        return new ExternalCalendarEvent
        {
            ExternalEventId = item.Id ?? Guid.NewGuid().ToString("N"),
            Title = string.IsNullOrWhiteSpace(item.Summary) ? "(No title)" : item.Summary,
            Description = item.Description,
            StartAtUtc = startUtc,
            EndAtUtc = endUtc,
            IsAllDay = isAllDay,
            Location = item.Location,
            HtmlLink = item.HtmlLink,
            RawUpdatedAtUtc = rawUpdatedAtUtc
        };
    }

    private static DateTime ParseEventDateTime(EventDateTime? eventDateTime, bool isAllDay, bool isStart)
    {
        if (eventDateTime is null)
            return DateTime.UtcNow;

        if (isAllDay)
        {
            var dateOnly = DateOnly.Parse(eventDateTime.Date!);
            var utc = dateOnly.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            return isStart ? utc : utc.AddDays(1);
        }

        return eventDateTime.DateTimeDateTimeOffset?.UtcDateTime
            ?? DateTime.UtcNow;
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId))
            throw new InvalidOperationException("Google ClientId is not configured.");

        if (string.IsNullOrWhiteSpace(_options.ClientSecret))
            throw new InvalidOperationException("Google ClientSecret is not configured.");

        if (string.IsNullOrWhiteSpace(_options.CalendarRedirectUri))
            throw new InvalidOperationException("Google CalendarRedirectUri is not configured.");
    }

    private sealed class GoogleOAuthTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; init; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }
    }

    private sealed class GoogleUserInfoResponse
    {
        [JsonPropertyName("email")]
        public string? Email { get; init; }
    }
}
