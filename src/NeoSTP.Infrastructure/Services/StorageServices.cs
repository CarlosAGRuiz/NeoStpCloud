using Microsoft.Extensions.Options;
using NeoSTP.Application.Ops;

namespace NeoSTP.Infrastructure.Services;

/// <summary>
/// Almacenamiento local en disco (provider LOCAL). Escribe bajo
/// <see cref="BackupOptions.LocalPath"/>, resuelto relativo al directorio base si no es absoluto.
/// </summary>
public class LocalStorageService : IStorageService
{
    private readonly string _basePath;

    public LocalStorageService(IOptions<BackupOptions> options)
    {
        var path = options.Value.LocalPath;
        _basePath = Path.IsPathRooted(path)
            ? path
            : Path.Combine(AppContext.BaseDirectory, path);
    }

    public string Provider => "LOCAL";

    public async Task<StorageResult> GuardarAsync(string objectName, Stream content, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_basePath);
        var safeName = Path.GetFileName(objectName); // evita traversal
        var fullPath = Path.Combine(_basePath, safeName);

        await using (var file = File.Create(fullPath))
        {
            await content.CopyToAsync(file, ct);
        }

        var size = new FileInfo(fullPath).Length;
        return new StorageResult { Path = fullPath, SizeBytes = size };
    }
}

/// <summary>
/// Stub para proveedores externos (Azure Blob / S3). Mantiene el punto de extensión sin
/// arrastrar SDKs: requiere implementar con el SDK correspondiente y credenciales.
/// </summary>
public class ExternalStorageService : IStorageService
{
    public ExternalStorageService(IOptions<BackupOptions> options)
    {
        Provider = options.Value.StorageProvider;
    }

    public string Provider { get; }

    public Task<StorageResult> GuardarAsync(string objectName, Stream content, CancellationToken ct = default)
        => throw new NotSupportedException(
            $"El proveedor de storage '{Provider}' no está configurado. " +
            "Implemente la integración con el SDK (Azure.Storage.Blobs / AWSSDK.S3) y credenciales.");
}
