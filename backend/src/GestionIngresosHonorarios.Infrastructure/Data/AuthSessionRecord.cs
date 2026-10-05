namespace GestionIngresosHonorarios.Infrastructure.Data;

public sealed class AuthSessionRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid Sid { get; set; }
    public Guid RefreshFamilyId { get; set; }
    public string RefreshTokenHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastUsedAt { get; set; }
    public DateTimeOffset IdleExpiresAt { get; set; }
    public DateTimeOffset AbsoluteExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}

public sealed class RotatedRefreshTokenRecord
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid RefreshFamilyId { get; set; }
    public string RefreshTokenHash { get; set; } = string.Empty;
    public DateTimeOffset RotatedAt { get; set; }
}
