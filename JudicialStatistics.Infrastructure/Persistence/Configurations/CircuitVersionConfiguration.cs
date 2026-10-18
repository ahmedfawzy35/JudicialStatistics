using JudicialStatistics.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JudicialStatistics.Infrastructure.Persistence.Configurations;

public class CircuitVersionConfiguration : IEntityTypeConfiguration<CircuitVersion>
{
    public void Configure(EntityTypeBuilder<CircuitVersion> builder)
    {
        builder.ToTable("CircuitVersions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CircuitKind)
            .IsRequired().HasMaxLength(32).IsUnicode(false);

        builder.HasOne(x => x.Circuit)
            .WithMany(x => x.Versions)
            .HasForeignKey(x => x.CircuitId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.OrganizationVersion)
            .WithMany(x => x.CircuitVersions)
            .HasForeignKey(x => x.OrganizationVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.SessionVersions)
            .WithOne(x => x.CircuitVersion)
            .HasForeignKey(x => x.CircuitVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.JudgeAssignments)
            .WithOne(x => x.CircuitVersion)
            .HasForeignKey(x => x.CircuitVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.CircuitId);
        builder.HasIndex(x => x.OrganizationVersionId);
    }
}
