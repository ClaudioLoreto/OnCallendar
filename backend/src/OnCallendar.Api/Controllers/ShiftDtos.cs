using Microsoft.AspNetCore.Mvc;
using OnCallendar.Domain.Entities;
using OnCallendar.Domain.Enums;
using OnCallendar.Infrastructure.Persistence.Seed;

namespace OnCallendar.Api.Controllers;

/// <summary>DTO condivisi tra i controller del calendario.</summary>
public static class ShiftDtos
{
    public sealed record MedicoRefDto(Guid Id, string FullName, string? AvatarUrl, int? Number);

    /// <summary>Riferimento a un medico ESTERNO (non utente dell'app).</summary>
    public sealed record ExternalDoctorRefDto(Guid Id, string FullName, string FirstName, string LastName);

    public sealed record ShiftDto(
        Guid Id,
        string Date,           // yyyy-MM-dd (locale Europe/Rome)
        string Code,           // F | FN | P | PN | N
        string CodeLabel,      // "Festivo Diurno", ecc.
        DateTime StartUtc,
        DateTime EndUtc,
        string StartLocal,     // "08:00"
        string EndLocal,       // "20:00"
        ShiftStatus Status,
        MedicoRefDto? MedicoTurno,
        MedicoRefDto? MedicoReperibile,
        ExternalDoctorRefDto? ExternalDoctor,
        bool IsMineTurno,
        bool IsMineReperibile,
        bool IsPast);

    public static MedicoRefDto? MapMedico(ApplicationUser? u)
        => u is null ? null : new MedicoRefDto(u.Id, $"{u.FirstName} {u.LastName}".Trim(), u.AvatarUrl, u.MedicoNumber);

    public static ExternalDoctorRefDto? MapExternal(OnCallendar.Domain.Entities.ExternalDoctor? e)
        => e is null ? null : new ExternalDoctorRefDto(e.Id, e.FullName, e.FirstName, e.LastName);

    public static ShiftDto Map(Shift s, Guid currentUid)
    {
        var (startLocal, endLocal) = DbSeeder.ComputeLocalWindow(s.Date, s.Code);
        return new ShiftDto(
            s.Id,
            s.Date.ToString("yyyy-MM-dd"),
            s.Code.ToString(),
            CodeLabel(s.Code),
            s.StartUtc,
            s.EndUtc,
            startLocal.ToString("HH:mm"),
            endLocal.ToString("HH:mm"),
            s.Status,
            MapMedico(s.MedicoTurno),
            MapMedico(s.MedicoReperibile),
            MapExternal(s.ExternalDoctor),
            IsMineTurno: s.MedicoTurnoId == currentUid,
            IsMineReperibile: s.MedicoReperibileId == currentUid,
            IsPast: s.EndUtc <= DateTime.UtcNow);
    }

    public static string CodeLabel(ShiftCode c) => c switch
    {
        ShiftCode.F  => "Festivo Diurno",
        ShiftCode.FN => "Festivo Notte",
        ShiftCode.P  => "Prefestivo Diurno",
        ShiftCode.PN => "Prefestivo Notte",
        ShiftCode.N  => "Notte Infrasettimanale",
        _ => c.ToString()
    };
}
