using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Clientes;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Domain.Core.Dte;

/// <summary>
/// Cabecera de un Documento Tributario Electrónico (DTE).
/// Cubre los tipos: 01 Factura, 03 CCF, 05 Nota de Crédito, 06 Nota de Débito, 14 Sujeto Excluido.
/// </summary>
public class DteDocumento : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    public int? SucursalId { get; set; }
    public int? PuntoVentaId { get; set; }

    /// <summary>Catálogo CAT-002: 01, 03, 05, 06, 14, etc.</summary>
    public string TipoDteCodigo { get; set; } = TipoDteCodigos.FacturaConsumidorFinal;

    /// <summary>Versión del esquema DTE (por defecto 1).</summary>
    public int VersionDte { get; set; } = 1;

    /// <summary>PRUEBAS / PRODUCCION (heredado de la configuración al emitir).</summary>
    public string AmbienteCodigo { get; set; } = "PRUEBAS";

    /// <summary>Número de control: DTE-{tipo}-{cod-establecimiento}{cod-puntoventa}-{15 dígitos}.</summary>
    public string NumeroControl { get; set; } = null!;

    /// <summary>UUID v4 generado al emitir (en mayúsculas).</summary>
    public string CodigoGeneracion { get; set; } = null!;

    /// <summary>Sello recibido de Hacienda al procesar (null hasta procesar).</summary>
    public string? SelloRecibido { get; set; }

    /// <summary>Modelo de facturación: 1 Previo, 2 Diferido.</summary>
    public int ModeloFacturacion { get; set; } = DteModeloFacturacion.Previo;

    /// <summary>Tipo transmisión: 1 Normal, 2 Contingencia.</summary>
    public int TipoTransmision { get; set; } = DteTipoTransmision.Normal;

    public string? TipoContingenciaCodigo { get; set; }
    public string? MotivoContingencia { get; set; }

    public DateTime FechaEmision { get; set; } = DateTime.UtcNow.Date;
    public TimeSpan HoraEmision { get; set; } = DateTime.UtcNow.TimeOfDay;

    /// <summary>Catálogo MONEDA (USD por defecto en El Salvador).</summary>
    public string TipoMonedaCodigo { get; set; } = "USD";

    // --- Receptor ---
    public int? ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    // Snapshot del cliente (queda fijo en el DTE)
    public string? ReceptorTipoDocumento { get; set; }
    public string? ReceptorNumeroDocumento { get; set; }
    public string? ReceptorNrc { get; set; }
    public string? ReceptorNombre { get; set; }
    public string? ReceptorTipoContribuyente { get; set; }
    public string? ReceptorCodigoActividad { get; set; }
    public string? ReceptorActividadEconomica { get; set; }
    public string? ReceptorDepartamentoCodigo { get; set; }
    public string? ReceptorMunicipioCodigo { get; set; }
    public string? ReceptorDireccion { get; set; }
    public string? ReceptorCorreo { get; set; }
    public string? ReceptorTelefono { get; set; }

    // --- Operación ---
    public string CondicionOperacionCodigo { get; set; } = "1"; // 1 Contado, 2 Crédito, 3 Otro
    public string? FormaPagoCodigo { get; set; }
    public int? PlazoDias { get; set; }
    public string? Periodo { get; set; }

    // --- Documento relacionado (NC, ND, Sujeto Excluido modificatorios) ---
    public int? DocumentoRelacionadoId { get; set; }
    public DteDocumento? DocumentoRelacionado { get; set; }
    public string? NumeroDocumentoRelacionado { get; set; }
    public string? TipoDteRelacionado { get; set; }
    public string? TipoGeneracionRelacionado { get; set; } // 1 físico, 2 electrónico

    // --- Venta a tercero ---
    public string? VentaTerceroNit { get; set; }
    public string? VentaTerceroNombre { get; set; }

    public string? Observaciones { get; set; }

    // --- Totales (calculados desde detalles) ---
    public decimal TotalNoSujeto { get; set; }
    public decimal TotalExenta { get; set; }
    public decimal TotalGravada { get; set; }
    public decimal SubTotalVentas { get; set; }
    public decimal DescuentoNoSujeto { get; set; }
    public decimal DescuentoExenta { get; set; }
    public decimal DescuentoGravada { get; set; }
    public decimal PorcentajeDescuento { get; set; }
    public decimal TotalDescuento { get; set; }

    public decimal IvaTotal { get; set; }
    public decimal IvaRetenido { get; set; }
    public decimal ReteRenta { get; set; }

    public decimal SubTotal { get; set; }
    public decimal MontoTotalOperacion { get; set; }
    public decimal TotalNoGravado { get; set; }
    public decimal TotalPagar { get; set; }
    public string? TotalLetras { get; set; }

    /// <summary>Estado del ciclo de vida (BORRADOR / GENERADO / VALIDADO / etc.).</summary>
    public string EstadoCodigo { get; set; } = DteEstadoCodigos.Borrador;

    public DateTime? GeneradoAt { get; set; }
    public DateTime? ValidadoAt { get; set; }
    public DateTime? EnviadoAt { get; set; }
    public DateTime? ProcesadoAt { get; set; }

    // ── Retransmisión automática (sprint 9) ───────────────────────────
    /// <summary>Número de intentos de retransmisión realizados por el Worker.</summary>
    public int IntentoRetransmision { get; set; }

    /// <summary>Fecha y hora UTC del último intento de retransmisión automática.</summary>
    public DateTime? UltimoIntentoRetransmisionAt { get; set; }

    public ICollection<DteDocumentoDetalle> Detalles { get; set; } = new List<DteDocumentoDetalle>();
    public DteDocumentoJson? Json { get; set; }

    public bool EsCreditoFiscal => TipoDteCodigo == TipoDteCodigos.ComprobanteCreditoFiscal;
    public bool EsNotaCredito => TipoDteCodigo == TipoDteCodigos.NotaCredito;
    public bool EsNotaDebito => TipoDteCodigo == TipoDteCodigos.NotaDebito;
    public bool EsSujetoExcluido => TipoDteCodigo == TipoDteCodigos.FacturaSujetoExcluido;
}
