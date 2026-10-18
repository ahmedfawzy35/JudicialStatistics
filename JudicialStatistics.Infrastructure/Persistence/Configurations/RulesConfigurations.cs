using JudicialStatistics.Domain.Rules;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JudicialStatistics.Infrastructure.Persistence.Configurations;

public class RuleSetVersionConfiguration : IEntityTypeConfiguration<RuleSetVersion>
{
    public void Configure(EntityTypeBuilder<RuleSetVersion> builder)
    {
        builder.ToTable("RuleSetVersions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(24).IsUnicode(false);
        builder.Property(x => x.ApprovedAt).HasColumnType("datetime2");
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasOne(x => x.ApprovedBy)
            .WithMany()
            .HasForeignKey(x => x.ApprovedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class CaseYearPolicyConfiguration : IEntityTypeConfiguration<CaseYearPolicy>
{
    public void Configure(EntityTypeBuilder<CaseYearPolicy> builder)
    {
        builder.ToTable("CaseYearPolicies");
        builder.HasKey(x => x.Id);

        // UNIQUE(JudicialYearId, CaseTypeId, VersionNo)
        builder.HasIndex(x => new { x.JudicialYearId, x.CaseTypeId, x.VersionNo }).IsUnique();

        builder.Property(x => x.DecisionReference).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(24).IsUnicode(false);

        builder.HasOne(x => x.JudicialYear)
            .WithMany()
            .HasForeignKey(x => x.JudicialYearId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CaseType)
            .WithMany()
            .HasForeignKey(x => x.CaseTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Bands)
            .WithOne(x => x.Policy)
            .HasForeignKey(x => x.CaseYearPolicyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.JudicialYearId);
        builder.HasIndex(x => x.CaseTypeId);
    }
}

public class CaseYearBandConfiguration : IEntityTypeConfiguration<CaseYearBand>
{
    public void Configure(EntityTypeBuilder<CaseYearBand> builder)
    {
        builder.ToTable("CaseYearBands");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.BandKind).IsRequired().HasMaxLength(16).IsUnicode(false);
        builder.Property(x => x.LabelAr).IsRequired().HasMaxLength(60);

        builder.HasOne(x => x.Policy)
            .WithMany(x => x.Bands)
            .HasForeignKey(x => x.CaseYearPolicyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.CaseYearPolicyId);
    }
}

public class JudgmentTreeNodeConfiguration : IEntityTypeConfiguration<JudgmentTreeNode>
{
    public void Configure(EntityTypeBuilder<JudgmentTreeNode> builder)
    {
        builder.ToTable("JudgmentTreeNodes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(50).IsUnicode(false);
        builder.Property(x => x.NameAr).IsRequired().HasMaxLength(200);

        builder.HasOne(x => x.RuleSetVersion)
            .WithMany(x => x.JudgmentTreeNodes)
            .HasForeignKey(x => x.RuleSetVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Parent)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.RuleSetVersionId);
        builder.HasIndex(x => x.ParentId);
    }
}
