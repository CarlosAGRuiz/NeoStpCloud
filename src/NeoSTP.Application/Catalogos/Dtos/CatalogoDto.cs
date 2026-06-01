namespace NeoSTP.Application.Catalogos.Dtos;

public class CatalogoDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string? Descripcion { get; set; }
    public bool EsSistema { get; set; }
    public bool Activo { get; set; }
    public int? EmpresaId { get; set; }
    public int Version { get; set; }
    public string? MetadataJson { get; set; }
    public int TotalItems { get; set; }
}

public class CatalogoItemDto
{
    public int Id { get; set; }
    public int CatalogoId { get; set; }
    public string Codigo { get; set; } = null!;
    public string Valor { get; set; } = null!;
    public string? Descripcion { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; }
    public bool EsSistema { get; set; }
    public string? ParentCodigo { get; set; }
    public string? MetadataJson { get; set; }
}

public class CreateCatalogoRequest
{
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string? Descripcion { get; set; }
    public int Version { get; set; } = 1;
    public string? MetadataJson { get; set; }
}

public class UpdateCatalogoRequest
{
    public string Nombre { get; set; } = null!;
    public string? Descripcion { get; set; }
    public bool? Activo { get; set; }
    public int? Version { get; set; }
    public string? MetadataJson { get; set; }
}

public class CreateCatalogoItemRequest
{
    public string Codigo { get; set; } = null!;
    public string Valor { get; set; } = null!;
    public string? Descripcion { get; set; }
    public int Orden { get; set; }
    public string? ParentCodigo { get; set; }
    public string? MetadataJson { get; set; }
}

public class UpdateCatalogoItemRequest
{
    public string Valor { get; set; } = null!;
    public string? Descripcion { get; set; }
    public int? Orden { get; set; }
    public bool? Activo { get; set; }
    public string? ParentCodigo { get; set; }
    public string? MetadataJson { get; set; }
}

// ----- Import / Export -----

public enum CatalogoFileFormat
{
    Csv = 1,
    Json = 2,
    Xlsx = 3,
}

public enum CatalogoImportMode
{
    /// <summary>Insert + update por código (default).</summary>
    Upsert = 1,
    /// <summary>Solo inserta filas nuevas; las existentes se cuentan como Skipped.</summary>
    InsertOnly = 2,
}

/// <summary>Fila cruda de un archivo de importación, después del parseo.</summary>
public class CatalogoItemRow
{
    public int RowNumber { get; set; }
    public string? Codigo { get; set; }
    public string? Valor { get; set; }
    public string? Descripcion { get; set; }
    public int? Orden { get; set; }
    public string? ParentCodigo { get; set; }
    public string? MetadataJson { get; set; }
    public bool? Activo { get; set; }
}

public class CatalogoImportRequest
{
    public CatalogoFileFormat Format { get; set; } = CatalogoFileFormat.Csv;
    public Stream Content { get; set; } = Stream.Null;
    public bool DryRun { get; set; }
    public CatalogoImportMode Mode { get; set; } = CatalogoImportMode.Upsert;
}

public class CatalogoImportError
{
    public int Row { get; set; }
    public string? Codigo { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class CatalogoImportResult
{
    public bool DryRun { get; set; }
    public int Total { get; set; }
    public int Inserted { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int ErrorCount => Errors.Count;
    public List<CatalogoImportError> Errors { get; set; } = new();
}

public class CatalogoExportFile
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public byte[] Content { get; set; } = Array.Empty<byte>();
}
