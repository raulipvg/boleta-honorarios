namespace GestionIngresosHonorarios.Application.DTOs;

public sealed record LoginRequestDto(string UserName, string Password);
public sealed record AuthTokenDto(string AccessToken, int ExpiresInSeconds, bool RequiresPasswordChange);
public sealed record AuthSessionResult(AuthTokenDto Token, string? RefreshToken, DateTimeOffset? RefreshExpiresAt);
public sealed record AuthIdentityDto(Guid UserId, string UserName, string[] RoleCodes, string[] PermissionCodes, bool RequiresPasswordChange);
public sealed record CreateAccountRequestDto(string UserName, string TemporaryPassword, string[] RoleCodes, string? ProfessionalName);
public sealed record ResetPasswordRequestDto(string TemporaryPassword);
public sealed record AccountSummaryDto(Guid Id, string UserName, bool Active, bool MustChangePassword, string[] RoleCodes, string? ProfessionalName);
