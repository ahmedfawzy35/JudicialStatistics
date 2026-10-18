using JudicialStatistics.Domain.Courts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JudicialStatistics.Infrastructure.Persistence.Configurations;

public class ReportingScopeConfiguration : IEntityTypeConfiguration<ReportingScope>
{
    public void Configure(EntityTypeBuilder<ReportingScope> builder)
    {
        builder.ToTable("ReportingScopes");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code)
            .IsRequired()
            .HasMaxLength(50)
            .IsUnicode(false);
        builder.HasIndex(x => x.Code).IsUnique();

        builder.Property(x => x.NameAr)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasOne(x => x.RootCourtUnit)
            .WithMany(x => x.ReportingScopes)
            .HasForeignKey(x => x.RootCourtUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.ScopeUnits)
            .WithOne(x => x.ReportingScope)
            .HasForeignKey(x => x.ReportingScopeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.RootCourtUnitId);
    }
}
