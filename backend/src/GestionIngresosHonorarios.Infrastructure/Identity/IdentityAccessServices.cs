using GestionIngresosHonorarios.Application.Authorization;
using GestionIngresosHonorarios.Application.Common;
using GestionIngresosHonorarios.Application.Contracts;
using GestionIngresosHonorarios.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GestionIngresosHonorarios.Infrastructure.Identity;

public sealed class IdentityAccessServices(UserManager<ApplicationUser> users, AppDbContext db)
    : IActorContextProvider, IPermissionResolver
{
    public async Task<ActorContext> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null || !user.Active) throw new UnauthorizedAccessException();
        var roles = await GetRoleCodesAsync(userId, cancellationToken);
        var professionalId = await db.Professionals.AsNoTracking()
            .Where(x => x.UserId == userId).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(cancellationToken);
        return new ActorContext(userId, professionalId, roles.Contains("ADMINISTRADOR", StringComparer.Ordinal),
            roles.Contains("PROFESIONAL", StringComparer.Ordinal));
    }

    public async Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null || !user.Active) return false;
        var roles = await GetRoleCodesAsync(userId, cancellationToken);
        return PermissionCatalog.HasPermission(roles, permission);
    }

    public async Task<string[]> GetPermissionCodesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var roleCodes = await GetRoleCodesAsync(userId, cancellationToken);
        return PermissionCatalog.ForRoles(roleCodes);
    }

    public async Task<string[]> GetRoleCodesAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await (from userRole in db.UserRoles.AsNoTracking()
                      join role in db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                      where userRole.UserId == userId
                      select role.Code).Distinct().OrderBy(x => x).ToArrayAsync(cancellationToken);
    }
}
