using GestionIngresosHonorarios.Application.Authorization;
using GestionIngresosHonorarios.Application.Contracts;
using GestionIngresosHonorarios.Application.DTOs;
using AppError = GestionIngresosHonorarios.Application.Common.ApplicationException;
using GestionIngresosHonorarios.Infrastructure.Data;
using GestionIngresosHonorarios.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.Data;
using System.Security.Cryptography;
using System.Text;

namespace GestionIngresosHonorarios.Infrastructure.Security;

public sealed class AuthApplicationService(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    IdentityAccessServices access,
    AppDbContext db,
    JwtTokenIssuer tokenIssuer,
    ILogger<AuthApplicationService> logger) : IAuthApplicationService
{
    private const int IdleHours = 8;
    private const int AbsoluteDays = 7;
    private readonly PasswordHasher<ApplicationUser> _dummyHasher = new();
    private readonly ApplicationUser _dummyUser = new() { Id = Guid.NewGuid() };
    private readonly string _dummyPasswordHash = new PasswordHasher<ApplicationUser>()
        .HashPassword(new ApplicationUser { Id = Guid.NewGuid() }, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

    public async Task<AuthSessionResult?> LoginAsync(string userName, string password, CancellationToken cancellationToken)
    {
        var user = await users.FindByNameAsync(userName);
        if (user is null)
        {
            _ = _dummyHasher.VerifyHashedPassword(_dummyUser, _dummyPasswordHash, password);
            logger.LogWarning("Falló un intento de inicio de sesión para una cuenta no disponible");
            return null;
        }

        if (!user.Active)
        {
            _ = _dummyHasher.VerifyHashedPassword(_dummyUser, _dummyPasswordHash, password);
            logger.LogWarning("Falló un intento de inicio de sesión para una cuenta deshabilitada {UserId}", user.Id);
            return null;
        }

        Microsoft.AspNetCore.Identity.SignInResult signInResult;
        try
        {
            signInResult = await signIn.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        }
        catch (FormatException)
        {
            // A corrupt/legacy verifier must not leak an account-specific server error.
            await users.AccessFailedAsync(user);
            logger.LogWarning("Falló un intento de inicio de sesión por un verificador inválido para {UserId}", user.Id);
            return null;
        }
        if (!signInResult.Succeeded)
        {
            logger.LogWarning("Falló un intento de inicio de sesión para {UserId}; lockedOut={LockedOut}", user.Id, signInResult.IsLockedOut);
            return null;
        }

        if (user.MustChangePassword)
        {
            logger.LogInformation("La cuenta {UserId} inició sesión y debe cambiar su contraseña temporal", user.Id);
            return new AuthSessionResult(tokenIssuer.CreateAccessToken(user, Guid.Empty, requiresPasswordChange: true), null, null);
        }

        return await CreateSessionAsync(user, cancellationToken);
    }

    public async Task<AuthSessionResult?> RefreshAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken) || refreshToken.Length > 512) return null;
        var now = DateTimeOffset.UtcNow;
        var oldHash = HashToken(refreshToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await LockSessionByRefreshHashAsync(oldHash, cancellationToken);

        var session = await db.AuthSessions.SingleOrDefaultAsync(x => x.RefreshTokenHash == oldHash, cancellationToken);
        if (session is null)
        {
            var rotated = await db.RotatedRefreshTokens.AsNoTracking()
                .SingleOrDefaultAsync(x => x.RefreshTokenHash == oldHash, cancellationToken);
            if (rotated is not null)
            {
                await RevokeFamilyAsync(rotated.RefreshFamilyId, now, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                logger.LogWarning("Se detectó reutilización de refresh token para familia {RefreshFamilyId}", rotated.RefreshFamilyId);
            }
            else
            {
                await transaction.CommitAsync(cancellationToken);
            }
            return null;
        }

        if (session.RevokedAt.HasValue || session.IdleExpiresAt <= now || session.AbsoluteExpiresAt <= now)
        {
            session.RevokedAt ??= now;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var user = await users.FindByIdAsync(session.UserId.ToString());
        if (user is null || !user.Active || user.MustChangePassword)
        {
            session.RevokedAt = now;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var replacement = GenerateRefreshToken();
        var replacementHash = HashToken(replacement);
        db.RotatedRefreshTokens.Add(new RotatedRefreshTokenRecord
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            RefreshFamilyId = session.RefreshFamilyId,
            RefreshTokenHash = oldHash,
            RotatedAt = now
        });
        session.RefreshTokenHash = replacementHash;
        session.LastUsedAt = now;
        session.IdleExpiresAt = Min(now.AddHours(IdleHours), session.AbsoluteExpiresAt);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Se rotó el refresh de la sesión {SessionId}", session.Sid);

        return new AuthSessionResult(
            tokenIssuer.CreateAccessToken(user, session.Sid), replacement, session.AbsoluteExpiresAt);
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken) || refreshToken.Length > 512) return;
        var hash = HashToken(refreshToken);
        var now = DateTimeOffset.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await LockSessionByRefreshHashAsync(hash, cancellationToken);
        var current = await db.AuthSessions.SingleOrDefaultAsync(x => x.RefreshTokenHash == hash, cancellationToken);
        if (current is not null)
        {
            current.RevokedAt ??= now;
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Se cerró la sesión {SessionId} del usuario {UserId}", current.Sid, current.UserId);
        }
        else
        {
            var rotated = await db.RotatedRefreshTokens.AsNoTracking()
                .SingleOrDefaultAsync(x => x.RefreshTokenHash == hash, cancellationToken);
            if (rotated is not null) await RevokeFamilyAsync(rotated.RefreshFamilyId, now, cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task LogoutAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var revokedCount = await RevokeUserSessionsAsync(userId, now, cancellationToken);
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is not null)
        {
            var result = await users.UpdateSecurityStampAsync(user);
            if (!result.Succeeded) throw AppError.Conflict("No se pudieron invalidar las sesiones de la cuenta.");
        }
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Se cerraron {RevokedSessionCount} sesiones para el usuario {UserId}", revokedCount, userId);
    }

    public async Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString()) ?? throw AppError.NotFound();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var result = await users.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
        {
            if (result.Errors.Any(x => x.Code == "PasswordMismatch"))
                throw AppError.BadRequest("La contraseña actual no es válida.");
            throw AppError.BadRequest("La nueva contraseña no cumple la política de seguridad.");
        }

        user.MustChangePassword = false;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        var updateResult = await users.UpdateAsync(user);
        if (!updateResult.Succeeded) throw AppError.Conflict("No se pudo guardar el cambio de contraseña.");
        await RevokeUserSessionsAsync(userId, DateTimeOffset.UtcNow, cancellationToken);
        var stampResult = await users.UpdateSecurityStampAsync(user);
        if (!stampResult.Succeeded) throw AppError.Conflict("No se pudieron invalidar las demás sesiones.");
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("El usuario {UserId} actualizó su contraseña y sus sesiones fueron revocadas", userId);
    }

    public async Task<AuthIdentityDto> GetIdentityAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString())
            ?? throw AppError.NotFound();
        if (!user.Active) throw new UnauthorizedAccessException();
        var roleCodes = await access.GetRoleCodesAsync(userId, cancellationToken);
        var permissions = PermissionCatalog.ForRoles(roleCodes);
        return new AuthIdentityDto(user.Id, user.UserName ?? string.Empty, roleCodes, permissions, user.MustChangePassword);
    }

    private async Task<AuthSessionResult> CreateSessionAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var refreshToken = GenerateRefreshToken();
        var absoluteExpiry = now.AddDays(AbsoluteDays);
        var session = new AuthSessionRecord
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Sid = Guid.NewGuid(),
            RefreshFamilyId = Guid.NewGuid(),
            RefreshTokenHash = HashToken(refreshToken),
            CreatedAt = now,
            LastUsedAt = now,
            IdleExpiresAt = now.AddHours(IdleHours),
            AbsoluteExpiresAt = absoluteExpiry
        };
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.AuthSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Se creó una sesión autenticada {SessionId} para {UserId}", session.Sid, user.Id);
        return new AuthSessionResult(tokenIssuer.CreateAccessToken(user, session.Sid), refreshToken, absoluteExpiry);
    }

    private async Task LockSessionByRefreshHashAsync(string hash, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id FROM sesiones_auth WHERE refresh_token_hash = @tokenHash FOR UPDATE";
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        var parameter = command.CreateParameter();
        parameter.ParameterName = "tokenHash";
        parameter.Value = hash;
        command.Parameters.Add(parameter);
        _ = await command.ExecuteScalarAsync(cancellationToken);
    }

    private async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await db.AuthSessions.Where(x => x.RefreshFamilyId == familyId && x.RevokedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.RevokedAt, now), cancellationToken);
    }

    private Task<int> RevokeUserSessionsAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        db.AuthSessions.Where(x => x.UserId == userId && x.RevokedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.RevokedAt, now), cancellationToken);

    private static string GenerateRefreshToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static DateTimeOffset Min(DateTimeOffset first, DateTimeOffset second) => first <= second ? first : second;
}
