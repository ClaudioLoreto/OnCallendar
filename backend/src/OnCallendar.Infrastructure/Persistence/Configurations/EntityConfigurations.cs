using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnCallendar.Domain.Entities;
using OnCallendar.Domain.Entities.Lookup;
using OnCallendar.Infrastructure.Persistence;

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
        b.Property(x => x.Address).HasMaxLength(400);
        b.Property(x => x.FiscalCode).HasMaxLength(32);
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
        b.Property(x => x.Badge).HasMaxLength(16);

        // Role: persistito come stringa (Code). Il FK constraint verso
        // la tabella RoleTypes viene aggiunto a mano nella migration
        // (EF non supporta FK con value converter sul tipo della colonna).
        b.Property(x => x.Role)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        b.HasOne(x => x.Tenant)
            .WithMany(t => t.Users)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.TenantId, x.MedicoNumber });
        // Filter sintassi SqlServer-only: in Postgres NULL ≠ NULL già di default,
        // quindi un indice unique nullable non genera collisioni.
        var badgeIndex = b.HasIndex(x => x.Badge).IsUnique();
        if (DatabaseProviderHelper.IsSqlServer)
            badgeIndex.HasFilter("[Badge] IS NOT NULL");
    }
}

public class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> b)
    {
        b.ToTable("Shifts");
        b.HasKey(x => x.Id);
        b.Property(x => x.Notes).HasMaxLength(1000);

        // Code (ShiftCode) e Status (ShiftStatus): stringa. FK a ShiftTypes/
        // ShiftStatuses aggiunti manualmente nella migration.
        b.Property(x => x.Code)
            .HasConversion<string>()
            .HasMaxLength(8)
            .IsRequired();

        b.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        b.HasOne(x => x.Tenant)
            .WithMany(t => t.Shifts)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.MedicoTurno)
            .WithMany()
            .HasForeignKey(x => x.MedicoTurnoId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.MedicoReperibile)
            .WithMany()
            .HasForeignKey(x => x.MedicoReperibileId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.TenantId, x.Date });
        b.HasIndex(x => new { x.TenantId, x.StartUtc });
        b.HasIndex(x => new { x.TenantId, x.MedicoTurnoId });
    }
}

public class SwapRequestConfiguration : IEntityTypeConfiguration<SwapRequest>
{
    public void Configure(EntityTypeBuilder<SwapRequest> b)
    {
        b.ToTable("SwapRequests");
        b.HasKey(x => x.Id);

        b.Property(x => x.Type)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        b.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

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
        // Provider-agnostic: SqlServer mappa string→nvarchar(max), Npgsql→text.
        b.Property(x => x.OldValuesJson);
        b.Property(x => x.NewValuesJson);
        b.HasIndex(x => new { x.TenantId, x.EntityType, x.EntityId });
    }
}
