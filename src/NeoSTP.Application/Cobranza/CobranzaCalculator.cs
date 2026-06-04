using NeoSTP.Domain.Core.Cobranza;
using NeoSTP.Domain.Core.Dte;

namespace NeoSTP.Application.Cobranza;

/// <summary>
/// Reglas puras de cuentas por cobrar (testeable sin BD):
/// - Solo facturas/CCF PROCESADO a crédito (condición 2/3) generan cuenta por cobrar.
/// - Saldo = TotalPagar − Σ(pagos CONFIRMADOS).
/// - Vencimiento = fecha de emisión + plazo (días). Vencida si saldo &gt; 0 y vencimiento &lt; hoy.
/// </summary>
public static class CobranzaCalculator
{
    /// <summary>Condiciones de operación que generan cuenta por cobrar (no contado).</summary>
    public static bool EsCredito(string condicionOperacionCodigo)
        => condicionOperacionCodigo is "2" or "3";

    /// <summary>Tipos de DTE cobrables (venta): Factura (01) y CCF (03).</summary>
    public static bool EsCobrable(string tipoDteCodigo)
        => tipoDteCodigo is TipoDteCodigos.FacturaConsumidorFinal or TipoDteCodigos.ComprobanteCreditoFiscal;

    public static decimal Saldo(decimal totalPagar, decimal pagadoConfirmado)
    {
        var s = totalPagar - pagadoConfirmado;
        return s < 0 ? 0m : s;
    }

    public static DateOnly Vencimiento(DateOnly fechaEmision, int? plazoDias)
        => fechaEmision.AddDays(plazoDias is > 0 ? plazoDias.Value : 0);

    public static string EstadoCobro(decimal saldo, DateOnly vencimiento, DateOnly hoy)
    {
        if (saldo <= 0) return CobroEstados.Pagado;
        return vencimiento < hoy ? CobroEstados.Vencido : CobroEstados.Pendiente;
    }

    public static int DiasVencido(DateOnly vencimiento, DateOnly hoy)
        => vencimiento < hoy ? hoy.DayNumber - vencimiento.DayNumber : 0;
}
