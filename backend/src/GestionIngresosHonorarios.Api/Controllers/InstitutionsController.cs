using GestionIngresosHonorarios.Application.Authorization;
using GestionIngresosHonorarios.Application.Common;
using GestionIngresosHonorarios.Application.Contracts;
using GestionIngresosHonorarios.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace GestionIngresosHonorarios.Api.Controllers;

[ApiController]
[Route("api/institutions")]
[Authorize]
public sealed class InstitutionsController(
    IIncomeApplicationService income,
    IActorContextProvider actors,
    ILogger<InstitutionsController> logger) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PermissionCatalog.InstitutionsRead)]
    public async Task<ActionResult<IReadOnlyList<InstitutionDto>>> List(CancellationToken cancellationToken) =>
        Ok(await income.GetInstitutionsAsync(await CurrentActor(cancellationToken), cancellationToken));

    [HttpPost]
    [Authorize(Policy = PermissionCatalog.InstitutionsManage)]
    public async Task<ActionResult<InstitutionDto>> Create([FromBody] InstitutionPayload payload, CancellationToken cancellationToken)
    {
        var institution = await income.CreateInstitutionAsync(payload.Name, cancellationToken);
        logger.LogInformation("Administrador {ActorId} creó la institución pública {InstitutionId}", User.GetSubjectId(), institution.Id);
        return Created($"/api/institutions/{institution.Id}", institution);
    }

    [HttpPut("{institutionId:guid}")]
    [Authorize(Policy = PermissionCatalog.InstitutionsManage)]
    public async Task<ActionResult<InstitutionDto>> Update(Guid institutionId, [FromBody] UpdateInstitutionPayload payload, CancellationToken cancellationToken)
    {
        var result = await income.UpdateInstitutionAsync(institutionId, payload.Name, payload.Active, cancellationToken);
        logger.LogInformation("Administrador {ActorId} actualizó estado/datos de institución {InstitutionId}", User.GetSubjectId(), institutionId);
        return Ok(result);
    }

    private Task<ActorContext> CurrentActor(CancellationToken cancellationToken) =>
        actors.GetAsync(User.GetSubjectId(), cancellationToken);
}

public sealed record InstitutionPayload([param: Required, StringLength(200, MinimumLength = 1)] string Name);
public sealed record UpdateInstitutionPayload([param: Required, StringLength(200, MinimumLength = 1)] string Name, bool Active);
