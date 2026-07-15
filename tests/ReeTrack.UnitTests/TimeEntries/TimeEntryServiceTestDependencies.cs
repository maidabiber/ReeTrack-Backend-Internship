using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Options;
using ReeTrack.Infrastructure.TimeEntries;

namespace ReeTrack.UnitTests.TimeEntries;

internal sealed class FakeEmailSender : IEmailSender
{
    public bool ThrowOnMentionEmail { get; set; }

    public Task SendInviteEmailAsync(
        string toEmail,
        string inviteUrl,
        string inviterName,
        string roleName,
        string appName,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task SendTimeEntryMentionEmailAsync(
        string toEmail,
        string assigneeName,
        string submitterName,
        string? description,
        string reviewUrl,
        string appName,
        CancellationToken cancellationToken = default) =>
        ThrowOnMentionEmail
            ? throw new InvalidOperationException("SMTP unavailable.")
            : Task.CompletedTask;
}

internal static class TimeEntryServiceTestDependencies
{
    public static (
        FakeEmailSender EmailSender,
        IConfiguration Configuration,
        IOptions<AppOptions> AppOptions,
        ISharedTimeEntryEmailNotifier EmailNotifier) Create()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Frontend:Origin"] = "http://localhost:5173"
            })
            .Build();

        var emailSender = new FakeEmailSender();
        var appOptions = Options.Create(new AppOptions());
        var emailNotifier = new SharedTimeEntryEmailNotifier(
            emailSender,
            NullLogger<SharedTimeEntryEmailNotifier>.Instance,
            configuration,
            appOptions);

        return (emailSender, configuration, appOptions, emailNotifier);
    }
}
