using ReeTrack.Domain.Common;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Domain.Entities;

public class Holiday : BaseEntity
{
    public DateOnly Date { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public HolidaySource Source { get; set; } = HolidaySource.Custom;
    public string? CountryCode { get; set; }
}
