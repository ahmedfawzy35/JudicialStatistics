using JudicialStatistics.Domain.Rules;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JudicialStatistics.Infrastructure.Persistence.Configurations;

public class JudgmentTypeConfiguration : IEntityTypeConfiguration<JudgmentType>
{
    public void Configure(EntityTypeBuilder<JudgmentType> builder)
    {
        builder.ToTable("JudgmentTypes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(50).IsUnicode(false);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
    }
}

public class IncomingTypeConfiguration : IEntityTypeConfiguration<IncomingType>
{
    public void Configure(EntityTypeBuilder<IncomingType> builder)
    {
        builder.ToTable("IncomingTypes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(50).IsUnicode(false);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(x => x.InputSource).IsRequired().HasMaxLength(24).IsUnicode(false);
    }
}

public class DeferralReasonConfiguration : IEntityTypeConfiguration<DeferralReason>
{
    public void Configure(EntityTypeBuilder<DeferralReason> builder)
    {
        builder.ToTable("DeferralReasons");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(50).IsUnicode(false);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(x => x.SourceKind).IsRequired().HasMaxLength(24).IsUnicode(false);
    }
}

public class ProductionItemTypeConfiguration : IEntityTypeConfiguration<ProductionItemType>
{
    public void Configure(EntityTypeBuilder<ProductionItemType> builder)
    {
        builder.ToTable("ProductionItemTypes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(50).IsUnicode(false);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(x => x.ItemKind).IsRequired().HasMaxLength(24).IsUnicode(false);
        builder.Property(x => x.EntryScreen).IsRequired().HasMaxLength(24).IsUnicode(false);

        builder.HasOne(x => x.DeferralReason)
            .WithMany()
            .HasForeignKey(x => x.DeferralReasonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.DeferralReasonId);
    }
}

public class JudgeActionTypeConfiguration : IEntityTypeConfiguration<JudgeActionType>
{
    public void Configure(EntityTypeBuilder<JudgeActionType> builder)
    {
        builder.ToTable("JudgeActionTypes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(50).IsUnicode(false);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.NameAr).IsRequired().HasMaxLength(200);

        builder.HasOne(x => x.DeferralReason)
            .WithMany()
            .HasForeignKey(x => x.DeferralReasonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.DeferralReasonId);
    }
}
