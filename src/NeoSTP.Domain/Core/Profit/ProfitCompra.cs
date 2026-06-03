using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Domain.Core.Profit;

/// <summary>
/// Compra de mercadería / insumos registrada manualmente (o desde NeoScan a futuro).
/// Alimenta costo de ventas e IVA crédito fiscal en NeoProfit. No es un documento fiscal emitido.
/// </summary>
public class ProfitCompra : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    public DateOnly Fecha { get; set; }

    public string Proveedor { get; set; } = null!;
    public string? NumeroDocumento { get; set; }
    public string? Descripcion { get; set; }

    /// <summary>Subtotal sin IVA.</summary>
    public decimal Subtotal { get; set; }

    /// <summary>IVA crédito fiscal de la compra.</summary>
    public decimal IvaMonto { get; set; }

    /// <summary>ACTIVO / INACTIVO (soft-delete, sin borrado físico).</summary>
    public string EstadoCodigo { get; set; } = "ACTIVO";

    public decimal Total => Subtotal + IvaMonto;
}
