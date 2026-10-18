using JudicialStatistics.Domain.Years;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JudicialStatistics.Infrastructure.Persistence.Configurations;

public class StatisticalPeriodConfiguration : IEntityTypeConfiguration<StatisticalPeriod>
{
    public void Configure(EntityTypeBuilder<StatisticalPeriod> builder)
    {
        builder.ToTable("StatisticalPeriods");
        builder.HasKey(x => x.Id);

        // UNIQUE(JudicialYearId, MonthStartDate)
        builder.HasIndex(x => new { x.JudicialYearId, x.MonthStartDate }).IsUnique();

        builder.Property(x => x.IsOpenForEntry)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasOne(x => x.JudicialYear)
            .WithMany(x => x.Periods)
            .HasForeignKey(x => x.JudicialYearId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
