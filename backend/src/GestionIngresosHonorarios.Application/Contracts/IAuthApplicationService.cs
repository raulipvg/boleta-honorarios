using GestionIngresosHonorarios.Application.DTOs;

namespace GestionIngresosHonorarios.Application.Contracts;

public interface IAuthApplicationService
{
    Task<AuthSessionResult?> LoginAsync(string userName, string password, CancellationToken cancellationToken);
    Task<AuthSessionResult?> RefreshAsync(string? refreshToken, CancellationToken cancellationToken);
    Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken);
    Task LogoutAllAsync(Guid userId, CancellationToken cancellationToken);
    Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken);
    Task<AuthIdentityDto> GetIdentityAsync(Guid userId, CancellationToken cancellationToken);
}

public interface IAccountAdministrationService
{
    Task<IReadOnlyList<AccountSummaryDto>> ListAsync(CancellationToken cancellationToken);
    Task<AccountSummaryDto> GetAsync(Guid userId, CancellationToken cancellationToken);
    Task<AccountSummaryDto> CreateAsync(CreateAccountRequestDto request, CancellationToken cancellationToken);
    Task ResetPasswordAsync(Guid userId, string temporaryPassword, CancellationToken cancellationToken);
}

public interface IActorContextProvider
{
    Task<GestionIngresosHonorarios.Application.Common.ActorContext> GetAsync(Guid userId, CancellationToken cancellationToken);
}

public interface IPermissionResolver
{
    Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken);
    Task<string[]> GetPermissionCodesAsync(Guid userId, CancellationToken cancellationToken);
}

public interface IAuthSessionValidator
{
    Task<bool> IsActiveAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken);
}
