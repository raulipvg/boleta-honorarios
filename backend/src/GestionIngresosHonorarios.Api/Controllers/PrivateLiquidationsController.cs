using GestionIngresosHonorarios.Application.Authorization;
using GestionIngresosHonorarios.Application.Contracts;
using GestionIngresosHonorarios.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppError = GestionIngresosHonorarios.Application.Common.ApplicationException;

namespace GestionIngresosHonorarios.Api.Controllers;

[ApiController]
[Route("api/private-liquidations")]
[Authorize]
public sealed class PrivateLiquidationsController(
    IPrivateLiquidationApplicationService liquidations,
    IActorContextProvider actors) : ControllerBase
{
    private const long MaxPdfBytes = 1_048_576;

    [HttpPost("preview")]
    [Authorize(Policy = PermissionCatalog.PrivateLiquidationsCreate)]
    [RequestSizeLimit(1_100_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 1_100_000)]
    public async Task<ActionResult<PrivateLiquidationPreviewDto>> Preview(
        [FromForm] IFormFile? file,
        [FromForm] int minutesPerAttention,
        CancellationToken cancellationToken)
    {
        if (file is null) throw AppError.BadRequest("Selecciona un PDF para analizar.");
        if (file.Length is < 1 or > MaxPdfBytes) throw AppError.BadRequest("El PDF no puede superar 1 MB.");

        await using var stream = file.OpenReadStream();
        var preview = await liquidations.PreviewAsync(
            await actors.GetAsync(User.GetSubjectId(), cancellationToken),
            stream, file.Length, file.FileName, minutesPerAttention, cancellationToken);
        return Ok(preview);
    }

    [HttpPost]
    [Authorize(Policy = PermissionCatalog.PrivateLiquidationsCreate)]
    [RequestSizeLimit(1_100_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 1_100_000)]
    public async Task<ActionResult<PrivateLiquidationDto>> Import(
        [FromForm] IFormFile? file,
        [FromForm] int minutesPerAttention,
        [FromForm] string expectedSha256,
        [FromForm] decimal expectedRetentionPercentage,
        CancellationToken cancellationToken)
    {
        if (file is null) throw AppError.BadRequest("Selecciona un PDF para importar.");
        if (file.Length is < 1 or > MaxPdfBytes) throw AppError.BadRequest("El PDF no puede superar 1 MB.");

        await using var stream = file.OpenReadStream();
        var result = await liquidations.ImportAsync(
            await actors.GetAsync(User.GetSubjectId(), cancellationToken),
            stream, file.Length, file.FileName, minutesPerAttention, expectedSha256, expectedRetentionPercentage, cancellationToken);
        return CreatedAtAction(nameof(List), new { year = result.AccountingYear, month = result.AccountingMonth }, result);
    }

    [HttpGet]
    [Authorize(Policy = PermissionCatalog.PrivateLiquidationsRead)]
    public async Task<ActionResult<IReadOnlyList<PrivateLiquidationDto>>> List(
        [FromQuery] Guid? professionalId,
        [FromQuery] short? year,
        [FromQuery] short? month,
        CancellationToken cancellationToken) =>
        Ok(await liquidations.ListAsync(
            await actors.GetAsync(User.GetSubjectId(), cancellationToken), professionalId, year, month, cancellationToken));

    [HttpGet("{liquidationId:guid}/file")]
    [Authorize(Policy = PermissionCatalog.PrivateLiquidationsRead)]
    public async Task<IActionResult> Download(Guid liquidationId, CancellationToken cancellationToken)
    {
        var file = await liquidations.DownloadAsync(
            await actors.GetAsync(User.GetSubjectId(), cancellationToken), liquidationId, cancellationToken);
        return File(file.Content, "application/pdf", file.FileName, enableRangeProcessing: false);
    }

    [HttpDelete("{liquidationId:guid}")]
    [Authorize(Policy = PermissionCatalog.PrivateLiquidationsDelete)]
    public async Task<IActionResult> Delete(Guid liquidationId, CancellationToken cancellationToken)
    {
        await liquidations.DeleteAsync(await actors.GetAsync(User.GetSubjectId(), cancellationToken), liquidationId, cancellationToken);
        return NoContent();
    }
}
