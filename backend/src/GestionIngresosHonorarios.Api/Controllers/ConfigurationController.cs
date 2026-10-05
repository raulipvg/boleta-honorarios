using GestionIngresosHonorarios.Application.Authorization;
using GestionIngresosHonorarios.Application.Contracts;
using GestionIngresosHonorarios.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionIngresosHonorarios.Api.Controllers;

[ApiController]
[Route("api/configuration")]
[Authorize(Policy = PermissionCatalog.RetentionRead)]
public sealed class ConfigurationController(IIncomeApplicationService income) : ControllerBase
{
    [HttpGet("retention-rates")]
    public async Task<ActionResult<IReadOnlyList<RetentionRateDto>>> RetentionRates(CancellationToken cancellationToken) =>
        Ok(await income.GetRetentionRatesAsync(cancellationToken));
}
