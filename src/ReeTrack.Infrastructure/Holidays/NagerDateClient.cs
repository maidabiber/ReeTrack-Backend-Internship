using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;

namespace ReeTrack.Infrastructure.Holidays;

public sealed class NagerDateClient : INagerDateClient
{
    private readonly HttpClient _httpClient;

    public NagerDateClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<HolidayCalendarDto>> GetAvailableCountriesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var countries = await _httpClient.GetFromJsonAsync<List<NagerCountryResponse>>(
                "api/v3/AvailableCountries",
                cancellationToken);

            if (countries is null)
                throw new AppException("Could not load available holiday calendars.", 502, ErrorCode.ServiceUnavailable);

            return countries
                .Where(c => !string.IsNullOrWhiteSpace(c.CountryCode) && !string.IsNullOrWhiteSpace(c.Name))
                .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .Select(c => new HolidayCalendarDto
                {
                    CountryCode = c.CountryCode.Trim().ToUpperInvariant(),
                    Name = c.Name.Trim()
                })
                .ToList();
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new AppException("Could not reach the holiday calendar service.", 502, ErrorCode.ServiceUnavailable);
        }
    }

    public async Task<IReadOnlyList<NagerPublicHoliday>> GetPublicHolidaysAsync(
        int year,
        string countryCode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var holidays = await _httpClient.GetFromJsonAsync<List<NagerHolidayResponse>>(
                $"api/v3/PublicHolidays/{year}/{Uri.EscapeDataString(countryCode)}",
                cancellationToken);

            if (holidays is null)
                throw new AppException("Could not load public holidays for the selected calendar.", 502, ErrorCode.ServiceUnavailable);

            return holidays
                .Where(h => h.Types is null || h.Types.Count == 0 || h.Types.Contains("Public", StringComparer.OrdinalIgnoreCase))
                .Where(h => !string.IsNullOrWhiteSpace(h.Name))
                .Select(h => new NagerPublicHoliday
                {
                    Date = DateOnly.FromDateTime(h.Date),
                    Name = string.IsNullOrWhiteSpace(h.Name) ? h.LocalName ?? "Holiday" : h.Name.Trim(),
                    Types = h.Types ?? []
                })
                .ToList();
        }
        catch (AppException)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            throw new AppException("Could not reach the holiday calendar service.", 502, ErrorCode.ServiceUnavailable);
        }
        catch (Exception)
        {
            throw new AppException("Could not load public holidays for the selected calendar.", 502, ErrorCode.ServiceUnavailable);
        }
    }

    private sealed class NagerCountryResponse
    {
        [JsonPropertyName("countryCode")]
        public string CountryCode { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    private sealed class NagerHolidayResponse
    {
        [JsonPropertyName("date")]
        public DateTime Date { get; set; }

        [JsonPropertyName("localName")]
        public string? LocalName { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("types")]
        public List<string>? Types { get; set; }
    }
}
