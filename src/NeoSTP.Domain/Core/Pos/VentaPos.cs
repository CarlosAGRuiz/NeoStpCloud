using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Domain.Core.Pos;

/// <summary>
/// Venta de punto de venta (ticket de cobro). No es un DTE: es un comprobante de venta interno
/// que puede <b>promoverse</b> a Factura/CCF electrónica después (campo <see cref="DteDocumentoId"/>).
/// Aislada por EmpresaId.
/// </summary>
public class VentaPos : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    public int? SucursalId { get; set; }
    public int? PuntoVentaId { get; set; }

    /// <summary>Correlativo interno por empresa, p. ej. POS-000001.</summary>
    public string Numero { get; set; } = null!;

    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    public int? ClienteId { get; set; }
    public string ClienteNombre { get; set; } = "Consumidor final";

    /// <summary>EFECTIVO, TARJETA, TRANSFERENCIA, QR, OTRO.</summary>
    public string FormaPagoCodigo { get; set; } = "EFECTIVO";
    public decimal? EfectivoRecibido { get; set; }
    public decimal? Cambio { get; set; }

    public decimal Subtotal { get; set; }       // gravado + exento, sin IVA
    public decimal IvaTotal { get; set; }
    public decimal TotalDescuento { get; set; }
    public decimal Total { get; set; }

    public string? Nota { get; set; }

    /// <summary>COMPLETADA | ANULADA.</summary>
    public string EstadoCodigo { get; set; } = VentaPosEstados.Completada;

    /// <summary>NO_FACTURADA | FACTURADA (cuando se promueve a DTE).</summary>
    public string EstadoFacturacion { get; set; } = VentaPosFacturacion.NoFacturada;

    /// <summary>DTE generado al promover la venta a Factura/CCF (Sprint 2), si aplica.</summary>
    public int? DteDocumentoId { get; set; }

    /// <summary>Sesión / corte de caja a la que pertenece la venta (Sprint 4), si hay una abierta.</summary>
    public int? SesionCajaId { get; set; }

    public ICollection<VentaPosLinea> Lineas { get; set; } = new List<VentaPosLinea>();
}

public static class VentaPosEstados
{
    public const string Completada = "COMPLETADA";
    public const string Anulada = "ANULADA";
}

public static class VentaPosFacturacion
{
    public const string NoFacturada = "NO_FACTURADA";
    public const string Facturada = "FACTURADA";
}

public static class FormasPagoPos
{
    public const string Efectivo = "EFECTIVO";
    public const string Tarjeta = "TARJETA";
    public const string Transferencia = "TRANSFERENCIA";
    public const string Qr = "QR";
    public const string Otro = "OTRO";

    public static readonly string[] All = [Efectivo, Tarjeta, Transferencia, Qr, Otro];
}
