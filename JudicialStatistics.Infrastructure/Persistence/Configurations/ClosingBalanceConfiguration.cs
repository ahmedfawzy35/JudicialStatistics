using JudicialStatistics.Domain.Statistics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JudicialStatistics.Infrastructure.Persistence.Configurations;

public class ClosingBalanceConfiguration : IEntityTypeConfiguration<ClosingBalance>
{
    public void Configure(EntityTypeBuilder<ClosingBalance> builder)
    {
        builder.ToTable("ClosingBalances");
        builder.HasKey(x => x.Id);

        // UNIQUE(StatisticsHeaderId, CaseTypeId, CaseYearBandId)
        builder.HasIndex(x => new { x.StatisticsHeaderId, x.CaseTypeId, x.CaseYearBandId }).IsUnique();

        builder.HasOne(x => x.StatisticsHeader).WithMany()
            .HasForeignKey(x => x.StatisticsHeaderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.CaseType).WithMany()
            .HasForeignKey(x => x.CaseTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CaseYearBand).WithMany()
            .HasForeignKey(x => x.CaseYearBandId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.StatisticsHeaderId);
    }
}
