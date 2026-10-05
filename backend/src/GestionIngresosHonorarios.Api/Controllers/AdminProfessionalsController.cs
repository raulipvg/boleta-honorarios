using GestionIngresosHonorarios.Application.Authorization;
using GestionIngresosHonorarios.Application.Contracts;
using GestionIngresosHonorarios.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionIngresosHonorarios.Api.Controllers;

[ApiController]
[Route("api/admin/professionals")]
[Authorize(Policy = PermissionCatalog.UsersRead)]
public sealed class AdminProfessionalsController(IIncomeApplicationService income, ILogger<AdminProfessionalsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProfessionalSummaryDto>>> List(CancellationToken cancellationToken)
    {
        var professionals = await income.GetProfessionalsAsync(cancellationToken);
        logger.LogInformation("Administrador {ActorId} consultó el directorio profesional ({ProfessionalCount} profesionales)",
            User.GetSubjectId(), professionals.Count);
        return Ok(professionals);
    }
}
