using JudicialStatistics.Domain.Statistics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JudicialStatistics.Infrastructure.Persistence.Configurations;

public class StatisticsHeaderConfiguration : IEntityTypeConfiguration<StatisticsHeader>
{
    public void Configure(EntityTypeBuilder<StatisticsHeader> builder)
    {
        builder.ToTable("StatisticsHeaders");
        builder.HasKey(x => x.Id);

        // UNIQUE(SessionVersionId, StatisticalPeriodId)
        builder.HasIndex(x => new { x.SessionVersionId, x.StatisticalPeriodId }).IsUnique();

        builder.Property(x => x.Status).IsRequired().HasMaxLength(24).IsUnicode(false);
        builder.Property(x => x.ReviewComment).HasMaxLength(1000);
        builder.Property(x => x.RejectionReason).HasMaxLength(1000);
        builder.Property(x => x.CreatedAt).HasColumnType("datetime2");
        builder.Property(x => x.SubmittedAt).HasColumnType("datetime2");
        builder.Property(x => x.ReviewedAt).HasColumnType("datetime2");
        builder.Property(x => x.ApprovedAt).HasColumnType("datetime2");
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasOne(x => x.SessionVersion)
            .WithMany()
            .HasForeignKey(x => x.SessionVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.StatisticalPeriod)
            .WithMany()
            .HasForeignKey(x => x.StatisticalPeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RuleSetVersion)
            .WithMany()
            .HasForeignKey(x => x.RuleSetVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CaseYearPolicy)
            .WithMany()
            .HasForeignKey(x => x.CaseYearPolicyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CreatedBy)
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SubmittedBy)
            .WithMany()
            .HasForeignKey(x => x.SubmittedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ReviewedBy)
            .WithMany()
            .HasForeignKey(x => x.ReviewedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ApprovedBy)
            .WithMany()
            .HasForeignKey(x => x.ApprovedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.IncomingRows)
            .WithOne(x => x.StatisticsHeader)
            .HasForeignKey(x => x.StatisticsHeaderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.ProductionRows)
            .WithOne(x => x.StatisticsHeader)
            .HasForeignKey(x => x.StatisticsHeaderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.AuditEntries)
            .WithOne(x => x.StatisticsHeader)
            .HasForeignKey(x => x.StatisticsHeaderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.SessionVersionId);
        builder.HasIndex(x => x.StatisticalPeriodId);
        builder.HasIndex(x => x.Status);
    }
}

public class OpeningBalanceConfiguration : IEntityTypeConfiguration<OpeningBalance>
{
    public void Configure(EntityTypeBuilder<OpeningBalance> builder)
    {
        builder.ToTable("OpeningBalances");
        builder.HasKey(x => x.Id);

        // UNIQUE(SessionVersionId, CaseTypeId, CaseYearBandId)
        builder.HasIndex(x => new { x.SessionVersionId, x.CaseTypeId, x.CaseYearBandId }).IsUnique();

        builder.HasOne(x => x.SessionVersion).WithMany(x => x.OpeningBalances)
            .HasForeignKey(x => x.SessionVersionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.CaseType).WithMany()
            .HasForeignKey(x => x.CaseTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CaseYearBand).WithMany()
            .HasForeignKey(x => x.CaseYearBandId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.SessionVersionId);
    }
}

public class IncomingRowConfiguration : IEntityTypeConfiguration<IncomingRow>
{
    public void Configure(EntityTypeBuilder<IncomingRow> builder)
    {
        builder.ToTable("IncomingRows");
        builder.HasKey(x => x.Id);

        // UNIQUE(StatisticsHeaderId, CaseTypeId, CaseYearBandId, IncomingTypeId)
        builder.HasIndex(x => new { x.StatisticsHeaderId, x.CaseTypeId, x.CaseYearBandId, x.IncomingTypeId }).IsUnique();

        builder.HasOne(x => x.StatisticsHeader).WithMany(x => x.IncomingRows)
            .HasForeignKey(x => x.StatisticsHeaderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.CaseType).WithMany()
            .HasForeignKey(x => x.CaseTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CaseYearBand).WithMany()
            .HasForeignKey(x => x.CaseYearBandId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.IncomingType).WithMany()
            .HasForeignKey(x => x.IncomingTypeId).OnDelete(DeleteBehavior.Restrict);

        // SourcePeriodId — يُملأ فقط للتقصير الوارد (SHORT)
        builder.HasOne(x => x.SourcePeriod).WithMany()
            .HasForeignKey(x => x.SourcePeriodId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.StatisticsHeaderId);
        builder.HasIndex(x => x.SourcePeriodId);
    }
}

