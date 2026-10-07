using GestionIngresosHonorarios.Application.Common;
using GestionIngresosHonorarios.Application.DTOs;

namespace GestionIngresosHonorarios.Application.Contracts;

public interface IPrivateLiquidationApplicationService
{
    Task<PrivateLiquidationPreviewDto> PreviewAsync(
        ActorContext actor, Stream pdf, long fileSizeBytes, string fileName, int minutesPerAttention,
        CancellationToken cancellationToken);
    Task<PrivateLiquidationDto> ImportAsync(
        ActorContext actor, Stream pdf, long fileSizeBytes, string fileName, int minutesPerAttention,
        string expectedSha256, decimal expectedRetentionPercentage, CancellationToken cancellationToken);
    Task<IReadOnlyList<PrivateLiquidationDto>> ListAsync(
        ActorContext actor, Guid? professionalId, short? year, short? month, CancellationToken cancellationToken);
    Task<PrivateLiquidationFile> DownloadAsync(ActorContext actor, Guid liquidationId, CancellationToken cancellationToken);
    Task DeleteAsync(ActorContext actor, Guid liquidationId, CancellationToken cancellationToken);
}
