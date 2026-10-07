using GestionIngresosHonorarios.Application.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace GestionIngresosHonorarios.Infrastructure.Storage;

public sealed class LocalPrivateLiquidationFileStorage : IPrivateLiquidationFileStorage
{
    private readonly string _rootPath;

    public LocalPrivateLiquidationFileStorage(IConfiguration configuration, IHostEnvironment environment)
    {
        var configuredPath = configuration["PrivateLiquidations:StoragePath"];
        _rootPath = Path.GetFullPath(string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(environment.ContentRootPath, "private-liquidations")
            : configuredPath);
        Directory.CreateDirectory(_rootPath);
    }

    public async Task StoreAsync(string storageKey, Stream content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        var path = ResolvePath(storageKey);
        if (File.Exists(path)) throw new IOException("Ya existe un archivo con esa clave de almacenamiento.");

        try
        {
            await using var destination = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                bufferSize: 64 * 1024, useAsync: true);
            await content.CopyToAsync(destination, cancellationToken);
            await destination.FlushAsync(cancellationToken);
        }
        catch
        {
            if (File.Exists(path)) File.Delete(path);
            throw;
        }
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stream stream = new FileStream(ResolvePath(storageKey), FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 64 * 1024, useAsync: true);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolvePath(storageKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string ResolvePath(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey) || Path.GetFileName(storageKey) != storageKey
            || !string.Equals(Path.GetExtension(storageKey), ".pdf", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("La clave del archivo PDF no es válida.", nameof(storageKey));

        var path = Path.GetFullPath(Path.Combine(_rootPath, storageKey));
        var rootPrefix = _rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? _rootPath
            : _rootPath + Path.DirectorySeparatorChar;
        if (!path.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("La clave del archivo PDF no es válida.", nameof(storageKey));
        return path;
    }
}
