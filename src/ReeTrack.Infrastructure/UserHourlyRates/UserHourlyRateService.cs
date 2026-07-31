using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Exceptions;
using ReeTrack.Domain.ValueObjects;

namespace ReeTrack.Infrastructure.UserHourlyRates;

public sealed class UserHourlyRateService : IUserHourlyRateService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrencyService _currencyService;

    public UserHourlyRateService(IApplicationDbContext db, ICurrencyService currencyService)
    {
        _db = db;
        _currencyService = currencyService;
    }

    public async Task<IReadOnlyList<UserHourlyRateDto>> ListByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureUserExistsAsync(userId, cancellationToken);

        var rates = await _db.UserHourlyRates
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.ValidFrom)
            .ToListAsync(cancellationToken);

        return rates.Select(Map).ToList();
    }

    public async Task<UserHourlyRateDto> GetCurrentAsync(
        Guid userId,
        DateOnly? onDate = null,
        CancellationToken cancellationToken = default)
    {
        var user = await LoadUserWithRatesAsync(userId, cancellationToken);
        var date = onDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        try
        {
            return Map(user.GetHourlyRateOn(date));
        }
        catch (DomainException ex)
        {
            throw new AppException(ex.Message, 404, ErrorCode.NotFound);
        }
    }

    public async Task<UserHourlyRateDto> ChangeAsync(
        Guid userId,
        ChangeUserHourlyRateInput input,
        CancellationToken cancellationToken = default)
    {
        var user = await LoadUserWithRatesAsync(userId, cancellationToken);
        var currency = await _currencyService.EnsureSupportedAsync(input.CurrencyCode, cancellationToken);

        try
        {
            var money = Money.Of(input.HourlyRate, currency);
            var changed = user.ChangeHourlyRate(money, input.ValidFrom);
            await _db.SaveChangesAsync(cancellationToken);
            return Map(changed);
        }
        catch (DomainException ex)
        {
            throw new AppException(ex.Message, 400, ErrorCode.Validation);
        }
    }

    public async Task<UserHourlyRateDto> CorrectAsync(
        Guid userId,
        Guid rateId,
        CorrectUserHourlyRateInput input,
        CancellationToken cancellationToken = default)
    {
        var user = await LoadUserWithRatesAsync(userId, cancellationToken);
        var currency = await _currencyService.EnsureSupportedAsync(input.CurrencyCode, cancellationToken);

        try
        {
            var money = Money.Of(input.HourlyRate, currency);
            var corrected = user.CorrectHourlyRate(rateId, money, input.ValidFrom, input.ValidTo);
            await _db.SaveChangesAsync(cancellationToken);
            return Map(corrected);
        }
        catch (DomainException ex)
        {
            var isNotFound = ex.Message.Contains("was not found", StringComparison.OrdinalIgnoreCase);
            var status = isNotFound ? 404 : 400;
            var code = isNotFound ? ErrorCode.NotFound : ErrorCode.Validation;
            throw new AppException(ex.Message, status, code);
        }
    }

    private async Task EnsureUserExistsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var exists = await _db.Users.AsNoTracking().AnyAsync(u => u.Id == userId, cancellationToken);
        if (!exists)
            throw AppErrors.NotFound("User");
    }

    private async Task<User> LoadUserWithRatesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .Include(u => u.HourlyRates)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
            throw AppErrors.NotFound("User");

        return user;
    }

    private static UserHourlyRateDto Map(UserHourlyRate rate) =>
        new()
        {
            Id = rate.Id,
            UserId = rate.UserId,
            HourlyRate = rate.Rate.Amount,
            CurrencyCode = rate.Rate.CurrencyCode,
            ValidFrom = rate.ValidFrom,
            ValidTo = rate.ValidTo
        };
}
