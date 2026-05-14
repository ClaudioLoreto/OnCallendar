using OnCallendar.Api.Controllers;

namespace OnCallendar.Api.Contracts;

// ── Users ──
public sealed record MedicoDto(Guid Id, string FullName, string Email, string? AvatarUrl);

public sealed record MeDto(
    Guid Id, string Email, string FirstName, string LastName,
    string? Phone, string? AvatarUrl,
    string PreferredLanguage, string ThemePreference,
    bool EmailConfirmed, bool PhoneConfirmed,
    bool IsDefaultEmail, string? PendingEmail,
    bool PasswordChangeRequired, bool PasswordExpired,
    DateTime? PasswordChangedAtUtc);

public sealed record UpdateMeRequest(
    string? FirstName = null, string? LastName = null,
    string? Email = null, string? Phone = null,
    string? AvatarUrl = null,
    string? PreferredLanguage = null, string? ThemePreference = null);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record RequestEmailChangeRequest(string NewEmail, string? ClientCallbackUrl = null);

// ── Shifts ──
public sealed record AssignExternalRequest(
    string FirstName, string LastName, string? Phone = null, string? Email = null, bool IsReperibile = false);

// ── Notifications ──
public sealed record NotificationDto(
    Guid Id, string Type, string? Title, string Message,
    string? Category, bool IsRead, Guid? RelatedEntityId,
    string? DataJson, DateTime CreatedAtUtc);

public sealed record PreferenceDto(string Type, string Channel, bool Enabled);
public sealed record SetPreferenceRequest(string Type, string Channel, bool Enabled);

// ── Device Tokens ──
public sealed record RegisterDeviceRequest(string Token, string Platform, string? DeviceName = null);

// ── External Doctors ──
public sealed record ExternalDoctorDto(
    Guid Id, string FirstName, string LastName, string FullName, string? Phone);

// ── Calendar ──
public sealed record DayDto(string Date, IReadOnlyList<ShiftDtos.ShiftDto> Shifts);
