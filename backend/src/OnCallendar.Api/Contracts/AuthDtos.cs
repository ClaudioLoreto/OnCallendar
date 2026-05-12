namespace OnCallendar.Api.Contracts;

// ── Auth ──
public sealed record LoginRequest(string Email, string Password);

public sealed record LoginResponse(
    string Token, DateTime ExpiresAtUtc,
    Guid UserId, string Email, string FullName, string Role,
    Guid? TenantId, bool PasswordExpired);

public sealed record RegisterMedicoRequest(
    string Email, string Password,
    string FirstName, string LastName,
    Guid TenantId,
    string? FiscalCode = null,
    string? MedicalRegistrationNumber = null);

public sealed record ForgotPasswordRequest(string Email, string? ClientCallbackUrl = null);

public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);

public sealed record ExternalInviteInfoDto(string FirstName, string LastName, string? Email);

public sealed record RegisterExternalRequest(string Token, string Email, string Password);

public sealed record ConfirmEmailChangeRequest(string Token);

public sealed record DevResetPasswordRequest(string Email, string NewPassword);
