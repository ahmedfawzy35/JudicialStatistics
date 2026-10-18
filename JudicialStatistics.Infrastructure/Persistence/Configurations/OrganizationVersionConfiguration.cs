using JudicialStatistics.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JudicialStatistics.Infrastructure.Persistence.Configurations;

public class OrganizationVersionConfiguration : IEntityTypeConfiguration<OrganizationVersion>
{
    public void Configure(EntityTypeBuilder<OrganizationVersion> builder)
    {
        builder.ToTable("OrganizationVersions");
        builder.HasKey(x => x.Id);

        // UNIQUE(JudicialYearId, VersionNo)
        builder.HasIndex(x => new { x.JudicialYearId, x.VersionNo }).IsUnique();

        builder.Property(x => x.DecisionReference).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(24).IsUnicode(false);
        builder.Property(x => x.ApprovedAt).HasColumnType("datetime2");
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasOne(x => x.JudicialYear)
            .WithMany(x => x.OrganizationVersions)
            .HasForeignKey(x => x.JudicialYearId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ApprovedBy)
            .WithMany()
            .HasForeignKey(x => x.ApprovedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.CircuitVersions)
            .WithOne(x => x.OrganizationVersion)
            .HasForeignKey(x => x.OrganizationVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ApprovedByUserId);
    }
}
