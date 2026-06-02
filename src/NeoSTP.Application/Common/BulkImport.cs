namespace NeoSTP.Application.Common;

/// <summary>Formato de archivo para carga masiva.</summary>
public enum BulkFileFormat
{
    Csv = 1,
    Xlsx = 2,
}

/// <summary>Petición de carga masiva (clientes, productos, …).</summary>
public sealed class BulkImportRequest
{
    public BulkFileFormat Format { get; set; } = BulkFileFormat.Xlsx;
    public Stream Content { get; set; } = Stream.Null;
    /// <summary>Si true, valida y reporta sin persistir.</summary>
    public bool DryRun { get; set; }
}

public sealed class BulkImportError
{
    public int Row { get; set; }
    public string? Key { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>Resultado de una carga masiva con desglose y errores por fila.</summary>
public sealed class BulkImportResult
{
    public bool DryRun { get; set; }
    public int Total { get; set; }
    public int Inserted { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int ErrorCount => Errors.Count;
    public List<BulkImportError> Errors { get; set; } = new();
}
