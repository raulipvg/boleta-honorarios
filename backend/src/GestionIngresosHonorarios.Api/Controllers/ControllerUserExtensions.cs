using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace GestionIngresosHonorarios.Api.Controllers;

internal static class ControllerUserExtensions
{
    public static Guid GetSubjectId(this ClaimsPrincipal principal)
    {
        var subject = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (!Guid.TryParse(subject, out var id)) throw new UnauthorizedAccessException();
        return id;
    }
}
