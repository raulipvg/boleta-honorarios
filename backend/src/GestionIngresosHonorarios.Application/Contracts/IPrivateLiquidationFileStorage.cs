namespace GestionIngresosHonorarios.Application.Contracts;

public interface IPrivateLiquidationFileStorage
{
    Task StoreAsync(string storageKey, Stream content, CancellationToken cancellationToken);
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken);
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
}
