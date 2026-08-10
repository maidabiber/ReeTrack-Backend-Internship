namespace ReeTrack.Application.Common.Constants;


public static class Permissions
{
    public const string ReportsView = "reports.view";
    public const string TimesheetReview = "timesheets.review";
    public const string MembersView = "members.view";
    public const string MembersManage = "members.manage";
    public const string InvitationsManage = "invitations.manage";
    public const string AuditLogsView = "audit_logs.view";
    public const string BillableRatesManage = "billable_rates.manage";
    public const string RateMultipliersManage = "rate_multipliers.manage";
    public const string HolidaysManage = "holidays.manage";
    public const string ProjectsManage = "projects.manage";
    public const string InvoicesManage = "invoices.manage";

    public static readonly IReadOnlyList<string> All =
    [
        ReportsView,
        TimesheetReview,
        MembersView,
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
        public const string ReportsView = "Permission:reports.view";
        public const string TimesheetReview = "Permission:timesheets.review";
        public const string MembersView = "Permission:members.view";
        public const string MembersManage = "Permission:members.manage";
        public const string InvitationsManage = "Permission:invitations.manage";
        public const string AuditLogsView = "Permission:audit_logs.view";
        public const string BillableRatesManage = "Permission:billable_rates.manage";
        public const string RateMultipliersManage = "Permission:rate_multipliers.manage";
        public const string HolidaysManage = "Permission:holidays.manage";
        public const string ProjectsManage = "Permission:projects.manage";
        public const string InvoicesManage = "Permission:invoices.manage";
    }
}
