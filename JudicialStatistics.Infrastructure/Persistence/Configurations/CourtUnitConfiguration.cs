using JudicialStatistics.Domain.Courts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JudicialStatistics.Infrastructure.Persistence.Configurations;

public class CourtUnitConfiguration : IEntityTypeConfiguration<CourtUnit>
{
    public void Configure(EntityTypeBuilder<CourtUnit> builder)
    {
        builder.ToTable("CourtUnits");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code)
            .IsRequired()
            .HasMaxLength(50)
            .IsUnicode(false);
        builder.HasIndex(x => x.Code).IsUnique();

        builder.Property(x => x.NameAr)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.UnitKind)
            .IsRequired()
            .HasMaxLength(24)
            .IsUnicode(false);

        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // علاقة ذاتية
        builder.HasOne(x => x.ParentCourtUnit)
            .WithMany(x => x.ChildUnits)
            .HasForeignKey(x => x.ParentCourtUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.ReportingScopes)
            .WithOne(x => x.RootCourtUnit)
            .HasForeignKey(x => x.RootCourtUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ParentCourtUnitId);
    }
}
