using NeoSTP.Application.Productos;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Productos;

namespace NeoSTP.Application.Dte;

/// <summary>
/// Traduce precios comerciales a la semántica fiscal que exige cada esquema DTE.
/// Un tipo de origen null conserva el contrato histórico del endpoint.
/// </summary>
public static class DtePrecioNormalizer
{
    public readonly record struct Resultado(decimal PrecioUnitario, decimal MontoDescuento, string? TipoPrecio);

    public static Resultado Normalizar(
        string tipoDte,
        decimal precioUnitario,
        decimal montoDescuento,
        string? tipoPrecioOrigen,
        string clasificacion,
        bool noGravado,
        decimal ivaTasa = DteCalculator.IvaTasa)
    {
        if (string.IsNullOrWhiteSpace(tipoPrecioOrigen))
            return new(precioUnitario, montoDescuento, null);

        var origen = TipoPrecioCodigos.Normalizar(tipoPrecioOrigen);
        if (!TipoPrecioCodigos.EsValido(origen))
            throw new ArgumentException("El tipo de precio debe ser IVA_INCLUIDO o IVA_EXCLUIDO.", nameof(tipoPrecioOrigen));

        var gravada = !noGravado && string.Equals(clasificacion, "GRAVADA", StringComparison.OrdinalIgnoreCase);
        if (!gravada || tipoDte is TipoDteCodigos.FacturaExportacion or TipoDteCodigos.FacturaSujetoExcluido)
            return new(precioUnitario, montoDescuento, origen);

        var destino = UsaIvaSeparado(tipoDte)
            ? TipoPrecioCodigos.IvaExcluido
            : TipoPrecioCodigos.IvaIncluido;

        return new(
            PrecioIvaCalculator.ConvertirImporte(precioUnitario, true, origen, destino, ivaTasa),
            PrecioIvaCalculator.ConvertirImporte(montoDescuento, true, origen, destino, ivaTasa),
            destino);
    }

    public static bool UsaIvaSeparado(string tipoDte)
        => tipoDte is TipoDteCodigos.ComprobanteCreditoFiscal
            or TipoDteCodigos.NotaRemision
            or TipoDteCodigos.NotaCredito
            or TipoDteCodigos.NotaDebito
            or TipoDteCodigos.ComprobanteLiquidacion;
}
