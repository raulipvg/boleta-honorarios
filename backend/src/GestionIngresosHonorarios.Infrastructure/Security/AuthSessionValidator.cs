using GestionIngresosHonorarios.Application.Contracts;
using GestionIngresosHonorarios.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GestionIngresosHonorarios.Infrastructure.Security;

public sealed class AuthSessionValidator(AppDbContext db) : IAuthSessionValidator
{
    public Task<bool> IsActiveAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        return db.AuthSessions.AsNoTracking().AnyAsync(x => x.UserId == userId && x.Sid == sessionId
            && x.RevokedAt == null && x.IdleExpiresAt > now && x.AbsoluteExpiresAt > now, cancellationToken);
    }
}
