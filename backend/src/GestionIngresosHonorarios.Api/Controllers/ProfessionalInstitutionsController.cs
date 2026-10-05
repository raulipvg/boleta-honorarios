using GestionIngresosHonorarios.Application.Authorization;
using GestionIngresosHonorarios.Application.Contracts;
using GestionIngresosHonorarios.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace GestionIngresosHonorarios.Api.Controllers;

[ApiController]
[Route("api/professional-institutions")]
[Authorize]
public sealed class ProfessionalInstitutionsController(
    IIncomeApplicationService income,
    IActorContextProvider actors,
    ILogger<ProfessionalInstitutionsController> logger) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PermissionCatalog.RelationshipsRead)]
    public async Task<ActionResult<IReadOnlyList<ProfessionalInstitutionDto>>> List([FromQuery] Guid? professionalId, CancellationToken cancellationToken)
    {
        var actor = await actors.GetAsync(User.GetSubjectId(), cancellationToken);
        var result = await income.GetProfessionalInstitutionsAsync(actor, professionalId, cancellationToken);
        if (actor.IsAdministrator && professionalId.HasValue)
            logger.LogInformation("Administrador {ActorId} consultó relaciones del profesional {TargetProfessionalId}", actor.UserId, professionalId);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = PermissionCatalog.RelationshipsManage)]
    public async Task<ActionResult<ProfessionalInstitutionDto>> Add([FromBody] AddProfessionalInstitutionPayload payload, CancellationToken cancellationToken)
    {
        var relation = await income.AddProfessionalInstitutionAsync(
            await actors.GetAsync(User.GetSubjectId(), cancellationToken), payload.InstitutionId, cancellationToken);
        return Created($"/api/professional-institutions/{relation.Id}", relation);
    }

    [HttpDelete("{relationId:guid}")]
    [Authorize(Policy = PermissionCatalog.RelationshipsManage)]
    public async Task<IActionResult> Deactivate(Guid relationId, CancellationToken cancellationToken)
    {
        await income.RemoveProfessionalInstitutionAsync(await actors.GetAsync(User.GetSubjectId(), cancellationToken), relationId, cancellationToken);
        return NoContent();
    }

    [HttpGet("{relationId:guid}/rates")]
    [Authorize(Policy = PermissionCatalog.RatesRead)]
    public async Task<ActionResult<IReadOnlyList<HourlyRateDto>>> Rates(Guid relationId, [FromQuery] short? year, CancellationToken cancellationToken)
    {
        var actor = await actors.GetAsync(User.GetSubjectId(), cancellationToken);
        var result = await income.GetHourlyRatesAsync(actor, relationId, year, cancellationToken);
        if (actor.IsAdministrator)
            logger.LogInformation("Administrador {ActorId} consultó historial de tarifa para relación {ProfessionalInstitutionId}", actor.UserId, relationId);
        return Ok(result);
    }

    [HttpPost("{relationId:guid}/rates")]
    [Authorize(Policy = PermissionCatalog.RatesCreate)]
    public async Task<ActionResult<HourlyRateDto>> AddRate(Guid relationId, [FromBody] AddHourlyRatePayload payload, CancellationToken cancellationToken)
    {
        var rate = await income.AddHourlyRateVersionAsync(
            await actors.GetAsync(User.GetSubjectId(), cancellationToken), relationId, payload.Year, payload.HourlyRateClp, cancellationToken);
        return Created($"/api/professional-institutions/{relationId}/rates/{rate.Year}/{rate.Version}", rate);
    }
}

public sealed record AddProfessionalInstitutionPayload([param: Required] Guid InstitutionId);
public sealed record AddHourlyRatePayload([param: Range(1900, 32767)] short Year, [param: Range(0, long.MaxValue)] long HourlyRateClp);
