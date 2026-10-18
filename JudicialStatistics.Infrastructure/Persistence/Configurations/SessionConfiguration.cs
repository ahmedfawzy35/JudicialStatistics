using JudicialStatistics.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JudicialStatistics.Infrastructure.Persistence.Configurations;

public class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("Sessions");
        builder.HasKey(x => x.Id);

        // UNIQUE(CircuitId, Code)
        builder.HasIndex(x => new { x.CircuitId, x.Code }).IsUnique();

        builder.Property(x => x.Code).IsRequired().HasMaxLength(50).IsUnicode(false);
        builder.Property(x => x.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.ShowInProgressReport).HasDefaultValue(true);

        builder.HasOne(x => x.Circuit)
            .WithMany(x => x.Sessions)
            .HasForeignKey(x => x.CircuitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Versions)
            .WithOne(x => x.Session)
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.UserSessionGrants)
            .WithOne(x => x.Session)
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Employee)
            .WithMany(x => x.Sessions)
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.CircuitId);
        builder.HasIndex(x => x.EmployeeId);
    }
}
