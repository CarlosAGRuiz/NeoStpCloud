using NeoSTP.Domain.Common;

namespace NeoSTP.Domain.Core.Compras;

/// <summary>
/// Pago realizado a un proveedor contra una factura de compra (cuenta por pagar).
/// Espejo de <c>PagoCliente</c>. Solo CONFIRMADO reduce el saldo de la factura.
/// </summary>
public class PagoProveedor : AuditableEntity
{
    public int EmpresaId { get; set; }

    public int FacturaCompraId { get; set; }
    public FacturaCompra FacturaCompra { get; set; } = null!;

    public DateOnly Fecha { get; set; }
    public decimal Monto { get; set; }

    /// <summary>EFECTIVO, TRANSFERENCIA, TARJETA, CHEQUE, QR, OTRO.</summary>
    public string FormaPagoCodigo { get; set; } = "TRANSFERENCIA";

    public string? Referencia { get; set; }
    public string? Nota { get; set; }

    /// <summary>Movimiento de tesorería generado por este pago (egreso), si aplica.</summary>
    public int? MovimientoTesoreriaId { get; set; }

    /// <summary>CONFIRMADO | ANULADO. Solo CONFIRMADO reduce el saldo.</summary>
    public string EstadoCodigo { get; set; } = PagoProveedorEstados.Confirmado;
}

public static class PagoProveedorEstados
{
    public const string Confirmado = "CONFIRMADO";
    public const string Anulado = "ANULADO";
}
