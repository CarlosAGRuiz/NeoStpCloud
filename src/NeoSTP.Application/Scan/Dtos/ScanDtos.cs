namespace NeoSTP.Application.Scan.Dtos;

/// <summary>Item de la bandeja de NeoScanAI (campos extraídos + estado).</summary>
public sealed class ScanDocumentoDto
{
    public int Id { get; set; }
    public string EstadoCodigo { get; set; } = string.Empty;
    public string? TipoClasificacion { get; set; }
    public string Origen { get; set; } = "MOBILE";
    public string? ArchivoNombre { get; set; }
    public string? ArchivoContentType { get; set; }
    public bool TieneArchivo { get; set; }

    public string? EmisorNombre { get; set; }
    public string? EmisorNit { get; set; }
    public string? EmisorNrc { get; set; }
    public DateOnly? Fecha { get; set; }
    public string? TipoDocumento { get; set; }
    public string? NumeroControl { get; set; }
    public string? SelloRecibido { get; set; }
    public decimal? Subtotal { get; set; }
    public decimal? Iva { get; set; }
    public decimal? Total { get; set; }
    public decimal Confianza { get; set; }
    public string? Notas { get; set; }

    public int? ProfitGastoId { get; set; }
    public int? ProfitCompraId { get; set; }
    public int? DteRecibidoId { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Subida de un documento capturado (imagen/PDF en base64).</summary>
public sealed class SubirScanRequest
{
    public string Nombre { get; set; } = "captura";
    public string ContentType { get; set; } = "image/jpeg";
    /// <summary>Contenido del archivo en base64.</summary>
    public string ContenidoBase64 { get; set; } = null!;
    public string Origen { get; set; } = "MOBILE";
}

/// <summary>Corrección manual de los campos extraídos.</summary>
public class CorregirScanRequest
{
    public string? EmisorNombre { get; set; }
    public string? EmisorNit { get; set; }
    public string? EmisorNrc { get; set; }
    public DateOnly? Fecha { get; set; }
    public string? TipoDocumento { get; set; }
    public string? NumeroControl { get; set; }
    public string? SelloRecibido { get; set; }
    public decimal? Subtotal { get; set; }
    public decimal? Iva { get; set; }
    public decimal? Total { get; set; }
    public string? Notas { get; set; }
}

/// <summary>Resultado recibido desde un proveedor externo de NeoScanAI (webhook-style).</summary>
public sealed class ScanResultadoRequest : CorregirScanRequest
{
    public decimal Confianza { get; set; }
    /// <summary>Si true, queda PROCESADO; si false, REQUIERE_REVISION.</summary>
    public bool Completo { get; set; }
}

/// <summary>Confirmar el escaneo como DTE recibido de un proveedor.</summary>
public sealed class RegistrarDteRecibidoRequest
{
    public string EmisorNombre { get; set; } = null!;
    public string? EmisorNit { get; set; }
    public string? EmisorNrc { get; set; }
    public DateOnly? Fecha { get; set; }
    public string? TipoDteCodigo { get; set; }
    public string? NumeroControl { get; set; }
    public string? SelloRecibido { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Iva { get; set; }
    public decimal Total { get; set; }
}

public sealed class ScanQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public string? EstadoCodigo { get; set; }
    public string? TipoClasificacion { get; set; }
}

/// <summary>DTE recibido de un proveedor (registro/respaldo), generado desde NeoScanAI.</summary>
public sealed class DteRecibidoDto
{
    public int Id { get; set; }
    public string EmisorNombre { get; set; } = string.Empty;
    public string? EmisorNit { get; set; }
    public string? EmisorNrc { get; set; }
    public DateOnly Fecha { get; set; }
    public string? TipoDteCodigo { get; set; }
    public string? NumeroControl { get; set; }
    public string? SelloRecibido { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Iva { get; set; }
    public decimal Total { get; set; }
    public int? ScanDocumentoId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class DteRecibidoQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public DateOnly? Desde { get; set; }
    public DateOnly? Hasta { get; set; }
}
