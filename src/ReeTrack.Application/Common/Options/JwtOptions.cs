namespace ReeTrack.Application.Common.Options;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "reetrack";
    public string Audience { get; set; } = "reetrack-api";
    public string SigningKey { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 60 * 24;
}
