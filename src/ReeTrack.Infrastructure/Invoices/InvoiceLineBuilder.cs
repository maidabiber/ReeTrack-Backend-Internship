using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Reports;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Infrastructure.Invoices;

internal static class InvoiceLineBuilder
{
    public static IReadOnlyList<InvoiceLineItem> Build(IReadOnlyList<ProjectProfitabilityDto> projects)
    {
        var lines = new List<InvoiceLineItem>();
        var sortOrder = 0;

        foreach (var project in projects
                     .Where(p => p.Revenue > 0m)
                     .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
        {
            var billing = SummaryReportAnalytics.BillingModel(project.HourlyRate, project.FixedFeeAmount);
            if (billing is ProjectBillingModel.None)
                continue;

            var billableHours = SummaryReportAnalytics.Hours(project.BillableSeconds);

            if (billing == ProjectBillingModel.FixedFee)
            {
                var estimateHours = project.TimeEstimateHours;
                var hasEstimateExceeded = estimateHours is > 0m &&
                                          project.HourlyRate is > 0m &&
                                          billableHours > estimateHours.Value;

                var baseFee = project.FixedFeeAmount ?? project.Revenue;

                lines.Add(new InvoiceLineItem
                {
                    ProjectId = project.ProjectId,
                    Description = hasEstimateExceeded
                        ? $"{project.Name} · Up to {estimateHours:0.##}h estimated"
                        : BuildDescription(project, billableHours),
                    BillingModel = InvoiceLineBillingModel.FixedFee,
                    Quantity = 1m,
                    UnitPrice = baseFee,
                    Amount = baseFee,
                    SortOrder = sortOrder++
                });

                if (hasEstimateExceeded)
                {
                    var extraHours = billableHours - estimateHours!.Value;
                    var hourlyRate = project.HourlyRate!.Value;
                    var extraAmount = Math.Round(extraHours * hourlyRate, 2, MidpointRounding.AwayFromZero);

                    lines.Add(new InvoiceLineItem
                    {
                        ProjectId = project.ProjectId,
                        Description = $"{project.Name} · Extra hours above estimate ({extraHours:0.##}h @ {hourlyRate:0.##}/h)",
                        BillingModel = InvoiceLineBillingModel.Hourly,
                        Quantity = extraHours,
                        UnitPrice = hourlyRate,
                        Amount = extraAmount,
                        SortOrder = sortOrder++
                    });
                }
            }
            else
            {
                lines.Add(new InvoiceLineItem
                {
                    ProjectId = project.ProjectId,
                    Description = BuildDescription(project, billableHours),
                    BillingModel = InvoiceLineBillingModel.Hourly,
                    Quantity = billableHours,
                    UnitPrice = project.HourlyRate ?? 0m,
                    Amount = project.Revenue,
                    SortOrder = sortOrder++
                });
            }
        }

        return lines;
    }

    /// <summary>
    /// Billing model is shown separately on the invoice UI/PDF. Description keeps
    /// invoiced hours vs project estimate only.
    /// </summary>
    private static string BuildDescription(ProjectProfitabilityDto project, decimal actualHours)
    {
        var actual = $"{actualHours:0.##}h actual";
        if (project.TimeEstimateHours is not { } estimateHours)
            return $"{project.Name} · {actual}";

        var used = project.EstimateUsedPct is { } pct
            ? $" · {pct:0.#}% of estimate"
            : string.Empty;

        return $"{project.Name} · {actual} · {estimateHours:0.##}h estimated{used}";
    }
}
