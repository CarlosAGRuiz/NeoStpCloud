using NeoSTP.Domain.Common;

namespace NeoSTP.Domain.Core.Ops;

/// <summary>
/// Registro de un trabajo de respaldo (backup) de la base de datos.
/// Los backups de DR son a nivel de sistema (<see cref="EmpresaId"/> = null);
/// se permite además respaldos lógicos por empresa.
/// </summary>
public class BackupJob : AuditableEntity
{
    /// <summary>Empresa dueña del respaldo. Null = respaldo de sistema (DR completo).</summary>
    public int? EmpresaId { get; set; }

    /// <summary>FULL | DIFERENCIAL | LOGICO</summary>
    public string TipoBackup { get; set; } = BackupTipos.Full;

    /// <summary>PENDIENTE | EN_PROGRESO | COMPLETADO | FALLIDO</summary>
    public string EstadoCodigo { get; set; } = BackupEstados.Pendiente;

    /// <summary>MANUAL | PROGRAMADO</summary>
    public string Origen { get; set; } = BackupOrigen.Manual;

    /// <summary>LOCAL | AZURE_BLOB | S3</summary>
    public string StorageProvider { get; set; } = StorageProviders.Local;

    /// <summary>Ruta / clave del objeto en el storage destino.</summary>
    public string? StoragePath { get; set; }

    /// <summary>Tamaño del artefacto en bytes (cuando se conoce).</summary>
    public long? TamanoBytes { get; set; }

    /// <summary>Checksum SHA-256 del artefacto para verificar integridad.</summary>
    public string? Checksum { get; set; }

    public DateTime IniciadoAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinalizadoAt { get; set; }

    /// <summary>Mensaje de error si el respaldo falló.</summary>
    public string? Error { get; set; }
}

public static class BackupTipos
{
    public const string Full = "FULL";
    public const string Diferencial = "DIFERENCIAL";
    public const string Logico = "LOGICO";
}

public static class BackupEstados
{
    public const string Pendiente = "PENDIENTE";
    public const string EnProgreso = "EN_PROGRESO";
    public const string Completado = "COMPLETADO";
    public const string Fallido = "FALLIDO";
}

public static class BackupOrigen
{
    public const string Manual = "MANUAL";
    public const string Programado = "PROGRAMADO";
}

public static class StorageProviders
{
    public const string Local = "LOCAL";
    public const string AzureBlob = "AZURE_BLOB";
    public const string S3 = "S3";
}
