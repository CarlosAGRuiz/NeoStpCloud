namespace NeoSTP.Domain.Core.Dte;

/// <summary>
/// Reglas de los dos documentos de liquidación por cuenta de terceros:
/// Comprobante de Liquidación (08, esquema fe-cl-v2) y Documento Contable de
/// Liquidación (09, esquema fe-dcl-v2).
/// <para>
/// El 08 lo emite el mandatario (comisionista) al mandante y lista, documento por
/// documento, las ventas hechas por su cuenta. El 09 es el corte del período: no
/// lleva líneas sino un solo bloque con el total liquidado, la percepción de IVA
/// del 2 %, la comisión del mandatario y el líquido a pagar.
/// </para>
/// </summary>
public static class DteLiquidacion
{
    /// <summary>Percepción de IVA que el agente aplica sobre las operaciones liquidadas (2 %).</summary>
    public const decimal TasaPercepcion = 0.02m;

    /// <summary>Comisión del mandatario cuando el documento no la especifica (5 %).</summary>
    public const decimal PorcentajeComisionDefault = 5m;

    /// <summary>
    /// Deriva el bloque económico del DCL (09) a partir del valor bruto de las operaciones
    /// del período (IVA incluido, tal como se le facturó al consumidor):
    /// <code>
    /// subTotal              = valorOperaciones - montoSinPercepcion
    /// montoSujetoPercepcion = subTotal / (1 + IVA)      → base sin IVA
    /// iva                   = subTotal - montoSujetoPercepcion
    /// ivaPercibido          = montoSujetoPercepcion * 2 %
    /// comision              = montoSujetoPercepcion * porcentComision
    /// ivaComision           = comision * IVA
    /// liquidoApagar         = subTotal - ivaPercibido - comision - ivaComision
    /// </code>
    /// El esquema de Hacienda exige que <c>valorOperaciones</c>, <c>subTotal</c>, <c>iva</c>,
    /// <c>montoSujetoPercepcion</c>, <c>ivaPercibido</c> y <c>liquidoApagar</c> sean
    /// estrictamente mayores que cero (<c>exclusiveMinimum: 0</c>); la validación del
    /// documento lo verifica antes de generar.
    /// </summary>
    public static LiquidacionTotales Calcular(
        decimal valorOperaciones,
        decimal montoSinPercepcion,
        decimal porcentajeComision,
        decimal ivaTasa)
    {
        var subTotal = Round2(valorOperaciones - montoSinPercepcion);
        var montoSujetoPercepcion = Round2(subTotal / (1m + ivaTasa));
        var iva = Round2(subTotal - montoSujetoPercepcion);
        var ivaPercibido = Round2(montoSujetoPercepcion * TasaPercepcion);
        var comision = Round2(montoSujetoPercepcion * porcentajeComision / 100m);
        var ivaComision = Round2(comision * ivaTasa);
        var liquidoAPagar = Round2(subTotal - ivaPercibido - comision - ivaComision);

        return new LiquidacionTotales(
            SubTotal: subTotal,
            MontoSujetoPercepcion: montoSujetoPercepcion,
            Iva: iva,
            IvaPercibido: ivaPercibido,
            Comision: comision,
            IvaComision: ivaComision,
            LiquidoAPagar: liquidoAPagar);
    }

    private static decimal Round2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}

/// <summary>Bloque económico calculado del Documento Contable de Liquidación (09).</summary>
public readonly record struct LiquidacionTotales(
    decimal SubTotal,
    decimal MontoSujetoPercepcion,
    decimal Iva,
    decimal IvaPercibido,
    decimal Comision,
    decimal IvaComision,
    decimal LiquidoAPagar);
