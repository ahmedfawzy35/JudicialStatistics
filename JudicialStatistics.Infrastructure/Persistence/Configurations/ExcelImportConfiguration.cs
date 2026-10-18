using JudicialStatistics.Domain.Import;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JudicialStatistics.Infrastructure.Persistence.Configurations;

public class ExcelImportTemplateConfiguration : IEntityTypeConfiguration<ExcelImportTemplate>
{
    public void Configure(EntityTypeBuilder<ExcelImportTemplate> builder)
    {
        builder.ToTable("ExcelImportTemplates");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.NameAr).HasMaxLength(150).IsRequired();
        builder.Property(t => t.DisciplineKind).HasMaxLength(50).IsRequired();
        builder.Property(t => t.StatisticsSheetName).HasMaxLength(100).IsRequired();
        builder.Property(t => t.TashkeelSheetName).HasMaxLength(100).IsRequired();

        builder.HasMany(t => t.ColumnMappings)
               .WithOne(m => m.ExcelImportTemplate)
               .HasForeignKey(m => m.ExcelImportTemplateId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ExcelImportColumnMappingConfiguration : IEntityTypeConfiguration<ExcelImportColumnMapping>
{
    public void Configure(EntityTypeBuilder<ExcelImportColumnMapping> builder)
    {
        builder.ToTable("ExcelImportColumnMappings");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Section).HasMaxLength(50).IsRequired();
        builder.Property(m => m.ColumnLetter).HasMaxLength(10).IsRequired();
        builder.Property(m => m.TargetItemCode).HasMaxLength(50).IsRequired();
        builder.Property(m => m.TargetItemNameAr).HasMaxLength(150).IsRequired();
    }
}
