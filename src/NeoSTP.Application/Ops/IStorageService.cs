namespace NeoSTP.Application.Ops;

/// <summary>Resultado de almacenar un objeto en el storage destino.</summary>
public sealed record StorageResult
{
    public string Path { get; init; } = null!;
    public long SizeBytes { get; init; }
}

/// <summary>
/// Almacenamiento de artefactos de respaldo. Toggle Local / Azure Blob / S3 vía
/// <c>Hardening:Backup:StorageProvider</c>. La implementación Local escribe en disco;
/// las externas requieren SDK y credenciales (extensión documentada).
/// </summary>
public interface IStorageService
{
    /// <summary>Código del proveedor (LOCAL | AZURE_BLOB | S3).</summary>
    string Provider { get; }

    /// <summary>Guarda el contenido bajo el nombre/clave indicado y devuelve ruta + tamaño.</summary>
    Task<StorageResult> GuardarAsync(string objectName, Stream content, CancellationToken ct = default);
}
