using Microsoft.AspNetCore.DataProtection;
using ReeTrack.Application.Integrations.Calendar;

namespace ReeTrack.Infrastructure.Integrations.Calendar;

public class DataProtectionTokenProtector : ITokenProtector
{
    private readonly IDataProtector _protector;

    public DataProtectionTokenProtector(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector("UserCalendarTokens");
    }

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string Unprotect(string protectedText) => _protector.Unprotect(protectedText);
}
