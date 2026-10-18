using JudicialStatistics.Domain.Cases;
using JudicialStatistics.Domain.Rules;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JudicialStatistics.Infrastructure.Persistence.Configurations;

public class CaseTaxonomyNodeConfiguration : IEntityTypeConfiguration<CaseTaxonomyNode>
{
    public void Configure(EntityTypeBuilder<CaseTaxonomyNode> builder)
    {
        builder.ToTable("CaseTaxonomyNodes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(50).IsUnicode(false);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.NameAr).IsRequired().HasMaxLength(200);

        builder.HasOne(x => x.Parent)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.CaseTypes)
            .WithOne(x => x.TaxonomyNode)
            .HasForeignKey(x => x.TaxonomyNodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ParentId);
    }
}

public class CaseTypeConfiguration : IEntityTypeConfiguration<CaseType>
{
    public void Configure(EntityTypeBuilder<CaseType> builder)
    {
        builder.ToTable("CaseTypes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(50).IsUnicode(false);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(x => x.StatisticalLevelCode).IsRequired().HasMaxLength(24).IsUnicode(false);
        builder.Property(x => x.ProcedureCode).IsRequired().HasMaxLength(24).IsUnicode(false);
        builder.Property(x => x.AgeCategoryCode).IsRequired().HasMaxLength(24).IsUnicode(false);

        builder.HasOne(x => x.TaxonomyNode)
            .WithMany(x => x.CaseTypes)
            .HasForeignKey(x => x.TaxonomyNodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.SessionCaseTypes)
            .WithOne(x => x.CaseType)
            .HasForeignKey(x => x.CaseTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.TaxonomyNodeId);
    }
}

public class SessionCaseTypeConfiguration : IEntityTypeConfiguration<SessionCaseType>
{
    public void Configure(EntityTypeBuilder<SessionCaseType> builder)
    {
        builder.ToTable("SessionCaseTypes");
        builder.HasKey(x => x.Id);

        // UNIQUE(SessionVersionId, CaseTypeId)
        builder.HasIndex(x => new { x.SessionVersionId, x.CaseTypeId }).IsUnique();

        builder.HasOne(x => x.SessionVersion)
            .WithMany(x => x.SessionCaseTypes)
            .HasForeignKey(x => x.SessionVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CaseType)
            .WithMany(x => x.SessionCaseTypes)
            .HasForeignKey(x => x.CaseTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.CaseTypeId);
    }
}
