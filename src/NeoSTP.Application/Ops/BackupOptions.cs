namespace NeoSTP.Application.Ops;

/// <summary>Opciones de respaldo (sección "Hardening:Backup").</summary>
public sealed class BackupOptions
{
    public const string SectionName = "Hardening:Backup";

    /// <summary>LOCAL | AZURE_BLOB | S3. Default LOCAL.</summary>
    public string StorageProvider { get; set; } = "LOCAL";

    /// <summary>Carpeta destino para el provider LOCAL.</summary>
    public string LocalPath { get; set; } = "backups";

    /// <summary>Activa el BackupWorker periódico. Default false (se ejecuta bajo demanda).</summary>
    public bool WorkerEnabled { get; set; }

    /// <summary>Intervalo del BackupWorker en horas. Default 24.</summary>
    public int IntervaloHoras { get; set; } = 24;
}
