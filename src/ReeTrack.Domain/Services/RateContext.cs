using ReeTrack.Domain.Entities;

namespace ReeTrack.Domain.Services;

public sealed record RateContext(TimeEntry TimeEntry, DateOnly EntryDate, decimal BaseRate);
