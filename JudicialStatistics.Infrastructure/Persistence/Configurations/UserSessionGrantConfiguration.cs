using JudicialStatistics.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JudicialStatistics.Infrastructure.Persistence.Configurations;

public class UserSessionGrantConfiguration : IEntityTypeConfiguration<UserSessionGrant>
{
    public void Configure(EntityTypeBuilder<UserSessionGrant> builder)
    {
        builder.ToTable("UserSessionGrants");
        builder.HasKey(x => x.Id);

        // UNIQUE(CourtGrantId, SessionId)
        builder.HasIndex(x => new { x.CourtGrantId, x.SessionId }).IsUnique();

        builder.HasOne(x => x.CourtGrant)
            .WithMany(x => x.SessionGrants)
            .HasForeignKey(x => x.CourtGrantId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK إلى Sessions
        builder.HasOne(x => x.Session)
            .WithMany(x => x.UserSessionGrants)
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
