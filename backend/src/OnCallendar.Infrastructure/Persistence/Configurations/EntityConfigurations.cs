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

        b.HasOne(x => x.ExternalDoctor)
            .WithMany()
            .HasForeignKey(x => x.ExternalDoctorId)
            .OnDelete(DeleteBehavior.SetNull);

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
        b.HasIndex(x => new { x.InitiatorShiftId, x.Status });
    }
}

public class SwapCounterOfferConfiguration : IEntityTypeConfiguration<SwapCounterOffer>
{
    public void Configure(EntityTypeBuilder<SwapCounterOffer> b)
    {
        b.ToTable("SwapCounterOffers");
        b.HasKey(x => x.Id);

        b.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        b.Property(x => x.Message).HasMaxLength(1000);

        b.HasOne(x => x.SwapRequest)
            .WithMany(r => r.CounterOffers)
            .HasForeignKey(x => x.SwapRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.ProposedByMedico)
            .WithMany()
            .HasForeignKey(x => x.ProposedByMedicoId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.OfferedShift)
            .WithMany()
            .HasForeignKey(x => x.OfferedShiftId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.SwapRequestId);
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

public class ExternalDoctorConfiguration : IEntityTypeConfiguration<ExternalDoctor>
{
    public void Configure(EntityTypeBuilder<ExternalDoctor> b)
    {
        b.ToTable("ExternalDoctors");
        b.HasKey(x => x.Id);
        b.Property(x => x.FirstName).IsRequired().HasMaxLength(80);
        b.Property(x => x.LastName).IsRequired().HasMaxLength(80);
        b.Property(x => x.NormalizedKey).IsRequired().HasMaxLength(170);
        b.Property(x => x.Phone).HasMaxLength(40);
        b.Property(x => x.Email).HasMaxLength(256);
        b.Property(x => x.InviteToken).HasMaxLength(128);
        b.Property(x => x.Notes).HasMaxLength(500);
        b.Ignore(x => x.FullName);
        b.HasIndex(x => x.InviteToken).IsUnique().HasFilter("\"InviteToken\" IS NOT NULL");

        b.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // Una stessa persona (tenant + nome+cognome normalizzati) viene censita
        // una sola volta e poi riproposta come suggerimento per gli utilizzi
        // successivi.
        b.HasIndex(x => new { x.TenantId, x.NormalizedKey }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.LastName });
    }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("Notifications");
        b.HasKey(x => x.Id);
        b.Property(x => x.Type).IsRequired().HasMaxLength(50);
        b.Property(x => x.Title).HasMaxLength(200);
        b.Property(x => x.Message).IsRequired().HasMaxLength(1000);
        b.Property(x => x.Category).HasMaxLength(20);
        // DataJson: nvarchar(max) / text — provider agnostic.
        b.Property(x => x.DataJson);

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAtUtc });
        b.HasIndex(x => new { x.TenantId, x.Type });
    }
}

public class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> b)
    {
        b.ToTable("NotificationPreferences");
        b.HasKey(x => x.Id);
        b.Property(x => x.Type).IsRequired().HasMaxLength(50);
        b.Property(x => x.Channel).IsRequired().HasMaxLength(16);

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Una sola riga per (utente, evento, canale).
        b.HasIndex(x => new { x.UserId, x.Type, x.Channel }).IsUnique();
    }
}

public class UserDeviceTokenConfiguration : IEntityTypeConfiguration<UserDeviceToken>
{
    public void Configure(EntityTypeBuilder<UserDeviceToken> b)
    {
        b.ToTable("UserDeviceTokens");
        b.HasKey(x => x.Id);
        b.Property(x => x.Token).IsRequired().HasMaxLength(256);
        b.Property(x => x.Platform).IsRequired().HasMaxLength(16);
        b.Property(x => x.DeviceName).HasMaxLength(120);

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Stesso device si registra più volte: il token Expo è univoco per
        // device, quindi su (UserId, Token) garantisce idempotenza.
        b.HasIndex(x => new { x.UserId, x.Token }).IsUnique();
        b.HasIndex(x => x.Token);
    }
}
