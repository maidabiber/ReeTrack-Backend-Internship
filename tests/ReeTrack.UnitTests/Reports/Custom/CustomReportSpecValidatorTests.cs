using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Common.Models.CustomReports;
using ReeTrack.Infrastructure.Reports.Custom;
using Xunit;

namespace ReeTrack.UnitTests.Reports.Custom;

public class CustomReportSpecValidatorTests
{
    [Fact]
    public void Validate_RejectsIncompatibleMetricDimension()
    {
        var spec = new CustomReportSpec
        {
            Blocks =
            [
                new BreakdownBlockSpec
                {
                    Id = "b1",
                    Dimensions = ["day"],
                    Metrics = ["revenue"]
                }
            ]
        };

        var ex = Assert.Throws<AppException>(() => CustomReportSpecValidator.Validate(spec));
        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(ErrorCode.Validation, ex.Code);
    }

    [Fact]
    public void Validate_RejectsTooManyBlocks()
    {
        var blocks = Enumerable.Range(0, 13)
            .Select(i => (ReportBlockSpec)new KpiBlockSpec
            {
                Id = $"b{i}",
                Metrics = ["totalHours"]
            })
            .ToList();

        var ex = Assert.Throws<AppException>(() =>
            CustomReportSpecValidator.Validate(new CustomReportSpec { Blocks = blocks }));
        Assert.Equal(ErrorCode.Validation, ex.Code);
    }

    [Fact]
    public void AnalyzeNeeds_HoursOnly_DoesNotNeedCost()
    {
        var spec = new CustomReportSpec
        {
            Blocks =
            [
                new KpiBlockSpec { Id = "b1", Metrics = ["totalHours", "entryCount"] }
            ]
        };

        var (needsCost, needsProjects, needsHourTargets) = CustomReportSpecValidator.AnalyzeNeeds(spec);
        Assert.False(needsCost);
        Assert.False(needsProjects);
        Assert.False(needsHourTargets);
    }

    [Fact]
    public void AnalyzeNeeds_LabourCost_NeedsCost()
    {
        var spec = new CustomReportSpec
        {
            Blocks =
            [
                new KpiBlockSpec { Id = "b1", Metrics = ["labourCost"] }
            ]
        };

        var (needsCost, _, _) = CustomReportSpecValidator.AnalyzeNeeds(spec);
        Assert.True(needsCost);
    }

    [Fact]
    public void AnalyzeNeeds_HourTypeDimension_NeedsCost()
    {
        var spec = new CustomReportSpec
        {
            Blocks =
            [
                new BreakdownBlockSpec
                {
                    Id = "b1",
                    Dimensions = ["hourType"],
                    Metrics = ["totalHours"]
                }
            ]
        };

        var (needsCost, _, _) = CustomReportSpecValidator.AnalyzeNeeds(spec);
        Assert.True(needsCost);
    }

    [Fact]
    public void Validate_AllowsOpenEndedQuery()
    {
        var spec = new CustomReportSpec
        {
            Query = new() { From = null, To = null },
            Blocks =
            [
                new KpiBlockSpec { Id = "b1", Metrics = ["totalHours"] }
            ]
        };

        CustomReportSpecValidator.Validate(spec);
    }

    private static CustomReportSpec WithComputed(ComputedColumnSpec computed) =>
        new()
        {
            Blocks =
            [
                new BreakdownBlockSpec
                {
                    Id = "b1",
                    Dimensions = ["client"],
                    Metrics = ["totalHours"],
                    Computed = [computed]
                }
            ]
        };

    [Fact]
    public void Validate_AcceptsALiteralRightOperand()
    {
        // "billable hours x 85" is the case two metrics cannot express.
        var spec = WithComputed(new ComputedColumnSpec(
            "c1", "At 85/h", "totalHours", ComputedOperator.Multiply, Right: null, RightValue: 85m));

        CustomReportSpecValidator.Validate(spec);
    }

    [Fact]
    public void Validate_RejectsBothRightOperands()
    {
        var spec = WithComputed(new ComputedColumnSpec(
            "c1", "Ambiguous", "totalHours", ComputedOperator.Multiply, Right: "totalHours", RightValue: 2m));

        var ex = Assert.Throws<AppException>(() => CustomReportSpecValidator.Validate(spec));
        Assert.Equal(ErrorCode.Validation, ex.Code);
    }

    [Fact]
    public void Validate_RejectsLiteralDivisionByZero()
    {
        var spec = WithComputed(new ComputedColumnSpec(
            "c1", "Boom", "totalHours", ComputedOperator.Divide, Right: null, RightValue: 0m));

        var ex = Assert.Throws<AppException>(() => CustomReportSpecValidator.Validate(spec));
        Assert.Equal(ErrorCode.Validation, ex.Code);
    }

    [Fact]
    public void Validate_RejectsAnOversizedLiteral()
    {
        var spec = WithComputed(new ComputedColumnSpec(
            "c1", "Overflow", "totalHours", ComputedOperator.Multiply,
            Right: null, RightValue: CustomReportSpecValidator.MaxComputedLiteral + 1m));

        var ex = Assert.Throws<AppException>(() => CustomReportSpecValidator.Validate(spec));
        Assert.Equal(ErrorCode.Validation, ex.Code);
    }

    [Fact]
    public void Validate_RejectsARightOperandOnPctOfTotal()
    {
        var spec = WithComputed(new ComputedColumnSpec(
            "c1", "Share", "totalHours", ComputedOperator.PctOfTotal, Right: null, RightValue: 5m));

        var ex = Assert.Throws<AppException>(() => CustomReportSpecValidator.Validate(spec));
        Assert.Equal(ErrorCode.Validation, ex.Code);
    }

    [Fact]
    public void Validate_RejectsOversizedNarrativeCachedText()
    {
        var spec = new CustomReportSpec
        {
            Blocks =
            [
                new NarrativeBlockSpec
                {
                    Id = "n1",
                    CachedText = new string('x', CustomReportSpecValidator.MaxNarrativeTextLength + 1)
                }
            ]
        };

        var ex = Assert.Throws<AppException>(() => CustomReportSpecValidator.Validate(spec));
        Assert.Equal(ErrorCode.Validation, ex.Code);
    }

    [Fact]
    public void Validate_RejectsOversizedNarrativeFocus()
    {
        var spec = new CustomReportSpec
        {
            Blocks =
            [
                new NarrativeBlockSpec
                {
                    Id = "n1",
                    Focus = new string('x', CustomReportSpecValidator.MaxNarrativeFocusLength + 1)
                }
            ]
        };

        var ex = Assert.Throws<AppException>(() => CustomReportSpecValidator.Validate(spec));
        Assert.Equal(ErrorCode.Validation, ex.Code);
    }

    [Fact]
    public void Validate_AcceptsNarrativeWithinLimits()
    {
        var spec = new CustomReportSpec
        {
            Blocks =
            [
                new NarrativeBlockSpec { Id = "n1", Focus = "margin by client", CachedText = "Some findings." }
            ]
        };

        CustomReportSpecValidator.Validate(spec);
    }

    [Fact]
    public void Validate_RejectsTooManyEntriesGroupByLevels()
    {
        var spec = new CustomReportSpec
        {
            Blocks =
            [
                new EntriesBlockSpec
                {
                    Id = "e1",
                    Columns = ["client"],
                    GroupBy = [ReportGroupBy.Client, ReportGroupBy.Project, ReportGroupBy.Task]
                }
            ]
        };

        var ex = Assert.Throws<AppException>(() => CustomReportSpecValidator.Validate(spec));
        Assert.Equal(ErrorCode.Validation, ex.Code);
    }
}
