using GestionIngresosHonorarios.Application.Authorization;
using GestionIngresosHonorarios.Application.Contracts;
using GestionIngresosHonorarios.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace GestionIngresosHonorarios.Api.Controllers;

[ApiController]
[Authorize(Policy = PermissionCatalog.HoursManage)]
public sealed class HourRecordsController(IIncomeApplicationService income, IActorContextProvider actors) : ControllerBase
{
    [HttpPost("api/periods/{year:int}/{month:int}/institutions/{professionalInstitutionId:guid}/hours")]
    public async Task<ActionResult<SavedHourRecordDto>> Add(
        short year, short month, Guid professionalInstitutionId, [FromBody] HourRecordPayload payload, CancellationToken cancellationToken)
    {
        var record = await income.AddHourRecordAsync(
            await actors.GetAsync(User.GetSubjectId(), cancellationToken), year, month, professionalInstitutionId, payload.Hours, cancellationToken);
        return Created($"/api/hours/{record.Record.Id}", record);
    }

    [HttpPut("api/hours/{recordId:guid}")]
    public async Task<ActionResult<SavedHourRecordDto>> Update(
        Guid recordId, [FromBody] UpdateHourRecordPayload payload, CancellationToken cancellationToken) =>
        Ok(await income.UpdateHourRecordAsync(
            await actors.GetAsync(User.GetSubjectId(), cancellationToken), recordId, payload.Hours, payload.Version, cancellationToken));

    [HttpDelete("api/hours/{recordId:guid}")]
    public async Task<ActionResult<MonthlyWorkspaceDto>> Delete(
        Guid recordId, [FromQuery] long version, CancellationToken cancellationToken)
    {
        return Ok(await income.DeleteHourRecordAsync(await actors.GetAsync(User.GetSubjectId(), cancellationToken), recordId, version, cancellationToken));
    }
}

public sealed record HourRecordPayload([param: Range(1, int.MaxValue)] int Hours);
public sealed record UpdateHourRecordPayload([param: Range(1, int.MaxValue)] int Hours, [param: Range(1, long.MaxValue)] long Version);
