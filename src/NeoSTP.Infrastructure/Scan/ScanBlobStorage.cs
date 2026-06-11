namespace NeoSTP.Infrastructure.Scan;

/// <summary>
/// V2.5-S4 — storage externo para los archivos capturados por NeoScan. Cuando está
/// registrado (Scan:Storage:Provider=FileSystem) los bytes salen de la BD: la fila
/// guarda solo la clave devuelta por <see cref="GuardarAsync"/>.
/// </summary>
public interface IScanBlobStorage
{
    Task<string> GuardarAsync(int empresaId, string? nombreOriginal, byte[] contenido, CancellationToken ct = default);
    Task<byte[]?> LeerAsync(string path, CancellationToken ct = default);
}

/// <summary>
/// Implementación sobre un directorio (disco local o ruta UNC compartida entre instancias).
/// Estructura: {root}/{empresaId}/{yyyyMM}/{guid}{ext} — la clave guardada es relativa al root,
/// así el root puede moverse por configuración sin tocar datos.
/// </summary>
public sealed class FileSystemScanBlobStorage : IScanBlobStorage
{
    private readonly string _root;

    public FileSystemScanBlobStorage(string root)
    {
        _root = Path.IsPathRooted(root) ? root : Path.Combine(AppContext.BaseDirectory, root);
    }

    public async Task<string> GuardarAsync(int empresaId, string? nombreOriginal, byte[] contenido, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(nombreOriginal ?? string.Empty);
        if (ext.Length is 0 or > 10) ext = ".bin";
        var relativo = Path.Combine(
            empresaId.ToString(),
            DateTime.UtcNow.ToString("yyyyMM"),
            $"{Guid.NewGuid():N}{ext}");

        var destino = Path.Combine(_root, relativo);
        Directory.CreateDirectory(Path.GetDirectoryName(destino)!);
        await File.WriteAllBytesAsync(destino, contenido, ct);
        // Clave portable entre Windows/Linux.
        return relativo.Replace('\\', '/');
    }

    public async Task<byte[]?> LeerAsync(string path, CancellationToken ct = default)
    {
        // Solo claves relativas generadas por GuardarAsync; nada de traversal.
        if (string.IsNullOrWhiteSpace(path) || path.Contains("..")) return null;
        var completo = Path.Combine(_root, path.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(completo) ? await File.ReadAllBytesAsync(completo, ct) : null;
    }
}
