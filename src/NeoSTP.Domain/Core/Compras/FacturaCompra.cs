using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Domain.Core.Compras;

/// <summary>
/// Factura/documento de compra recibido de un proveedor (cuenta por pagar). El saldo se
/// deriva: Total − Σ(pagos CONFIRMADOS). Espejo de cuentas por cobrar (Cobranza), pero del
/// lado de egresos. No es un DTE emitido por nosotros; es el documento del proveedor.
/// </summary>
public class FacturaCompra : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    public int ProveedorId { get; set; }
    public Proveedor Proveedor { get; set; } = null!;

    /// <summary>Número de documento del proveedor (factura/CCF/recibo).</summary>
    public string NumeroDocumento { get; set; } = null!;

    /// <summary>FACTURA | CCF | RECIBO | OTRO.</summary>
    public string TipoDocumento { get; set; } = "FACTURA";

    public DateOnly FechaEmision { get; set; }
    public DateOnly FechaVencimiento { get; set; }

    /// <summary>CONTADO | CREDITO.</summary>
    public string CondicionPago { get; set; } = "CREDITO";

    public decimal Subtotal { get; set; }
    public decimal Iva { get; set; }
    public decimal Total { get; set; }

    /// <summary>Si el IVA es crédito fiscal deducible (CCF).</summary>
    public bool IvaDeducible { get; set; }

    public string? Descripcion { get; set; }

    /// <summary>PENDIENTE | PARCIAL | PAGADA | ANULADA. Derivado del saldo; se persiste para consultas.</summary>
    public string EstadoCodigo { get; set; } = FacturaCompraEstados.Pendiente;

    /// <summary>Gasto de NeoProfit generado al registrarla (para P&amp;L sin doble captura), si aplica.</summary>
    public int? ProfitGastoId { get; set; }

    public ICollection<PagoProveedor> Pagos { get; set; } = new List<PagoProveedor>();
}

public static class FacturaCompraEstados
{
    public const string Pendiente = "PENDIENTE";
    public const string Parcial = "PARCIAL";
    public const string Pagada = "PAGADA";
    public const string Anulada = "ANULADA";
}

public static class TiposDocumentoCompra
{
    public const string Factura = "FACTURA";
    public const string Ccf = "CCF";
    public const string Recibo = "RECIBO";
    public const string Otro = "OTRO";

    public static readonly string[] All = [Factura, Ccf, Recibo, Otro];
}

public static class CondicionesPagoCompra
{
    public const string Contado = "CONTADO";
    public const string Credito = "CREDITO";

    public static readonly string[] All = [Contado, Credito];
}
