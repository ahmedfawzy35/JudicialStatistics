namespace JudicialStatistics.Domain.Identity;

/// <summary>
/// ثوابت أكواد الصلاحيات — المصدر الوحيد للحقيقة لأسماء الصلاحيات
/// </summary>
public static class PermissionCodes
{
    // المحاكم
    public const string CourtsView   = "Courts.View";
    public const string CourtsManage = "Courts.Manage";

    // الأعوام القضائية
    public const string JudicialYearsView   = "JudicialYears.View";
    public const string JudicialYearsManage = "JudicialYears.Manage";

    // التشكيل
    public const string OrganizationView   = "Organization.View";
    public const string OrganizationManage = "Organization.Manage";

    // القضاة
    public const string JudgesManage = "Judges.Manage";

    // الإحصائية
    public const string StatisticsView       = "Statistics.View";
    public const string StatisticsCreate     = "Statistics.Create";
    public const string StatisticsEditDraft  = "Statistics.EditDraft";
    public const string StatisticsDeleteDraft = "Statistics.DeleteDraft";
    public const string StatisticsSubmit     = "Statistics.Submit";
    public const string StatisticsReview     = "Statistics.Review";
    public const string StatisticsApprove    = "Statistics.Approve";
    public const string StatisticsReopen     = "Statistics.Reopen";
    public const string StatisticalPeriodsManageStatus = "StatisticalPeriods.ManageStatus";

    // الأرصدة
    public const string BalancesView   = "Balances.View";
    public const string BalancesManage = "Balances.Manage";

    // التقارير
    public const string ReportsView   = "Reports.View";
    public const string ReportsExport = "Reports.Export";

    // المستخدمون
    public const string UsersManage = "Users.Manage";

    // سجل المراجعة
    public const string AuditView = "Audit.View";
}
