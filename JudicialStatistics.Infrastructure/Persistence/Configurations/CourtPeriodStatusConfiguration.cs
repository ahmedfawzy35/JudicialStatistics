using JudicialStatistics.Domain.Years;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JudicialStatistics.Infrastructure.Persistence.Configurations;

public class CourtPeriodStatusConfiguration : IEntityTypeConfiguration<CourtPeriodStatus>
{
    public void Configure(EntityTypeBuilder<CourtPeriodStatus> builder)
    {
        builder.ToTable("CourtPeriodStatuses");
        builder.HasKey(x => x.Id);

        // UNIQUE(CourtUnitId, StatisticalPeriodId)
        builder.HasIndex(x => new { x.CourtUnitId, x.StatisticalPeriodId }).IsUnique();

        builder.Property(x => x.IsOpenForEntry)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasOne(x => x.CourtUnit)
            .WithMany()
            .HasForeignKey(x => x.CourtUnitId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.StatisticalPeriod)
            .WithMany()
            .HasForeignKey(x => x.StatisticalPeriodId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.UpdatedByUser)
            .WithMany()
            .HasForeignKey(x => x.UpdatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
