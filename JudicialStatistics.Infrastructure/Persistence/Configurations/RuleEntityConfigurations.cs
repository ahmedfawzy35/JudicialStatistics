using JudicialStatistics.Domain.Rules;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JudicialStatistics.Infrastructure.Persistence.Configurations;

public class CaseTypeJudgmentRuleConfiguration : IEntityTypeConfiguration<CaseTypeJudgmentRule>
{
    public void Configure(EntityTypeBuilder<CaseTypeJudgmentRule> builder)
    {
        builder.ToTable("CaseTypeJudgmentRules");
        builder.HasKey(x => x.Id);

        // UNIQUE(RuleSetVersionId, CaseTypeId, JudgmentTypeId)
        builder.HasIndex(x => new { x.RuleSetVersionId, x.CaseTypeId, x.JudgmentTypeId }).IsUnique();

        builder.HasOne(x => x.RuleSetVersion)
            .WithMany(x => x.CaseTypeJudgmentRules)
            .HasForeignKey(x => x.RuleSetVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CaseType)
            .WithMany()
            .HasForeignKey(x => x.CaseTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.JudgmentType)
            .WithMany(x => x.Rules)
            .HasForeignKey(x => x.JudgmentTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.TreeNode)
            .WithMany()
            .HasForeignKey(x => x.TreeNodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.DeferralReason)
            .WithMany()
            .HasForeignKey(x => x.DeferralReasonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.CaseTypeId);
        builder.HasIndex(x => x.RuleSetVersionId);
    }
}

public class CaseTypeInputRuleConfiguration : IEntityTypeConfiguration<CaseTypeInputRule>
{
    public void Configure(EntityTypeBuilder<CaseTypeInputRule> builder)
    {
        builder.ToTable("CaseTypeInputRules");
        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.RuleSetVersion)
            .WithMany(x => x.CaseTypeInputRules)
            .HasForeignKey(x => x.RuleSetVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CaseType)
            .WithMany()
            .HasForeignKey(x => x.CaseTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.IncomingType)
            .WithMany(x => x.InputRules)
            .HasForeignKey(x => x.IncomingTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProductionItemType)
            .WithMany(x => x.InputRules)
            .HasForeignKey(x => x.ProductionItemTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.JudgeActionType)
            .WithMany(x => x.InputRules)
            .HasForeignKey(x => x.JudgeActionTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.RuleSetVersionId, x.CaseTypeId });
    }
}

public class CaseTypeDeferralRuleConfiguration : IEntityTypeConfiguration<CaseTypeDeferralRule>
{
    public void Configure(EntityTypeBuilder<CaseTypeDeferralRule> builder)
    {
        builder.ToTable("CaseTypeDeferralRules");
        builder.HasKey(x => x.Id);

        // UNIQUE(RuleSetVersionId, CaseTypeId, DeferralReasonId)
        builder.HasIndex(x => new { x.RuleSetVersionId, x.CaseTypeId, x.DeferralReasonId }).IsUnique();

        builder.HasOne(x => x.RuleSetVersion)
            .WithMany(x => x.CaseTypeDeferralRules)
            .HasForeignKey(x => x.RuleSetVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CaseType)
            .WithMany()
            .HasForeignKey(x => x.CaseTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.DeferralReason)
            .WithMany(x => x.DeferralRules)
            .HasForeignKey(x => x.DeferralReasonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ReturnIncomingType)
            .WithMany(x => x.DeferralRules)
            .HasForeignKey(x => x.ReturnIncomingTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.RuleSetVersionId, x.CaseTypeId });
    }
}
