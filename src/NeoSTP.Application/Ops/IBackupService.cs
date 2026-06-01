using NeoSTP.Application.Common;

namespace NeoSTP.Application.Ops;

public sealed record BackupJobDto
{
    public int Id { get; init; }
    public int? EmpresaId { get; init; }
    public string TipoBackup { get; init; } = null!;
    public string EstadoCodigo { get; init; } = null!;
    public string Origen { get; init; } = null!;
    public string StorageProvider { get; init; } = null!;
    public string? StoragePath { get; init; }
    public long? TamanoBytes { get; init; }
    public string? Checksum { get; init; }
    public DateTime IniciadoAt { get; init; }
    public DateTime? FinalizadoAt { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// Genera respaldos lógicos (manifiesto/snapshot) y los persiste en <c>Ops_BackupJobs</c>,
/// subiéndolos al storage configurado con checksum SHA-256 para verificar integridad.
/// </summary>
public interface IBackupService
{
    Task<Result<BackupJobDto>> EjecutarBackupAsync(int? empresaId, string origen, string? actor, CancellationToken ct = default);
    Task<IReadOnlyList<BackupJobDto>> ListarAsync(int max = 50, CancellationToken ct = default);
}
