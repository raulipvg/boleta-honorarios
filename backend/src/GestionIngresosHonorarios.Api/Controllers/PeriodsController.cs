using GestionIngresosHonorarios.Application.Authorization;
using GestionIngresosHonorarios.Application.Contracts;
using GestionIngresosHonorarios.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace GestionIngresosHonorarios.Api.Controllers;

[ApiController]
[Route("api/periods")]
[Authorize]
public sealed class PeriodsController(
    IIncomeApplicationService income,
    IActorContextProvider actors,
    ILogger<PeriodsController> logger) : ControllerBase
{
    [HttpGet("{year:int}/{month:int}")]
    [Authorize(Policy = PermissionCatalog.PeriodsRead)]
    public async Task<ActionResult<MonthlyWorkspaceDto>> Get(
        short year, short month, [FromQuery] Guid? professionalId, CancellationToken cancellationToken)
    {
        var actor = await actors.GetAsync(User.GetSubjectId(), cancellationToken);
        var workspace = await income.GetMonthlyWorkspaceAsync(actor, professionalId, year, month, cancellationToken);
        if (actor.IsAdministrator)
            logger.LogInformation("Administrador {ActorId} consultó el período {Year}-{Month} del profesional {TargetProfessionalId}",
                actor.UserId, year, month, workspace.ProfessionalId);
        return Ok(workspace);
    }

    [HttpGet("{year:int}/{month:int}/institutions/{professionalInstitutionId:guid}/hours")]
    [Authorize(Policy = PermissionCatalog.PeriodsRead)]
    public async Task<ActionResult<GestionIngresosHonorarios.Application.DTOs.HourRecordsPageDto>> GetHourRecords(
        short year,
        short month,
        Guid professionalInstitutionId,
        [FromQuery] Guid? professionalId,
        [FromQuery] int? beforeOrder,
        [FromQuery] int pageSize = 100,
        CancellationToken cancellationToken = default) =>
        Ok(await income.GetHourRecordsPageAsync(await actors.GetAsync(User.GetSubjectId(), cancellationToken),
            professionalId, year, month, professionalInstitutionId, beforeOrder, pageSize, cancellationToken));

    [HttpPut("{year:int}/{month:int}/institutions")]
    [Authorize(Policy = PermissionCatalog.PeriodsManage)]
    public async Task<ActionResult<MonthlyWorkspaceDto>> AddInstitution(
        short year, short month, [FromBody] AddPeriodInstitutionPayload payload, CancellationToken cancellationToken) =>
        Ok(await income.AddInstitutionToPeriodAsync(
            await actors.GetAsync(User.GetSubjectId(), cancellationToken), year, month, payload.ProfessionalInstitutionId, cancellationToken));

    [HttpDelete("{year:int}/{month:int}/institutions/{professionalInstitutionId:guid}")]
    [Authorize(Policy = PermissionCatalog.PeriodsManage)]
    public async Task<ActionResult<MonthlyWorkspaceDto>> RemoveInstitution(
        short year, short month, Guid professionalInstitutionId, CancellationToken cancellationToken) =>
        Ok(await income.RemoveInstitutionFromPeriodAsync(
            await actors.GetAsync(User.GetSubjectId(), cancellationToken), year, month, professionalInstitutionId, cancellationToken));
}

public sealed record AddPeriodInstitutionPayload([param: Required] Guid ProfessionalInstitutionId);
