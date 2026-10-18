using JudicialStatistics.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JudicialStatistics.Infrastructure.Persistence.Configurations;

public class SessionVersionConfiguration : IEntityTypeConfiguration<SessionVersion>
{
    public void Configure(EntityTypeBuilder<SessionVersion> builder)
    {
        builder.ToTable("SessionVersions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ScheduleDescription).HasMaxLength(200);

        builder.HasOne(x => x.Session)
            .WithMany(x => x.Versions)
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.CircuitVersion)
            .WithMany(x => x.SessionVersions)
            .HasForeignKey(x => x.CircuitVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Jurisdictions)
            .WithOne(x => x.SessionVersion)
            .HasForeignKey(x => x.SessionVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.SessionId);
        builder.HasIndex(x => x.CircuitVersionId);
    }
}
