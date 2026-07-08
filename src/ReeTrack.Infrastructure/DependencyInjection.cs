using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Options;
using ReeTrack.Infrastructure.Auditing;
using ReeTrack.Infrastructure.Auth;
using ReeTrack.Infrastructure.Clients;
using ReeTrack.Infrastructure.Email;
using ReeTrack.Infrastructure.Invitations;
using ReeTrack.Infrastructure.Members;
using ReeTrack.Infrastructure.TimeEntries;

namespace ReeTrack.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GoogleAuthOptions>(configuration.GetSection(GoogleAuthOptions.SectionName));
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.Configure<InvitationOptions>(configuration.GetSection(InvitationOptions.SectionName));
        services.Configure<AppOptions>(configuration.GetSection(AppOptions.SectionName));
        services.Configure<TimeEntryOptions>(configuration.GetSection(TimeEntryOptions.SectionName));

        services.AddHttpContextAccessor();

        services.AddHttpClient<IGoogleCodeExchanger, GoogleCodeExchanger>();
        services.AddScoped<IGoogleOAuthService, GoogleOAuthService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ISetupService, SetupService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddScoped<IAuditLogService, AuditLogService>();

        var smtpConfigured = !string.IsNullOrWhiteSpace(configuration[$"{EmailOptions.SectionName}:SmtpHost"]);
        if (smtpConfigured)
            services.AddScoped<IEmailSender, SmtpEmailSender>();
        else
            services.AddScoped<IEmailSender, NoOpEmailSender>();

        services.AddScoped<IInvitationService, InvitationService>();
        services.AddScoped<IMemberService, MemberService>();
        services.AddScoped<ILockedPeriodService, LockedPeriodService>();
        services.AddScoped<ITimeEntryService, TimeEntryService>();
        services.AddScoped<IClientService, ClientService>();

        return services;
    }
}
