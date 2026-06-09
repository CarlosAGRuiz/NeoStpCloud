using NeoSTP.Domain.Core.Compras;

namespace NeoSTP.Application.Compras;

/// <summary>
/// Reglas puras de cuentas por pagar (testeable sin BD), espejo de <c>CobranzaCalculator</c>:
/// - Saldo = Total − Σ(pagos CONFIRMADOS).
/// - Estado: PAGADA (saldo 0) · PARCIAL (saldo &lt; total) · PENDIENTE (sin pagos) · ANULADA (externo).
/// - Vencida si saldo &gt; 0 y fecha de vencimiento &lt; hoy.
/// </summary>
public static class CuentasPagarCalculator
{
    public static decimal Saldo(decimal total, decimal pagadoConfirmado)
    {
        var s = total - pagadoConfirmado;
        return s < 0 ? 0m : s;
    }

    public static DateOnly Vencimiento(DateOnly fechaEmision, int plazoDias)
        => fechaEmision.AddDays(plazoDias > 0 ? plazoDias : 0);

    /// <summary>Estado de pago derivado del saldo (no considera ANULADA, que es un estado explícito).</summary>
    public static string Estado(decimal total, decimal pagadoConfirmado)
    {
        var saldo = Saldo(total, pagadoConfirmado);
        if (saldo <= 0) return FacturaCompraEstados.Pagada;
        return pagadoConfirmado > 0 ? FacturaCompraEstados.Parcial : FacturaCompraEstados.Pendiente;
    }

    public static bool EstaVencida(decimal saldo, DateOnly vencimiento, DateOnly hoy)
        => saldo > 0 && vencimiento < hoy;

    public static int DiasVencido(DateOnly vencimiento, DateOnly hoy)
        => vencimiento < hoy ? hoy.DayNumber - vencimiento.DayNumber : 0;
}
