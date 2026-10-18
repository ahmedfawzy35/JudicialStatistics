using JudicialStatistics.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JudicialStatistics.Infrastructure.Persistence.Configurations;

public class JudgeConfiguration : IEntityTypeConfiguration<Judge>
{
    public void Configure(EntityTypeBuilder<Judge> builder)
    {
        builder.ToTable("Judges");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).IsRequired().HasMaxLength(50).IsUnicode(false);
        builder.HasIndex(x => x.Code).IsUnique();

        builder.Property(x => x.FullNameAr).IsRequired().HasMaxLength(200);
        builder.HasIndex(x => x.FullNameAr).IsUnique();
        builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);

        builder.HasOne(x => x.CreatedCourtUnit)
            .WithMany()
            .HasForeignKey(x => x.CreatedCourtUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Assignments)
            .WithOne(x => x.Judge)
            .HasForeignKey(x => x.JudgeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}


public class JudicialRoleConfiguration : IEntityTypeConfiguration<JudicialRole>
{
    public void Configure(EntityTypeBuilder<JudicialRole> builder)
    {
        builder.ToTable("JudicialRoles");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).IsRequired().HasMaxLength(50).IsUnicode(false);
        builder.HasIndex(x => x.Code).IsUnique();

        builder.Property(x => x.NameAr).IsRequired().HasMaxLength(200);

        builder.HasMany(x => x.Assignments)
            .WithOne(x => x.JudicialRole)
            .HasForeignKey(x => x.JudicialRoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class CircuitJudgeAssignmentConfiguration : IEntityTypeConfiguration<CircuitJudgeAssignment>
{
    public void Configure(EntityTypeBuilder<CircuitJudgeAssignment> builder)
    {
        builder.ToTable("CircuitJudgeAssignments");
        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.CircuitVersion)
            .WithMany(x => x.JudgeAssignments)
            .HasForeignKey(x => x.CircuitVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Judge)
            .WithMany(x => x.Assignments)
            .HasForeignKey(x => x.JudgeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.JudicialRole)
            .WithMany(x => x.Assignments)
            .HasForeignKey(x => x.JudicialRoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.CircuitVersionId);
        builder.HasIndex(x => x.JudgeId);
        builder.HasIndex(x => x.JudicialRoleId);
    }
}

public class JudgeMovementConfiguration : IEntityTypeConfiguration<JudgeMovement>
{
    public void Configure(EntityTypeBuilder<JudgeMovement> builder)
    {
        builder.ToTable("JudgeMovements");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.MovementType).IsRequired().HasMaxLength(50);
        builder.Property(x => x.DecisionNumber).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Reason).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Notes).HasMaxLength(1000);

        builder.HasOne(x => x.Judge)
            .WithMany()
            .HasForeignKey(x => x.JudgeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Circuit)
            .WithMany()
            .HasForeignKey(x => x.CircuitId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.FromCircuit)
            .WithMany()
            .HasForeignKey(x => x.FromCircuitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ToCircuit)
            .WithMany()
            .HasForeignKey(x => x.ToCircuitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.JudicialRole)
            .WithMany()
            .HasForeignKey(x => x.JudicialRoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.JudgeId);
        builder.HasIndex(x => x.CircuitId);
        builder.HasIndex(x => x.FromCircuitId);
        builder.HasIndex(x => x.ToCircuitId);
        builder.HasIndex(x => x.JudicialRoleId);
        builder.HasIndex(x => x.DecisionDate);
    }
}
