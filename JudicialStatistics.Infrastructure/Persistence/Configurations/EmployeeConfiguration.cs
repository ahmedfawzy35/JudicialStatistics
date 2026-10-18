using JudicialStatistics.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JudicialStatistics.Infrastructure.Persistence.Configurations;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).IsRequired().HasMaxLength(50).IsUnicode(false);
        builder.Property(x => x.FullNameAr).IsRequired().HasMaxLength(200);
        builder.Property(x => x.JobTitle).HasMaxLength(150);
        builder.Property(x => x.NationalId).HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.PhoneNumber).HasMaxLength(30).IsUnicode(false);

        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => x.FullNameAr);

        builder.HasOne(x => x.CourtUnit)
            .WithMany()
            .HasForeignKey(x => x.CourtUnitId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
