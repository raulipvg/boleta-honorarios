using GestionIngresosHonorarios.Application.Authorization;
using GestionIngresosHonorarios.Application.Contracts;
using GestionIngresosHonorarios.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionIngresosHonorarios.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize(Policy = PermissionCatalog.DashboardRead)]
public sealed class DashboardController(
    IIncomeApplicationService income,
    IActorContextProvider actors,
    ILogger<DashboardController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DashboardDto>> Get(
        [FromQuery] short fromYear,
        [FromQuery] short toYear,
        [FromQuery] Guid? professionalId,
        [FromQuery] string[]? institutionKeys,
        CancellationToken cancellationToken)
    {
        var actor = await actors.GetAsync(User.GetSubjectId(), cancellationToken);
        var dashboard = await income.GetDashboardAsync(actor, professionalId, fromYear, toYear, institutionKeys, cancellationToken);
        if (actor.IsAdministrator)
            logger.LogInformation("Administrador {ActorId} consultó Dashboard del profesional {TargetProfessionalId} entre {FromYear} y {ToYear}",
                actor.UserId, dashboard.ProfessionalId, fromYear, toYear);
        return Ok(dashboard);
    }
}
