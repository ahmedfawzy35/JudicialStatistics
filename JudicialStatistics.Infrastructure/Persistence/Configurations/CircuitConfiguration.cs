using JudicialStatistics.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JudicialStatistics.Infrastructure.Persistence.Configurations;

public class CircuitConfiguration : IEntityTypeConfiguration<Circuit>
{
    public void Configure(EntityTypeBuilder<Circuit> builder)
    {
        builder.ToTable("Circuits");
        builder.HasKey(x => x.Id);

        // UNIQUE(JudicialYearId, CourtUnitId, Code)
        builder.HasIndex(x => new { x.JudicialYearId, x.CourtUnitId, x.Code }).IsUnique();

        builder.Property(x => x.Code).IsRequired().HasMaxLength(50).IsUnicode(false);
        builder.Property(x => x.NameAr).IsRequired().HasMaxLength(200);

        builder.HasOne(x => x.JudicialYear)
            .WithMany()
            .HasForeignKey(x => x.JudicialYearId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CourtUnit)
            .WithMany()
            .HasForeignKey(x => x.CourtUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Versions)
            .WithOne(x => x.Circuit)
            .HasForeignKey(x => x.CircuitId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Sessions)
            .WithOne(x => x.Circuit)
            .HasForeignKey(x => x.CircuitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.CourtUnitId);
        builder.HasIndex(x => x.JudicialYearId);
    }
}
