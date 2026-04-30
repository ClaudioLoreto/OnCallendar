using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnCallendar.Domain.Entities;

namespace OnCallendar.Infrastructure.Persistence.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> b)
    {
        b.ToTable("Tenants");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(200);
        b.Property(x => x.Slug).IsRequired().HasMaxLength(80);
        b.HasIndex(x => x.Slug).IsUnique();
        b.Property(x => x.TimeZoneId).HasMaxLength(80);
    }
}

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> b)
    {
        b.Property(x => x.FirstName).IsRequired().HasMaxLength(100);
        b.Property(x => x.LastName).IsRequired().HasMaxLength(100);
        b.Property(x => x.FiscalCode).HasMaxLength(32);
        b.Property(x => x.MedicalRegistrationNumber).HasMaxLength(64);
        b.Property(x => x.ExpoPushToken).HasMaxLength(256);
        b.Property(x => x.AvatarUrl).HasMaxLength(500);
        b.Property(x => x.Phone).HasMaxLength(40);
        b.Property(x => x.PreferredLanguage).HasMaxLength(8).HasDefaultValue("it");
        b.Property(x => x.ThemePreference).HasMaxLength(16).HasDefaultValue("system");

        b.HasOne(x => x.Tenant)
            .WithMany(t => t.Users)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.TenantId, x.Email });
    }
}

public class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> b)
    {
        b.ToTable("Shifts");
        b.HasKey(x => x.Id);
        b.Property(x => x.Location).HasMaxLength(200);
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.Property(x => x.Status).HasConversion<int>();
        b.Property(x => x.Capacity).HasDefaultValue(2);

        b.HasOne(x => x.Tenant)
            .WithMany(t => t.Shifts)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.TenantId, x.StartUtc, x.EndUtc });
    }
}

public class ShiftAssignmentConfiguration : IEntityTypeConfiguration<ShiftAssignment>
{
    public void Configure(EntityTypeBuilder<ShiftAssignment> b)
    {
        b.ToTable("ShiftAssignments");
        b.HasKey(x => x.Id);

        b.HasOne(x => x.Shift)
            .WithMany(s => s.Assignments)
            .HasForeignKey(x => x.ShiftId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Medico)
            .WithMany(u => u.ShiftAssignments)
            .HasForeignKey(x => x.MedicoId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.OriginatingSwapRequest)
            .WithMany()
            .HasForeignKey(x => x.OriginatingSwapRequestId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => new { x.TenantId, x.MedicoId, x.IsCurrent });
        b.HasIndex(x => new { x.ShiftId, x.IsCurrent });
    }
}

public class SwapRequestConfiguration : IEntityTypeConfiguration<SwapRequest>
{
    public void Configure(EntityTypeBuilder<SwapRequest> b)
    {
        b.ToTable("SwapRequests");
        b.HasKey(x => x.Id);
        b.Property(x => x.Type).HasConversion<int>();
        b.Property(x => x.Status).HasConversion<int>();
        b.Property(x => x.Message).HasMaxLength(1000);
        b.Property(x => x.ResolutionReason).HasMaxLength(1000);

        b.HasOne(x => x.InitiatorMedico)
            .WithMany()
            .HasForeignKey(x => x.InitiatorMedicoId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.InitiatorShift)
            .WithMany()
            .HasForeignKey(x => x.InitiatorShiftId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.CounterpartMedico)
            .WithMany()
            .HasForeignKey(x => x.CounterpartMedicoId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne(x => x.CounterpartShift)
            .WithMany()
            .HasForeignKey(x => x.CounterpartShiftId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => new { x.TenantId, x.Status });
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("AuditLogs");
        b.HasKey(x => x.Id);
        b.Property(x => x.EntityType).IsRequired().HasMaxLength(100);
        b.Property(x => x.Action).IsRequired().HasMaxLength(80);
        b.Property(x => x.PerformedByUserName).HasMaxLength(256);
        b.Property(x => x.OldValuesJson).HasColumnType("nvarchar(max)");
        b.Property(x => x.NewValuesJson).HasColumnType("nvarchar(max)");
        b.HasIndex(x => new { x.TenantId, x.EntityType, x.EntityId });
    }
}
