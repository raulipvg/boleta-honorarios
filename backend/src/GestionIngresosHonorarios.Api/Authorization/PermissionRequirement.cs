using GestionIngresosHonorarios.Application.Contracts;
using Microsoft.AspNetCore.Authorization;

namespace GestionIngresosHonorarios.Api.Authorization;

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;

public sealed class PermissionAuthorizationHandler(IPermissionResolver permissions)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var subject = context.User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(subject, out var userId)) return;
        if (await permissions.HasPermissionAsync(userId, requirement.Permission, CancellationToken.None))
            context.Succeed(requirement);
    }
}
