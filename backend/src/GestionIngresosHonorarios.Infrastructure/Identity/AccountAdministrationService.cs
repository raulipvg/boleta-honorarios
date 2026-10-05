using GestionIngresosHonorarios.Application.Authorization;
using GestionIngresosHonorarios.Application.Common;
using GestionIngresosHonorarios.Application.Contracts;
using GestionIngresosHonorarios.Application.DTOs;
using GestionIngresosHonorarios.Domain.Entities;
using GestionIngresosHonorarios.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AppError = GestionIngresosHonorarios.Application.Common.ApplicationException;

namespace GestionIngresosHonorarios.Infrastructure.Identity;

public sealed class AccountAdministrationService(
    UserManager<ApplicationUser> users,
    AppDbContext db,
    IdentityAccessServices access) : IAccountAdministrationService
{
    public async Task<IReadOnlyList<AccountSummaryDto>> ListAsync(CancellationToken cancellationToken)
    {
        var accounts = await users.Users.AsNoTracking().OrderBy(x => x.UserName).ToListAsync(cancellationToken);
        var professionals = await db.Professionals.AsNoTracking().ToDictionaryAsync(x => x.UserId, x => x.Name, cancellationToken);
        var result = new List<AccountSummaryDto>(accounts.Count);
        foreach (var user in accounts)
        {
            var roleCodes = await access.GetRoleCodesAsync(user.Id, cancellationToken);
            result.Add(new AccountSummaryDto(user.Id, user.UserName ?? string.Empty, user.Active,
                user.MustChangePassword, roleCodes, professionals.GetValueOrDefault(user.Id)));
        }
        return result;
    }

    public async Task<AccountSummaryDto> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString()) ?? throw AppError.NotFound();
        var roleCodes = await access.GetRoleCodesAsync(user.Id, cancellationToken);
        var professionalName = await db.Professionals.AsNoTracking()
            .Where(x => x.UserId == user.Id).Select(x => x.Name).SingleOrDefaultAsync(cancellationToken);
        return new AccountSummaryDto(user.Id, user.UserName ?? string.Empty, user.Active,
            user.MustChangePassword, roleCodes, professionalName);
    }

    public async Task<AccountSummaryDto> CreateAsync(CreateAccountRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || request.UserName.Trim().Length > 256)
            throw AppError.BadRequest("El nombre de usuario debe contener entre 1 y 256 caracteres.");
        if (string.IsNullOrWhiteSpace(request.TemporaryPassword))
            throw AppError.BadRequest("Debe asignar una contraseña temporal.");
        var requestedRoles = request.RoleCodes
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (requestedRoles.Length == 0 || requestedRoles.Any(x => !PermissionCatalog.IsKnownRole(x)))
            throw AppError.BadRequest("Debe asignar uno o más roles válidos.");
        var isProfessional = requestedRoles.Contains("PROFESIONAL", StringComparer.Ordinal);
        if (isProfessional && string.IsNullOrWhiteSpace(request.ProfessionalName))
            throw AppError.BadRequest("El perfil profesional requiere un nombre.");
        if (request.TemporaryPassword.Length < 15)
            throw AppError.BadRequest("La contraseña temporal debe tener al menos 15 caracteres.");

        var roles = await db.Roles.Where(x => requestedRoles.Contains(x.Code)).ToListAsync(cancellationToken);
        if (roles.Count != requestedRoles.Length) throw AppError.BadRequest("Uno o más roles no están configurados.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var user = new ApplicationUser
        {
            UserName = request.UserName.Trim(),
            Active = true,
            MustChangePassword = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var createResult = await users.CreateAsync(user, request.TemporaryPassword);
        if (!createResult.Succeeded)
        {
            if (createResult.Errors.Any(x => x.Code == "DuplicateUserName"))
                throw AppError.Conflict("El nombre de usuario ya está en uso.");
            throw AppError.BadRequest("No se pudo crear la cuenta. Verifique la política de contraseña.");
        }

        var roleNames = roles.Select(x => x.Name ?? x.Code).ToArray();
        var roleResult = await users.AddToRolesAsync(user, roleNames);
        if (!roleResult.Succeeded) throw AppError.BadRequest("No se pudieron asignar los roles solicitados.");

        if (isProfessional)
        {
            db.Professionals.Add(new Professional(user.Id, request.ProfessionalName!));
            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        var roleCodes = await access.GetRoleCodesAsync(user.Id, cancellationToken);
        return new AccountSummaryDto(user.Id, user.UserName!, user.Active, user.MustChangePassword,
            roleCodes, request.ProfessionalName);
    }

    public async Task ResetPasswordAsync(Guid userId, string temporaryPassword, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(temporaryPassword) || temporaryPassword.Length < 15)
            throw AppError.BadRequest("La contraseña temporal debe tener al menos 15 caracteres.");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var user = await users.FindByIdAsync(userId.ToString()) ?? throw AppError.NotFound();
        var resetToken = await users.GeneratePasswordResetTokenAsync(user);
        var resetResult = await users.ResetPasswordAsync(user, resetToken, temporaryPassword);
        if (!resetResult.Succeeded)
            throw AppError.BadRequest("No se pudo restablecer la contraseña. Verifique la política de contraseña.");
        user.MustChangePassword = true;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        var stampResult = await users.UpdateSecurityStampAsync(user);
        if (!stampResult.Succeeded) throw AppError.Conflict("No se pudo invalidar la sesión de la cuenta.");
        var updateResult = await users.UpdateAsync(user);
        if (!updateResult.Succeeded) throw AppError.Conflict("No se pudo guardar el estado de cambio de contraseña.");
        await db.AuthSessions.Where(x => x.UserId == userId && x.RevokedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.RevokedAt, DateTimeOffset.UtcNow), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
