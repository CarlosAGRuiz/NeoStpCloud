using NeoSTP.Domain.Core.Dte;

namespace NeoSTP.Application.Dte;

/// <summary>
/// Implementación por defecto: recalcula totales del documento siguiendo
/// las reglas de Hacienda El Salvador para Factura (01), CCF (03),
/// Nota de Crédito (05), Nota de Débito (06) y Sujeto Excluido (14).
/// </summary>
public class DteCalculator : IDteCalculator
{
    public const decimal IvaTasa = 0.13m;

    public void Recalcular(DteDocumento d)
    {
        var esCcfONota = d.TipoDteCodigo == TipoDteCodigos.ComprobanteCreditoFiscal
                         || d.TipoDteCodigo == TipoDteCodigos.NotaCredito
                         || d.TipoDteCodigo == TipoDteCodigos.NotaDebito;
        var esSujetoExcluido = d.TipoDteCodigo == TipoDteCodigos.FacturaSujetoExcluido;

        decimal totalGravada = 0, totalExenta = 0, totalNoSujeta = 0, ivaTotal = 0;

        foreach (var l in d.Detalles)
        {
            var bruto = Round4(l.Cantidad * l.PrecioUnitario);
            var neto = Round4(bruto - l.MontoDescuento);
            if (neto < 0) neto = 0;

            // Reset clasificaciones
            l.VentaGravada = 0;
            l.VentaExenta = 0;
            l.VentaNoSujeta = 0;
            l.IvaItem = 0;

            if (esSujetoExcluido)
            {
                // Sujeto excluido: la venta va como NoSujeta (sin IVA).
                l.VentaNoSujeta = neto;
                totalNoSujeta += neto;
                continue;
            }

            if (l.NoGravado)
            {
                l.VentaNoSujeta = neto;
                totalNoSujeta += neto;
                continue;
            }

            // Caso normal: gravada
            l.VentaGravada = neto;

            if (esCcfONota)
            {
                // CCF: precio SIN IVA. IVA por línea = gravada * 0.13
                l.IvaItem = Round2(neto * IvaTasa);
            }
            else
            {
                // Factura 01: precio CON IVA incluido. IVA informativo = gravada * 0.13/1.13
                l.IvaItem = Round2(neto * IvaTasa / (1m + IvaTasa));
            }

            totalGravada += neto;
            ivaTotal += l.IvaItem;
        }

        d.TotalGravada = Round2(totalGravada);
        d.TotalExenta = Round2(totalExenta);
        d.TotalNoSujeto = Round2(totalNoSujeta);

        // SubTotalVentas en formato Hacienda:
        // - Factura: precios incluyen IVA → suma directa
        // - CCF: precios sin IVA → suma de gravadas (IVA se separa luego)
        d.SubTotalVentas = Round2(d.TotalGravada + d.TotalExenta + d.TotalNoSujeto);

        d.DescuentoGravada = 0;
        d.DescuentoExenta = 0;
        d.DescuentoNoSujeto = 0;
        d.TotalDescuento = 0;
        d.PorcentajeDescuento = 0;

        d.SubTotal = Round2(d.SubTotalVentas - d.TotalDescuento);

        if (esSujetoExcluido)
        {
            d.IvaTotal = 0;
            d.MontoTotalOperacion = d.SubTotal;
            d.TotalNoGravado = 0;
            d.TotalPagar = Round2(d.MontoTotalOperacion - d.ReteRenta);
        }
        else if (esCcfONota)
        {
            d.IvaTotal = Round2(ivaTotal);
            d.MontoTotalOperacion = Round2(d.SubTotal + d.IvaTotal);
            d.TotalPagar = Round2(d.MontoTotalOperacion - d.IvaRetenido - d.ReteRenta);
            d.TotalNoGravado = 0;
        }
        else
        {
            // Factura 01: IVA ya está incluido en la gravada (solo informativo).
            d.IvaTotal = Round2(ivaTotal);
            d.MontoTotalOperacion = d.SubTotal;
            d.TotalPagar = Round2(d.MontoTotalOperacion - d.IvaRetenido - d.ReteRenta);
            d.TotalNoGravado = 0;
        }

        d.TotalLetras = MontoEnLetras(d.TotalPagar);
    }

    private static decimal Round2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
    private static decimal Round4(decimal v) => Math.Round(v, 4, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Convierte un monto a letras (formato MH: "DOSCIENTOS CINCUENTA 35/100 DÓLARES").
    /// Implementación simple para hasta 999,999.99.
    /// </summary>
    public static string MontoEnLetras(decimal monto)
    {
        if (monto < 0) monto = Math.Abs(monto);
        var entero = (long)Math.Floor(monto);
        var centavos = (int)Math.Round((monto - entero) * 100m, MidpointRounding.AwayFromZero);
        var letras = entero == 0 ? "CERO" : NumeroALetras(entero);
        return $"{letras} {centavos:00}/100 DÓLARES";
    }

    private static readonly string[] Unidades =
    {
        "", "UNO", "DOS", "TRES", "CUATRO", "CINCO", "SEIS", "SIETE", "OCHO", "NUEVE",
        "DIEZ", "ONCE", "DOCE", "TRECE", "CATORCE", "QUINCE",
        "DIECISÉIS", "DIECISIETE", "DIECIOCHO", "DIECINUEVE",
        "VEINTE", "VEINTIUNO", "VEINTIDÓS", "VEINTITRÉS", "VEINTICUATRO",
        "VEINTICINCO", "VEINTISÉIS", "VEINTISIETE", "VEINTIOCHO", "VEINTINUEVE"
    };
    private static readonly string[] Decenas =
    {
        "", "", "VEINTE", "TREINTA", "CUARENTA", "CINCUENTA",
        "SESENTA", "SETENTA", "OCHENTA", "NOVENTA"
    };
    private static readonly string[] Centenas =
    {
        "", "CIENTO", "DOSCIENTOS", "TRESCIENTOS", "CUATROCIENTOS", "QUINIENTOS",
        "SEISCIENTOS", "SETECIENTOS", "OCHOCIENTOS", "NOVECIENTOS"
    };

    private static string NumeroALetras(long n)
    {
        if (n == 0) return "";
        if (n < 30) return Unidades[(int)n];
        if (n < 100)
        {
            var dec = n / 10;
            var uni = n % 10;
            return uni == 0 ? Decenas[(int)dec] : $"{Decenas[(int)dec]} Y {Unidades[(int)uni]}";
        }
        if (n == 100) return "CIEN";
        if (n < 1000)
        {
            var c = n / 100;
            var r = n % 100;
            return r == 0 ? Centenas[(int)c] : $"{Centenas[(int)c]} {NumeroALetras(r)}";
        }
        if (n < 1_000_000)
        {
            var miles = n / 1000;
            var r = n % 1000;
            var milesText = miles == 1 ? "MIL" : $"{NumeroALetras(miles)} MIL";
            return r == 0 ? milesText : $"{milesText} {NumeroALetras(r)}";
        }
        var millones = n / 1_000_000;
        var rmillones = n % 1_000_000;
        var millonesText = millones == 1 ? "UN MILLÓN" : $"{NumeroALetras(millones)} MILLONES";
        return rmillones == 0 ? millonesText : $"{millonesText} {NumeroALetras(rmillones)}";
    }
}
