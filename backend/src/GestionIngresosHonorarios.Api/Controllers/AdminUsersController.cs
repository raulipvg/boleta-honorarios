using GestionIngresosHonorarios.Application.Authorization;
using GestionIngresosHonorarios.Application.Contracts;
using GestionIngresosHonorarios.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace GestionIngresosHonorarios.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize]
public sealed class AdminUsersController(IAccountAdministrationService accounts, ILogger<AdminUsersController> logger) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PermissionCatalog.UsersRead)]
    public async Task<ActionResult<IReadOnlyList<AccountSummaryDto>>> List(CancellationToken cancellationToken)
    {
        var accountsList = await accounts.ListAsync(cancellationToken);
        logger.LogInformation("Administrador {ActorId} consultó el catálogo de cuentas ({AccountCount} cuentas)", User.GetSubjectId(), accountsList.Count);
        return Ok(accountsList);
    }

    [HttpGet("{userId:guid}")]
    [Authorize(Policy = PermissionCatalog.UsersRead)]
    public async Task<ActionResult<AccountSummaryDto>> Get(Guid userId, CancellationToken cancellationToken)
    {
        var account = await accounts.GetAsync(userId, cancellationToken);
        logger.LogInformation("Administrador {ActorId} consultó la cuenta {TargetUserId}", User.GetSubjectId(), userId);
        return Ok(account);
    }

    [HttpPost]
    [Authorize(Policy = PermissionCatalog.UsersCreate)]
    public async Task<ActionResult<AccountSummaryDto>> Create([FromBody] CreateAccountPayload payload, CancellationToken cancellationToken)
    {
        var result = await accounts.CreateAsync(new CreateAccountRequestDto(
            payload.UserName, payload.TemporaryPassword, payload.RoleCodes, payload.ProfessionalName), cancellationToken);
        logger.LogInformation("Administrador {ActorId} creó la cuenta {TargetUserId} con roles {RoleCodes}",
            User.GetSubjectId(), result.Id, string.Join(',', result.RoleCodes));
        return Created($"/api/admin/users/{result.Id}", result);
    }

    [HttpPost("{userId:guid}/reset-password")]
    [Authorize(Policy = PermissionCatalog.UsersResetPassword)]
    public async Task<IActionResult> ResetPassword(Guid userId, [FromBody] ResetPasswordPayload payload, CancellationToken cancellationToken)
    {
        await accounts.ResetPasswordAsync(userId, payload.TemporaryPassword, cancellationToken);
        logger.LogInformation("Administrador {ActorId} restableció credenciales de la cuenta {TargetUserId}", User.GetSubjectId(), userId);
        return NoContent();
    }
}

public sealed record CreateAccountPayload(
    [param: Required, StringLength(256, MinimumLength = 1)] string UserName,
    [param: Required, StringLength(1024, MinimumLength = 15)] string TemporaryPassword,
    [param: Required, MinLength(1)] string[] RoleCodes,
    [param: StringLength(200)] string? ProfessionalName);

public sealed record ResetPasswordPayload(
    [param: Required, StringLength(1024, MinimumLength = 15)] string TemporaryPassword);
