namespace ReeTrack.Application.Integrations.Calendar;

public interface ITokenProtector
{
    string Protect(string plaintext);
    string Unprotect(string protectedText);
}
