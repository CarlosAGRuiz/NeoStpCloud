namespace NeoSTP.Application.Reportes;

/// <summary>Fila fuente de ventas: DTE PROCESADO del período (01/03/05/06).</summary>
public sealed record VentaFiscalRow(
    DateOnly Fecha, string TipoDte, string NumeroControl, string? ReceptorNombre, string? ReceptorNrc,
    decimal Gravada, decimal Exenta, decimal NoSujeta, decimal Iva);

/// <summary>Fila fuente de compras: facturas de compra no anuladas del período.</summary>
public sealed record CompraFiscalRow(
    DateOnly Fecha, string NumeroDocumento, string Proveedor, string? ProveedorNrc,
    decimal Neto, decimal Iva, bool IvaDeducible);

/// <summary>
/// NEOBI fiscal (V2-D1) — cálculo puro de libros IVA El Salvador (simplificado):
/// ventas a consumidor final (FC 01, precios CON IVA, agrupado por día), ventas a
/// contribuyentes (CCF 03 detallado; NC 05 resta, ND 06 suma), compras (crédito fiscal
/// solo si es deducible) y resumen F-07 (débito − crédito = impuesto o remanente).
/// Los documentos INVALIDADOS/RECHAZADOS quedan excluidos desde la fuente (solo PROCESADO).
/// </summary>
public static class LibroIvaCalculator
{
    public const decimal IvaTasa = 0.13m;

    public static List<VentasConsumidorDiaDto> VentasConsumidor(IEnumerable<VentaFiscalRow> rows)
        => rows.Where(r => r.TipoDte == "01")
            .GroupBy(r => r.Fecha)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var conIva = g.Sum(r => r.Gravada); // FC: gravada CON IVA incluido
                var neta = Round2(conIva / (1m + IvaTasa));
                return new VentasConsumidorDiaDto
                {
                    Fecha = g.Key,
                    Documentos = g.Count(),
                    Exentas = Round2(g.Sum(r => r.Exenta)),
                    NoSujetas = Round2(g.Sum(r => r.NoSujeta)),
                    GravadasConIva = Round2(conIva),
                    VentasNetas = neta,
                    DebitoFiscal = Round2(conIva - neta),
                };
            }).ToList();

    public static List<VentasContribuyenteRowDto> VentasContribuyentes(IEnumerable<VentaFiscalRow> rows)
        => rows.Where(r => r.TipoDte is "03" or "05" or "06")
            .OrderBy(r => r.Fecha).ThenBy(r => r.NumeroControl)
            .Select(r =>
            {
                var signo = r.TipoDte == "05" ? -1m : 1m; // NC resta, ND/CCF suman
                return new VentasContribuyenteRowDto
                {
                    Fecha = r.Fecha,
                    TipoDte = r.TipoDte,
                    NumeroControl = r.NumeroControl,
                    Receptor = r.ReceptorNombre ?? "",
                    ReceptorNrc = r.ReceptorNrc,
                    Exenta = Round2(r.Exenta * signo),
                    VentaNeta = Round2(r.Gravada * signo),  // CCF: gravada SIN IVA
                    DebitoFiscal = Round2(r.Iva * signo),
                    Total = Round2((r.Gravada + r.Exenta + r.NoSujeta + r.Iva) * signo),
                };
            }).ToList();

    public static List<ComprasRowDto> Compras(IEnumerable<CompraFiscalRow> rows)
        => rows.OrderBy(r => r.Fecha).ThenBy(r => r.NumeroDocumento)
            .Select(r => new ComprasRowDto
            {
                Fecha = r.Fecha,
                NumeroDocumento = r.NumeroDocumento,
                Proveedor = r.Proveedor,
                ProveedorNrc = r.ProveedorNrc,
                ComprasNetas = Round2(r.Neto),
                CreditoFiscal = r.IvaDeducible ? Round2(r.Iva) : 0m,
                IvaNoDeducible = r.IvaDeducible ? 0m : Round2(r.Iva),
                Total = Round2(r.Neto + r.Iva),
            }).ToList();

    public static ResumenF07Dto F07(
        IReadOnlyList<VentasConsumidorDiaDto> consumidor,
        IReadOnlyList<VentasContribuyenteRowDto> contribuyentes,
        IReadOnlyList<ComprasRowDto> compras)
    {
        var ventasNetas = Round2(consumidor.Sum(d => d.VentasNetas) + contribuyentes.Sum(c => c.VentaNeta));
        var debito = Round2(consumidor.Sum(d => d.DebitoFiscal) + contribuyentes.Sum(c => c.DebitoFiscal));
        var comprasNetas = Round2(compras.Sum(c => c.ComprasNetas));
        var credito = Round2(compras.Sum(c => c.CreditoFiscal));
        var determinado = Round2(debito - credito);
        return new ResumenF07Dto
        {
            VentasExentas = Round2(consumidor.Sum(d => d.Exentas) + contribuyentes.Sum(c => c.Exenta)),
            VentasNetasGravadas = ventasNetas,
            DebitoFiscal = debito,
            ComprasNetasGravadas = comprasNetas,
            CreditoFiscal = credito,
            ImpuestoDeterminado = determinado > 0 ? determinado : 0m,
            RemanenteCredito = determinado < 0 ? Round2(-determinado) : 0m,
        };
    }

    private static decimal Round2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}

// ── DTOs ─────────────────────────────────────────────────────────────────────

public sealed class VentasConsumidorDiaDto
{
    public DateOnly Fecha { get; set; }
    public int Documentos { get; set; }
    public decimal Exentas { get; set; }
    public decimal NoSujetas { get; set; }
    public decimal GravadasConIva { get; set; }
    public decimal VentasNetas { get; set; }
    public decimal DebitoFiscal { get; set; }
}

public sealed class VentasContribuyenteRowDto
{
    public DateOnly Fecha { get; set; }
    public string TipoDte { get; set; } = null!;
    public string NumeroControl { get; set; } = null!;
    public string Receptor { get; set; } = "";
    public string? ReceptorNrc { get; set; }
    public decimal Exenta { get; set; }
    public decimal VentaNeta { get; set; }
    public decimal DebitoFiscal { get; set; }
    public decimal Total { get; set; }
}

public sealed class ComprasRowDto
{
    public DateOnly Fecha { get; set; }
    public string NumeroDocumento { get; set; } = null!;
    public string Proveedor { get; set; } = null!;
    public string? ProveedorNrc { get; set; }
    public decimal ComprasNetas { get; set; }
    public decimal CreditoFiscal { get; set; }
    public decimal IvaNoDeducible { get; set; }
    public decimal Total { get; set; }
}

public sealed class ResumenF07Dto
{
    public decimal VentasExentas { get; set; }
    public decimal VentasNetasGravadas { get; set; }
    public decimal DebitoFiscal { get; set; }
    public decimal ComprasNetasGravadas { get; set; }
    public decimal CreditoFiscal { get; set; }
    /// <summary>IVA a pagar del período (si débito &gt; crédito).</summary>
    public decimal ImpuestoDeterminado { get; set; }
    /// <summary>Remanente de crédito fiscal (si crédito &gt; débito).</summary>
    public decimal RemanenteCredito { get; set; }
}

public sealed class LibroFiscalDto<T>
{
    public int Anio { get; set; }
    public int Mes { get; set; }
    public List<T> Filas { get; set; } = [];
}
