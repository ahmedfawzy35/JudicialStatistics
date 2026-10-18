using JudicialStatistics.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JudicialStatistics.Infrastructure.Persistence.Configurations;

public class StatisticalDisciplineConfiguration : IEntityTypeConfiguration<StatisticalDiscipline>
{
    public void Configure(EntityTypeBuilder<StatisticalDiscipline> builder)
    {
        builder.ToTable("StatisticalDisciplines");
        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.Code).IsUnique();

        builder.Property(x => x.Code).IsRequired().HasMaxLength(50).IsUnicode(false);
        builder.Property(x => x.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Icon).HasMaxLength(50);
        builder.Property(x => x.CircuitKinds).HasMaxLength(500);
        builder.Property(x => x.EntryFormKind).HasMaxLength(50);
    }
}
