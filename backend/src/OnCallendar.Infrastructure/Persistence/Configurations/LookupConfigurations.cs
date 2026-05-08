using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnCallendar.Domain.Entities.Lookup;
using OnCallendar.Domain.Enums;

namespace OnCallendar.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configurazioni delle tabelle di lookup (cataloghi).
/// PK testuale = Code, popolata dalla migration via HasData.
/// </summary>
public sealed class RoleTypeConfiguration : IEntityTypeConfiguration<RoleType>
{
    public void Configure(EntityTypeBuilder<RoleType> b)
    {
        b.ToTable("RoleTypes");
        b.HasKey(x => x.Code);
        b.Property(x => x.Code).HasMaxLength(40).IsRequired();
        b.Property(x => x.Description).HasMaxLength(200).IsRequired();

        b.HasData(
            new RoleType { Code = nameof(UserRole.SuperAdmin), Description = "Amministratore globale" },
            new RoleType { Code = nameof(UserRole.Medico),     Description = "Medico di guardia" }
        );
    }
}

public sealed class ShiftTypeConfiguration : IEntityTypeConfiguration<ShiftType>
{
    public void Configure(EntityTypeBuilder<ShiftType> b)
    {
        b.ToTable("ShiftTypes");
        b.HasKey(x => x.Code);
        b.Property(x => x.Code).HasMaxLength(8).IsRequired();
        b.Property(x => x.Description).HasMaxLength(200).IsRequired();

        b.HasData(
            new ShiftType { Code = nameof(ShiftCode.F),  Description = "Festivo Diurno",            StartHourLocal = 8,  EndHourLocal = 20, IsOvernight = false },
            new ShiftType { Code = nameof(ShiftCode.FN), Description = "Festivo Notte",             StartHourLocal = 20, EndHourLocal = 8,  IsOvernight = true },
            new ShiftType { Code = nameof(ShiftCode.P),  Description = "Prefestivo Diurno",         StartHourLocal = 10, EndHourLocal = 20, IsOvernight = false },
            new ShiftType { Code = nameof(ShiftCode.PN), Description = "Prefestivo Notte",          StartHourLocal = 20, EndHourLocal = 8,  IsOvernight = true },
            new ShiftType { Code = nameof(ShiftCode.N),  Description = "Notte Infrasettimanale",    StartHourLocal = 20, EndHourLocal = 8,  IsOvernight = true }
        );
    }
}

public sealed class ShiftStatusTypeConfiguration : IEntityTypeConfiguration<ShiftStatusType>
{
    public void Configure(EntityTypeBuilder<ShiftStatusType> b)
    {
        b.ToTable("ShiftStatuses");
        b.HasKey(x => x.Code);
        b.Property(x => x.Code).HasMaxLength(20).IsRequired();
        b.Property(x => x.Description).HasMaxLength(200).IsRequired();

        b.HasData(
            new ShiftStatusType { Code = nameof(ShiftStatus.Assigned),  Description = "Assegnato" },
            new ShiftStatusType { Code = nameof(ShiftStatus.OnBoard),   Description = "Pubblicato in bacheca" },
            new ShiftStatusType { Code = nameof(ShiftStatus.Completed), Description = "Completato" },
            new ShiftStatusType { Code = nameof(ShiftStatus.Cancelled), Description = "Annullato" }
        );
    }
}

public sealed class SwapRequestTypeLookupConfiguration : IEntityTypeConfiguration<SwapRequestTypeLookup>
{
    public void Configure(EntityTypeBuilder<SwapRequestTypeLookup> b)
    {
        b.ToTable("SwapRequestTypes");
        b.HasKey(x => x.Code);
        b.Property(x => x.Code).HasMaxLength(20).IsRequired();
        b.Property(x => x.Description).HasMaxLength(200).IsRequired();

        b.HasData(
            new SwapRequestTypeLookup { Code = nameof(SwapRequestType.Giveaway),      Description = "Cessione" },
            new SwapRequestTypeLookup { Code = nameof(SwapRequestType.Swap),          Description = "Scambio" },
            new SwapRequestTypeLookup { Code = nameof(SwapRequestType.PickFromBoard), Description = "Presa dalla bacheca" }
        );
    }
}

public sealed class SwapRequestStatusTypeConfiguration : IEntityTypeConfiguration<SwapRequestStatusType>
{
    public void Configure(EntityTypeBuilder<SwapRequestStatusType> b)
    {
        b.ToTable("SwapRequestStatuses");
        b.HasKey(x => x.Code);
        b.Property(x => x.Code).HasMaxLength(20).IsRequired();
        b.Property(x => x.Description).HasMaxLength(200).IsRequired();

        b.HasData(
            new SwapRequestStatusType { Code = nameof(SwapRequestStatus.Pending),         Description = "In attesa" },
            new SwapRequestStatusType { Code = nameof(SwapRequestStatus.AutoApproved),    Description = "Approvata automaticamente" },
            new SwapRequestStatusType { Code = nameof(SwapRequestStatus.Rejected),        Description = "Rifiutata" },
            new SwapRequestStatusType { Code = nameof(SwapRequestStatus.Cancelled),       Description = "Annullata" },
            new SwapRequestStatusType { Code = nameof(SwapRequestStatus.BlockedByRules),  Description = "Bloccata dalle regole" }
        );
    }
}