public class ProductionRowConfiguration : IEntityTypeConfiguration<ProductionRow>
{
    public void Configure(EntityTypeBuilder<ProductionRow> builder)
    {
        builder.ToTable("ProductionRows");
        builder.HasKey(x => x.Id);

        // UNIQUE(StatisticsHeaderId, CaseTypeId, CaseYearBandId, ProductionItemTypeId)
        builder.HasIndex(x => new { x.StatisticsHeaderId, x.CaseTypeId, x.CaseYearBandId, x.ProductionItemTypeId }).IsUnique();

        builder.HasOne(x => x.StatisticsHeader).WithMany(x => x.ProductionRows)
            .HasForeignKey(x => x.StatisticsHeaderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.CaseType).WithMany()
            .HasForeignKey(x => x.CaseTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CaseYearBand).WithMany()
            .HasForeignKey(x => x.CaseYearBandId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ProductionItemType).WithMany()
            .HasForeignKey(x => x.ProductionItemTypeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany<JudgmentAllocation>()
            .WithOne(x => x.ProductionRow)
            .HasForeignKey(x => x.ProductionRowId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.StatisticsHeaderId);
    }
}

public class JudgmentAllocationConfiguration : IEntityTypeConfiguration<JudgmentAllocation>
{
    public void Configure(EntityTypeBuilder<JudgmentAllocation> builder)
    {
        builder.ToTable("JudgmentAllocations");
        builder.HasKey(x => x.Id);

        // UNIQUE(ProductionRowId, JudgeId)
        builder.HasIndex(x => new { x.ProductionRowId, x.JudgeId }).IsUnique();

        builder.HasOne(x => x.ProductionRow).WithMany()
            .HasForeignKey(x => x.ProductionRowId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Judge).WithMany()
            .HasForeignKey(x => x.JudgeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ProductionRowId);
    }
}

/// <summary>
/// إعداد جدول JudgeJudgments — أحكام القضاة التفصيلية
/// مدني/أسرة/عمال: تفاصيل كاملة مع القاضي
/// جنائي: فئات إجمالية (CRIM_CAT_*) مع القاضي
/// </summary>
public class JudgeJudgmentConfiguration : IEntityTypeConfiguration<JudgeJudgment>
{
    public void Configure(EntityTypeBuilder<JudgeJudgment> builder)
    {
        builder.ToTable("JudgeJudgments");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.JudgmentKind)
            .IsRequired()
            .HasMaxLength(64)
            .IsUnicode(false);

        // UNIQUE(StatisticsHeaderId, JudgeId, CaseYearBandId, JudgmentKind)
        builder.HasIndex(x => new { x.StatisticsHeaderId, x.JudgeId, x.CaseYearBandId, x.JudgmentKind })
            .IsUnique();

        builder.HasOne(x => x.StatisticsHeader).WithMany(x => x.JudgeJudgments)
            .HasForeignKey(x => x.StatisticsHeaderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Judge).WithMany()
            .HasForeignKey(x => x.JudgeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CaseYearBand).WithMany()
            .HasForeignKey(x => x.CaseYearBandId).OnDelete(DeleteBehavior.Restrict);

        // فهارس للتقارير
        builder.HasIndex(x => x.StatisticsHeaderId);
        builder.HasIndex(x => new { x.StatisticsHeaderId, x.JudgeId });
        builder.HasIndex(x => new { x.StatisticsHeaderId, x.JudgmentKind });
    }
}

/// <summary>
/// إعداد جدول Postponements — المؤجلات على الشهور (مدني + جنائي + أسرة + عمال)
/// </summary>
public class PostponementConfiguration : IEntityTypeConfiguration<Postponement>
{
    public void Configure(EntityTypeBuilder<Postponement> builder)
    {
        builder.ToTable("Postponements");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.PostponementKind)
            .IsRequired()
            .HasMaxLength(64)
            .IsUnicode(false);

        // UNIQUE(StatisticsHeaderId, CaseYearBandId, PostponementKind, TargetPeriodId)
        builder.HasIndex(x => new { x.StatisticsHeaderId, x.CaseYearBandId, x.PostponementKind, x.TargetPeriodId })
            .IsUnique();

        builder.HasOne(x => x.StatisticsHeader).WithMany(x => x.Postponements)
            .HasForeignKey(x => x.StatisticsHeaderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.CaseYearBand).WithMany()
            .HasForeignKey(x => x.CaseYearBandId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.TargetPeriod).WithMany()
            .HasForeignKey(x => x.TargetPeriodId).OnDelete(DeleteBehavior.Restrict);

        // فهارس للتقارير
        builder.HasIndex(x => x.StatisticsHeaderId);
        builder.HasIndex(x => new { x.StatisticsHeaderId, x.TargetPeriodId });
        builder.HasIndex(x => x.TargetPeriodId);
    }
}

public class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("AuditEntries");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Action).IsRequired().HasMaxLength(100).IsUnicode(false);
        builder.Property(x => x.Comment).HasMaxLength(1000);
        builder.Property(x => x.OccurredAt).HasColumnType("datetime2");

        builder.HasOne(x => x.StatisticsHeader).WithMany(x => x.AuditEntries)
            .HasForeignKey(x => x.StatisticsHeaderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.StatisticsHeaderId);
        builder.HasIndex(x => x.OccurredAt);
    }
}
