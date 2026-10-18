using JudicialStatistics.Domain.Years;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JudicialStatistics.Infrastructure.Persistence.Configurations;

public class JudicialYearConfiguration : IEntityTypeConfiguration<JudicialYear>
{
    public void Configure(EntityTypeBuilder<JudicialYear> builder)
    {
        builder.ToTable("JudicialYears");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.NameAr)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(24)
            .IsUnicode(false);

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        // لا تتداخل الأعوام داخل نطاق التقرير — يُفحص بـ Business Rule
        builder.HasIndex(x => x.ReportingScopeId);

        builder.HasOne(x => x.ReportingScope)
            .WithMany()
            .HasForeignKey(x => x.ReportingScopeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Periods)
            .WithOne(x => x.JudicialYear)
            .HasForeignKey(x => x.JudicialYearId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.OrganizationVersions)
            .WithOne(x => x.JudicialYear)
            .HasForeignKey(x => x.JudicialYearId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
