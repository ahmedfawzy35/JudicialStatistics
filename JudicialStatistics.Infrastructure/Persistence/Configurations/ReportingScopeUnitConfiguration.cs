using JudicialStatistics.Domain.Courts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JudicialStatistics.Infrastructure.Persistence.Configurations;

public class ReportingScopeUnitConfiguration : IEntityTypeConfiguration<ReportingScopeUnit>
{
    public void Configure(EntityTypeBuilder<ReportingScopeUnit> builder)
    {
        builder.ToTable("ReportingScopeUnits");
        builder.HasKey(x => x.Id);

        // UNIQUE(ReportingScopeId, CourtUnitId) — منع الحساب المزدوج
        builder.HasIndex(x => new { x.ReportingScopeId, x.CourtUnitId }).IsUnique();

        builder.HasOne(x => x.ReportingScope)
            .WithMany(x => x.ScopeUnits)
            .HasForeignKey(x => x.ReportingScopeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.CourtUnit)
            .WithMany()
            .HasForeignKey(x => x.CourtUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.CourtUnitId);
    }
}
