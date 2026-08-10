using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Common.Models.CustomReports;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;
using ReeTrack.Infrastructure.Persistence;

namespace ReeTrack.Infrastructure.Reports;

public sealed class ReportShareService : IReportShareService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IReportService _reportService;
    private readonly ICustomReportService _customReportService;
    private readonly string _frontendOrigin;

    public ReportShareService(
        AppDbContext db,
        ICurrentUserService currentUser,
        IReportService reportService,
        ICustomReportService customReportService,
        IConfiguration configuration)
    {
        _db = db;
        _currentUser = currentUser;
        _reportService = reportService;
        _customReportService = customReportService;
        _frontendOrigin = configuration["Frontend:Origin"] ?? "http://localhost:5173";
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<Guid> GenerateLinkAsync(
        CreateShareLinkRequest request,
        CancellationToken cancellationToken = default)
    {
        var token = GenerateToken();
        var userId = _currentUser.UserId;

        var link = new ReportShareLink
        {
            Token = token,
            ReportType = request.ReportType,
            ReportId = request.ReportId,
            QueryJson = request.QueryJson,
            SpecJson = request.SpecJson,
            AccessLevel = request.AccessLevel,
            CreatedByUserId = userId,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _db.ReportShareLinks.Add(link);

        if (request.AccessLevel == ReportShareAccessLevel.Private && request.RecipientUserIds?.Count > 0)
        {
            foreach (var recipientId in request.RecipientUserIds)
            {
                link.Recipients.Add(new ReportShareRecipient
                {
                    UserId = recipientId,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        return link.Id;
    }

    public async Task<IReadOnlyList<ShareLinkDto>> FetchLinksAsync(
        ReportShareReportType reportType,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;

        var links = await _db.ReportShareLinks
            .Where(l => l.CreatedByUserId == userId && l.ReportType == reportType && l.IsActive)
            .OrderByDescending(l => l.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return links.Select(l => new ShareLinkDto
        {
            Id = l.Id,
            Token = l.Token,
            Url = $"{_frontendOrigin}/shared/{l.Token}",
            ReportType = l.ReportType,
            AccessLevel = l.AccessLevel,
            IsActive = l.IsActive,
            CreatedAtUtc = l.CreatedAtUtc,
            RecipientCount = l.Recipients.Count,
            QueryJson = l.QueryJson
        }).ToList();
    }

    public async Task RemoveLinkAsync(
        Guid shareLinkId,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;

        var link = await _db.ReportShareLinks
            .FirstOrDefaultAsync(l => l.Id == shareLinkId && l.CreatedByUserId == userId, cancellationToken)
            ?? throw AppErrors.NotFound("Share link");

        link.IsActive = false;
        link.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<SharedReportDto> GetSharedReportAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        var link = await _db.ReportShareLinks
            .Include(l => l.Recipients)
            .FirstOrDefaultAsync(l => l.Token == token, cancellationToken)
            ?? throw AppErrors.NotFound("Share link");

        if (!link.IsActive)
            throw AppErrors.Forbidden("This share link has been revoked.");

        if (link.AccessLevel == ReportShareAccessLevel.Private)
        {
            if (!_currentUser.IsAuthenticated)
                throw AppErrors.Forbidden("You do not have access to this report.");

            var userId = _currentUser.UserId;
            if (link.CreatedByUserId != userId && !link.Recipients.Any(r => r.UserId == userId))
                throw AppErrors.Forbidden("You do not have access to this report.");
        }

        var query = link.QueryJson != null
            ? JsonSerializer.Deserialize<ReportQuery>(link.QueryJson, JsonOptions)
            : null;

        SummaryReportDto? summary = null;
        DetailedReportDto? detailed = null;
        WorkloadReportDto? workload = null;
        ProfitabilityReportDto? profitability = null;
        CustomReportDto? custom = null;

        switch (link.ReportType)
        {
            case ReportShareReportType.Summary when query != null:
                summary = await _reportService.GetSummaryAsync(query, cancellationToken);
                break;
            case ReportShareReportType.Detailed when query != null:
                detailed = await _reportService.GetDetailedAsync(
                    query,
                    page: 1,
                    pageSize: 0,
                    cancellationToken: cancellationToken);
                break;
            case ReportShareReportType.Workload when query != null:
                workload = await _reportService.GetWorkloadAsync(query, cancellationToken);
                break;
            case ReportShareReportType.Profitability when query != null:
                profitability = await _reportService.GetProfitabilityAsync(query, cancellationToken);
                break;
            case ReportShareReportType.Custom when link.SpecJson != null:
                var spec = JsonSerializer.Deserialize<CustomReportSpec>(link.SpecJson, JsonOptions);
                if (spec != null)
                    custom = await _customReportService.RunAsync(spec, cancellationToken: cancellationToken);
                break;
        }

        return new SharedReportDto
        {
            ReportType = link.ReportType,
            AccessLevel = link.AccessLevel,
            Summary = summary,
            Detailed = detailed,
            Workload = workload,
            Profitability = profitability,
            Custom = custom
        };
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
