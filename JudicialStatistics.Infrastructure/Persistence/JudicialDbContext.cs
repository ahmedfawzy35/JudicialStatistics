using JudicialStatistics.Domain.Cases;
using JudicialStatistics.Domain.Courts;
using JudicialStatistics.Domain.Geography;
using JudicialStatistics.Domain.Identity;
using JudicialStatistics.Domain.Import;
using JudicialStatistics.Domain.Organization;
using JudicialStatistics.Domain.Reports;
using JudicialStatistics.Domain.Rules;
using JudicialStatistics.Domain.Statistics;
using JudicialStatistics.Domain.Years;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace JudicialStatistics.Infrastructure.Persistence;

/// <summary>
/// قاعدة مشتركة — Abstract Base يحتوي جميع جداول التطبيق.
/// يرث من IdentityDbContext{TUser}:
///   - JudicialDbContext (Infrastructure) ← IdentityUser  → Migrations + Seed
///   - ApplicationDbContext (Web)          ← ApplicationUser → Runtime
/// </summary>
public abstract class JudicialDbContextBase<TUser> : IdentityDbContext<TUser>
    where TUser : IdentityUser, new()
{
    protected JudicialDbContextBase(DbContextOptions options) : base(options) { }

    // ========== Identity / Access ==========
    public DbSet<AppUser>          AppUsers           => Set<AppUser>();
    public DbSet<Role>             AppRoles           => Set<Role>();
    public DbSet<Permission>       Permissions        => Set<Permission>();
    public DbSet<RolePermission>   AppRolePermissions => Set<RolePermission>();
    public DbSet<UserCourtGrant>   UserCourtGrants    => Set<UserCourtGrant>();
    public DbSet<UserSessionGrant> UserSessionGrants  => Set<UserSessionGrant>();

    // ========== Courts ==========
    public DbSet<CourtUnit>          CourtUnits          => Set<CourtUnit>();
    public DbSet<ReportingScope>     ReportingScopes     => Set<ReportingScope>();
    public DbSet<ReportingScopeUnit> ReportingScopeUnits => Set<ReportingScopeUnit>();

    // ========== Years ==========
    public DbSet<JudicialYear>      JudicialYears       => Set<JudicialYear>();
    public DbSet<StatisticalPeriod> StatisticalPeriods  => Set<StatisticalPeriod>();
    public DbSet<CourtPeriodStatus> CourtPeriodStatuses => Set<CourtPeriodStatus>();

    // ========== Organization ==========
    public DbSet<OrganizationVersion>    OrganizationVersions    => Set<OrganizationVersion>();
    public DbSet<Circuit>                Circuits                => Set<Circuit>();
    public DbSet<CircuitVersion>         CircuitVersions         => Set<CircuitVersion>();
    public DbSet<Session>                Sessions                => Set<Session>();
    public DbSet<SessionVersion>         SessionVersions         => Set<SessionVersion>();
    public DbSet<Judge>                  Judges                  => Set<Judge>();
    public DbSet<JudicialRole>           JudicialRoles           => Set<JudicialRole>();
    public DbSet<CircuitJudgeAssignment> CircuitJudgeAssignments => Set<CircuitJudgeAssignment>();
    public DbSet<JudgeMovement>          JudgeMovements          => Set<JudgeMovement>();
    public DbSet<StatisticalDiscipline>  StatisticalDisciplines  => Set<StatisticalDiscipline>();
    public DbSet<Employee>               Employees               => Set<Employee>();

    // ========== Geography ==========
    public DbSet<GeographicArea>      GeographicAreas      => Set<GeographicArea>();
    public DbSet<SessionJurisdiction> SessionJurisdictions => Set<SessionJurisdiction>();

    // ========== Cases ==========
    public DbSet<CaseTaxonomyNode> CaseTaxonomyNodes => Set<CaseTaxonomyNode>();
    public DbSet<CaseType>         CaseTypes          => Set<CaseType>();

    // ========== Rules ==========
    public DbSet<RuleSetVersion>       RuleSetVersions      => Set<RuleSetVersion>();
    public DbSet<SessionCaseType>      SessionCaseTypes     => Set<SessionCaseType>();
    public DbSet<CaseYearPolicy>       CaseYearPolicies     => Set<CaseYearPolicy>();
    public DbSet<CaseYearBand>         CaseYearBands        => Set<CaseYearBand>();
    public DbSet<JudgmentTreeNode>     JudgmentTreeNodes    => Set<JudgmentTreeNode>();
    public DbSet<JudgmentType>         JudgmentTypes        => Set<JudgmentType>();
    public DbSet<IncomingType>         IncomingTypes        => Set<IncomingType>();
    public DbSet<DeferralReason>       DeferralReasons      => Set<DeferralReason>();
    public DbSet<ProductionItemType>   ProductionItemTypes  => Set<ProductionItemType>();
    public DbSet<JudgeActionType>      JudgeActionTypes     => Set<JudgeActionType>();
    public DbSet<CaseTypeJudgmentRule> CaseTypeJudgmentRules => Set<CaseTypeJudgmentRule>();
    public DbSet<CaseTypeInputRule>    CaseTypeInputRules    => Set<CaseTypeInputRule>();
    public DbSet<CaseTypeDeferralRule> CaseTypeDeferralRules => Set<CaseTypeDeferralRule>();

    // ========== Statistics ==========
    public DbSet<StatisticsHeader>   StatisticsHeaders   => Set<StatisticsHeader>();
    public DbSet<OpeningBalance>     OpeningBalances     => Set<OpeningBalance>();
    public DbSet<ClosingBalance>     ClosingBalances     => Set<ClosingBalance>();
    public DbSet<IncomingRow>        IncomingRows        => Set<IncomingRow>();
    public DbSet<ProductionRow>      ProductionRows      => Set<ProductionRow>();
    public DbSet<JudgmentAllocation> JudgmentAllocations => Set<JudgmentAllocation>();
    public DbSet<JudgeJudgment>      JudgeJudgments      => Set<JudgeJudgment>();
    public DbSet<Postponement>       Postponements       => Set<Postponement>();
    public DbSet<AuditEntry>         AuditEntries        => Set<AuditEntry>();

    // ========== Reports Settings ==========
    public DbSet<CircuitReportColumnConfig> CircuitReportColumnConfigs => Set<CircuitReportColumnConfig>();

    // ========== Excel Import Templates ==========
    public DbSet<ExcelImportTemplate>      ExcelImportTemplates      => Set<ExcelImportTemplate>();
    public DbSet<ExcelImportColumnMapping> ExcelImportColumnMappings => Set<ExcelImportColumnMapping>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(JudicialDbContextBase<TUser>).Assembly);
    }
}

/// <summary>
/// Concrete Context في Infrastructure — يستخدم IdentityUser للـ Migrations
/// </summary>
public class JudicialDbContext : JudicialDbContextBase<IdentityUser>
{
    public JudicialDbContext(DbContextOptions<JudicialDbContext> options) : base(options) { }
}
