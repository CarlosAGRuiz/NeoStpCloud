using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Domain.Core.Cobranza;

/// <summary>
/// Pago registrado por el cliente contra un DTE (factura/CCF a crédito).
/// El saldo de una factura se deriva: TotalPagar − Σ(pagos CONFIRMADOS).
/// No es un documento fiscal; es seguimiento de cobranza (cuentas por cobrar).
/// </summary>
public class PagoCliente : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    /// <summary>DTE (factura/CCF) al que se aplica el pago.</summary>
    public int DteDocumentoId { get; set; }
    public DteDocumento DteDocumento { get; set; } = null!;

    public DateOnly Fecha { get; set; }
    public decimal Monto { get; set; }

    /// <summary>EFECTIVO, TRANSFERENCIA, TARJETA, CHEQUE, QR, OTRO.</summary>
    public string FormaPagoCodigo { get; set; } = "EFECTIVO";

    /// <summary>Referencia bancaria / número de comprobante.</summary>
    public string? Referencia { get; set; }
    public string? Nota { get; set; }

    /// <summary>URL/ruta del comprobante adjunto (opcional).</summary>
    public string? ComprobanteUrl { get; set; }

    /// <summary>CONFIRMADO | PENDIENTE_REVISION | ANULADO. Solo CONFIRMADO reduce el saldo.</summary>
    public string EstadoCodigo { get; set; } = PagoEstados.Confirmado;
}

public static class PagoEstados
{
    public const string Confirmado = "CONFIRMADO";
    public const string PendienteRevision = "PENDIENTE_REVISION";
    public const string Anulado = "ANULADO";
}

public static class CobroEstados
{
    public const string Pagado = "PAGADO";
    public const string Pendiente = "PENDIENTE";
    public const string Vencido = "VENCIDO";
}
