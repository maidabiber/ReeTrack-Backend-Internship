using ReeTrack.Domain.Common;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Domain.Entities;

public class Project : BaseEntity
{
    public Guid ClientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ProjectStatus Status { get; set; }

    public BillingType BillingType { get; set; }
    public string CurrencyCode { get; set; } = "EUR";
    public decimal? BudgetAmount { get; set; }
    public decimal? FixedFeeAmount { get; set; }
    public decimal? HourlyRate { get; set; }
    public decimal? TimeEstimateHours { get; set; }

    public Client Client { get; set; } = null!;
    public ICollection<ProjectTask> Tasks { get; set; } = [];
}
