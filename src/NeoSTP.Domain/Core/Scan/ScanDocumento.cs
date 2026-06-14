using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Domain.Core.Scan;

/// <summary>
/// Documento capturado/escaneado (NeoScanAI). Vive en la "bandeja" hasta que el usuario
/// revisa, corrige y confirma sus campos para convertirlo en gasto, compra o DTE recibido.
/// Los campos extraídos se guardan denormalizados en esta entidad (v1).
/// </summary>
public class ScanDocumento : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    /// <summary>RECIBIDO | PROCESANDO | PROCESADO | REQUIERE_REVISION | CONFIRMADO | RECHAZADO | ERROR.</summary>
    public string EstadoCodigo { get; set; } = ScanEstados.Recibido;

    /// <summary>COMPRA | GASTO | DTE_RECIBIDO | RESPALDO. Null hasta clasificar/confirmar.</summary>
    public string? TipoClasificacion { get; set; }

    /// <summary>MOBILE | WEB | API.</summary>
    public string Origen { get; set; } = "MOBILE";

    // ── Archivo capturado ──
    public byte[]? ArchivoBlob { get; set; }

    /// <summary>
    /// V2.5-S4: ruta/clave del archivo cuando se externaliza a storage
    /// (Scan:Storage:Provider=FileSystem). Excluyente con <see cref="ArchivoBlob"/>.
    /// </summary>
    public string? ArchivoPath { get; set; }

    public string? ArchivoContentType { get; set; }
    public string? ArchivoNombre { get; set; }

    // ── Campos extraídos (OCR/IA) — corregibles por el usuario ──
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
    /// <summary>Confianza global de la extracción (0..1). 0 = requiere captura manual.</summary>
    public decimal Confianza { get; set; }

    /// <summary>Proveedor OCR/IA usado en el ultimo intento: Mock, Gemini u otro.</summary>
    public string? OcrProveedor { get; set; }

    /// <summary>Modelo/version usado por el proveedor en el ultimo intento.</summary>
    public string? OcrModelo { get; set; }

    /// <summary>Duracion del ultimo intento OCR en milisegundos.</summary>
    public long? OcrDuracionMs { get; set; }

    /// <summary>Error resumido del ultimo intento OCR, sin secretos ni payload completo.</summary>
    public string? OcrErrorResumen { get; set; }

    /// <summary>Total de intentos OCR hechos sobre este documento.</summary>
    public int OcrIntentos { get; set; }

    /// <summary>Fecha UTC del ultimo intento OCR.</summary>
    public DateTime? OcrUltimoIntentoAt { get; set; }

    public string? Notas { get; set; }

    // ── Referencias creadas al confirmar ──
    public int? ProfitGastoId { get; set; }
    public int? ProfitCompraId { get; set; }
    public int? DteRecibidoId { get; set; }
}

public static class ScanEstados
{
    public const string Recibido = "RECIBIDO";
    public const string Procesando = "PROCESANDO";
    public const string Procesado = "PROCESADO";
    public const string RequiereRevision = "REQUIERE_REVISION";
    public const string Confirmado = "CONFIRMADO";
    public const string Rechazado = "RECHAZADO";
    public const string Error = "ERROR";
}

public static class ScanTipos
{
    public const string Compra = "COMPRA";
    public const string Gasto = "GASTO";
    public const string DteRecibido = "DTE_RECIBIDO";
    public const string Respaldo = "RESPALDO";
}
