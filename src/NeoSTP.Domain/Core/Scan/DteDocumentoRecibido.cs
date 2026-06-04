using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Domain.Core.Scan;

/// <summary>
/// DTE recibido de un proveedor (registro/respaldo). Se origina al confirmar un escaneo
/// como "DTE recibido". No es un documento emitido por la empresa.
/// </summary>
public class DteDocumentoRecibido : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    public string EmisorNombre { get; set; } = null!;
    public string? EmisorNit { get; set; }
    public string? EmisorNrc { get; set; }

    public DateOnly Fecha { get; set; }
    public string? TipoDteCodigo { get; set; }
    public string? NumeroControl { get; set; }
    public string? SelloRecibido { get; set; }

    public decimal Subtotal { get; set; }
    public decimal Iva { get; set; }
    public decimal Total { get; set; }

    /// <summary>Escaneo de origen, si vino de NeoScanAI.</summary>
    public int? ScanDocumentoId { get; set; }
}
