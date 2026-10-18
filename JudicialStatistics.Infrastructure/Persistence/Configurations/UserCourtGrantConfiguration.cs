using JudicialStatistics.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JudicialStatistics.Infrastructure.Persistence.Configurations;

public class UserCourtGrantConfiguration : IEntityTypeConfiguration<UserCourtGrant>
{
    public void Configure(EntityTypeBuilder<UserCourtGrant> builder)
    {
        builder.ToTable("UserCourtGrants");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SessionScopeMode)
            .IsRequired()
            .HasMaxLength(16)
            .IsUnicode(false)
            .HasDefaultValue("AllSessions");

        builder.Property(x => x.ValidFrom).IsRequired();
        builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);

        builder.HasOne(x => x.User)
            .WithMany(x => x.CourtGrants)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CourtUnit)
            .WithMany(x => x.UserGrants)
            .HasForeignKey(x => x.CourtUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Role)
            .WithMany(x => x.UserCourtGrants)
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.SessionGrants)
            .WithOne(x => x.CourtGrant)
            .HasForeignKey(x => x.CourtGrantId)
            .OnDelete(DeleteBehavior.Cascade);

        // فهارس
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.CourtUnitId);
        builder.HasIndex(x => x.RoleId);
    }
}
