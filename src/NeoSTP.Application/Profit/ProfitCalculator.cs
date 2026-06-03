using NeoSTP.Domain.Core.Dte;

namespace NeoSTP.Application.Profit;

// ─── Entradas (proyecciones desde la BD; el cálculo es puro y testeable) ──────

/// <summary>Cabecera de DTE necesaria para el cálculo de ventas.</summary>
public sealed record VentaDteInput(
    string TipoDteCodigo,
    string EstadoCodigo,
    decimal TotalGravada,
    decimal TotalExenta,
    decimal TotalNoSujeto,
    decimal IvaTotal);

/// <summary>Línea de DTE con costo, para el cálculo de utilidad bruta.</summary>
public sealed record CostoLineaInput(
    string TipoDteCodigo,
    string EstadoCodigo,
    decimal Cantidad,
    decimal MontoVenta,
    decimal? CostoUnitario);

// ─── Resultados ───────────────────────────────────────────────────────────────

public sealed record ResumenVentas
{
    public decimal VentasGravadas { get; init; }
    public decimal VentasExentas { get; init; }
    public decimal VentasNoSujetas { get; init; }
    public decimal IvaGenerado { get; init; }
    /// <summary>Ventas netas = gravada + exenta + no sujeta (NC ya restada, ND sumada).</summary>
    public decimal VentaNeta { get; init; }
    public int Documentos { get; init; }
}

public sealed record ResumenGanancia
{
    public decimal CostoVentas { get; init; }
    public decimal GananciaBruta { get; init; }
    /// <summary>Margen bruto en % sobre la venta neta (0 si no hay ventas).</summary>
    public decimal MargenPorcentaje { get; init; }
    /// <summary>Número de líneas sin costo conocido (costo pendiente).</summary>
    public int LineasSinCosto { get; init; }
}

/// <summary>
/// Cálculos financieros de NeoProfit sobre DTE emitidos. Reglas de negocio:
/// solo cuenta PROCESADO; Nota de Crédito resta; Nota de Débito suma;
/// Sujeto Excluido no genera IVA; producto sin costo se reporta como "costo pendiente".
/// </summary>
public static class ProfitCalculator
{
    /// <summary>Signo del documento: Nota de Crédito resta (-1); el resto suma (+1).</summary>
    public static int Signo(string tipoDteCodigo)
        => tipoDteCodigo == TipoDteCodigos.NotaCredito ? -1 : 1;

    /// <summary>Solo los documentos PROCESADO entran al cálculo financiero.</summary>
    public static bool EsComputable(string estadoCodigo)
        => estadoCodigo == DteEstadoCodigos.Procesado;

    /// <summary>Sujeto Excluido (14) no genera IVA.</summary>
    public static bool GeneraIva(string tipoDteCodigo)
        => tipoDteCodigo != TipoDteCodigos.FacturaSujetoExcluido;

    public static ResumenVentas CalcularVentas(IEnumerable<VentaDteInput> documentos)
    {
        decimal gravada = 0, exenta = 0, noSujeta = 0, iva = 0;
        var n = 0;

        foreach (var d in documentos)
        {
            if (!EsComputable(d.EstadoCodigo)) continue;
            var s = Signo(d.TipoDteCodigo);

            gravada += s * d.TotalGravada;
            exenta += s * d.TotalExenta;
            noSujeta += s * d.TotalNoSujeto;
            iva += s * (GeneraIva(d.TipoDteCodigo) ? d.IvaTotal : 0m);
            n++;
        }

        return new ResumenVentas
        {
            VentasGravadas = gravada,
            VentasExentas = exenta,
            VentasNoSujetas = noSujeta,
            IvaGenerado = iva,
            VentaNeta = gravada + exenta + noSujeta,
            Documentos = n,
        };
    }

    public static ResumenGanancia CalcularGanancia(IEnumerable<CostoLineaInput> lineas)
    {
        decimal ventaNeta = 0, costo = 0;
        var sinCosto = 0;

        foreach (var l in lineas)
        {
            if (!EsComputable(l.EstadoCodigo)) continue;
            var s = Signo(l.TipoDteCodigo);

            ventaNeta += s * l.MontoVenta;
            if (l.CostoUnitario is decimal c)
                costo += s * l.Cantidad * c;
            else
                sinCosto++;
        }

        var ganancia = ventaNeta - costo;
        var margen = ventaNeta != 0 ? Math.Round(ganancia / ventaNeta * 100m, 2) : 0m;

        return new ResumenGanancia
        {
            CostoVentas = costo,
            GananciaBruta = ganancia,
            MargenPorcentaje = margen,
            LineasSinCosto = sinCosto,
        };
    }
}
