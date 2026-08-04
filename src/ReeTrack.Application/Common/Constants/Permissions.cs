namespace ReeTrack.Application.Common.Constants;


public static class Permissions
{
    public const string ReportsView = "insights.reports.view";
    public const string TimesheetReview = "admin.timesheets.review";
    public const string MembersManage = "admin.members.manage";
    public const string InvitationsManage = "admin.invitations.manage";
    public const string AuditLogsView = "admin.audit_logs.view";
    public const string BillableRatesManage = "admin.billable_rates.manage";
    public const string RateMultipliersManage = "admin.rate_multipliers.manage";
    public const string HolidaysManage = "admin.holidays.manage";
    public const string ProjectsManage = "manage.projects.manage";
    public const string InvoicesManage = "manage.invoices.manage";

    public static readonly IReadOnlyList<string> All =
    [
        ReportsView,
        TimesheetReview,
        MembersManage,
        InvitationsManage,
        AuditLogsView,
        BillableRatesManage,
        RateMultipliersManage,
        HolidaysManage,
        ProjectsManage,
        InvoicesManage
    ];

    public static string PolicyName(string permission) => $"Permission:{permission}";

    public static class Policies
    {
        public const string ReportsView = "Permission:insights.reports.view";
        public const string TimesheetReview = "Permission:admin.timesheets.review";
        public const string MembersManage = "Permission:admin.members.manage";
        public const string InvitationsManage = "Permission:admin.invitations.manage";
        public const string AuditLogsView = "Permission:admin.audit_logs.view";
        public const string BillableRatesManage = "Permission:admin.billable_rates.manage";
        public const string RateMultipliersManage = "Permission:admin.rate_multipliers.manage";
        public const string HolidaysManage = "Permission:admin.holidays.manage";
        public const string ProjectsManage = "Permission:manage.projects.manage";
        public const string InvoicesManage = "Permission:manage.invoices.manage";
    }
}
