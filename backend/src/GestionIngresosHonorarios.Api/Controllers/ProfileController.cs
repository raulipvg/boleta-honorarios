using GestionIngresosHonorarios.Application.Authorization;
using GestionIngresosHonorarios.Application.Contracts;
using GestionIngresosHonorarios.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace GestionIngresosHonorarios.Api.Controllers;

[ApiController]
[Route("api/profile")]
[Authorize]
public sealed class ProfileController(IIncomeApplicationService income, IActorContextProvider actors) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PermissionCatalog.ProfileRead)]
    public async Task<ActionResult<ProfessionalProfileDto>> Get(CancellationToken cancellationToken) =>
        Ok(await income.GetProfileAsync(await actors.GetAsync(User.GetSubjectId(), cancellationToken), cancellationToken));

    [HttpPut]
    [Authorize(Policy = PermissionCatalog.ProfileUpdate)]
    public async Task<ActionResult<ProfessionalProfileDto>> Update([FromBody] UpdateProfilePayload payload, CancellationToken cancellationToken) =>
        Ok(await income.UpdateProfileAsync(await actors.GetAsync(User.GetSubjectId(), cancellationToken), payload.Name, cancellationToken));
}

public sealed record UpdateProfilePayload([param: Required, StringLength(200, MinimumLength = 1)] string Name);
