using JudicialStatistics.Domain.Geography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JudicialStatistics.Infrastructure.Persistence.Configurations;

public class GeographicAreaConfiguration : IEntityTypeConfiguration<GeographicArea>
{
    public void Configure(EntityTypeBuilder<GeographicArea> builder)
    {
        builder.ToTable("GeographicAreas");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).IsRequired().HasMaxLength(50).IsUnicode(false);
        builder.HasIndex(x => x.Code).IsUnique();

        builder.Property(x => x.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(x => x.AreaKind).IsRequired().HasMaxLength(24).IsUnicode(false);
        builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);

        // علاقة ذاتية
        builder.HasOne(x => x.ParentArea)
            .WithMany(x => x.ChildAreas)
            .HasForeignKey(x => x.ParentAreaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Jurisdictions)
            .WithOne(x => x.GeographicArea)
            .HasForeignKey(x => x.GeographicAreaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ParentAreaId);
    }
}

public class SessionJurisdictionConfiguration : IEntityTypeConfiguration<SessionJurisdiction>
{
    public void Configure(EntityTypeBuilder<SessionJurisdiction> builder)
    {
        builder.ToTable("SessionJurisdictions");
        builder.HasKey(x => x.Id);

        // UNIQUE(SessionVersionId, GeographicAreaId)
        builder.HasIndex(x => new { x.SessionVersionId, x.GeographicAreaId }).IsUnique();

        builder.HasOne(x => x.SessionVersion)
            .WithMany(x => x.Jurisdictions)
            .HasForeignKey(x => x.SessionVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.GeographicArea)
            .WithMany(x => x.Jurisdictions)
            .HasForeignKey(x => x.GeographicAreaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.GeographicAreaId);
    }
}
