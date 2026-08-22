using FluentAssertions;
using NeoSTP.Application.Dte;
using NeoSTP.Domain.Core.Dte;
using Xunit;

namespace NeoSTP.Tests.Unit.Dte;

/// <summary>
/// DTE 09 — Documento Contable de Liquidación (fe-dcl-v2): derivación del corte del
/// período (percepción de IVA del 2 %, comisión del mandatario y líquido a pagar).
/// </summary>
public class DteLiquidacionTests
{
    private const decimal Iva = DteCalculator.IvaTasa;

    [Fact]
    public void Calcular_DesglosaElIvaContenidoEnLasOperaciones()
    {
        var t = DteLiquidacion.Calcular(
            valorOperaciones: 1130m, montoSinPercepcion: 0m, porcentajeComision: 5m, ivaTasa: Iva);

        t.SubTotal.Should().Be(1130m);
        t.MontoSujetoPercepcion.Should().Be(1000m);   // base sin IVA
        t.Iva.Should().Be(130m);
        (t.MontoSujetoPercepcion + t.Iva).Should().Be(t.SubTotal);
    }

    [Fact]
    public void Calcular_PercibeElDosPorCientoSobreLaBaseSinIva()
    {
        var t = DteLiquidacion.Calcular(1130m, 0m, 5m, Iva);

        t.IvaPercibido.Should().Be(20m); // 2% de 1000
    }

    [Fact]
    public void Calcular_ComisionYSuIvaSalenDeLaBaseSinIva()
    {
        var t = DteLiquidacion.Calcular(1130m, 0m, 5m, Iva);

        t.Comision.Should().Be(50m);      // 5% de 1000
        t.IvaComision.Should().Be(6.50m); // 13% de la comisión
    }

    [Fact]
    public void Calcular_LiquidoAPagarDescuentaPercepcionYComision()
    {
        var t = DteLiquidacion.Calcular(1130m, 0m, 5m, Iva);

        // 1130 - 20 (percepción) - 50 (comisión) - 6.50 (IVA comisión)
        t.LiquidoAPagar.Should().Be(1053.50m);
    }

    [Fact]
    public void Calcular_ElMontoSinPercepcionSaleDeLaBase()
    {
        var t = DteLiquidacion.Calcular(1130m, montoSinPercepcion: 130m, porcentajeComision: 5m, ivaTasa: Iva);

        t.SubTotal.Should().Be(1000m);
        t.MontoSujetoPercepcion.Should().Be(884.96m); // 1000 / 1.13
        t.IvaPercibido.Should().Be(17.70m);
    }

    [Fact]
    public void Calcular_SinComisionDejaLosCamposEnCero()
    {
        // El esquema admite comisión 0 ("minimum": 0), a diferencia de los demás importes.
        var t = DteLiquidacion.Calcular(1130m, 0m, porcentajeComision: 0m, ivaTasa: Iva);

        t.Comision.Should().Be(0m);
        t.IvaComision.Should().Be(0m);
        t.LiquidoAPagar.Should().Be(1110m); // solo se descuenta la percepción
    }

    // ── Integración con el calculador del documento ──────────────────────────

    [Fact]
    public void Recalcular_Dcl_TomaLasLineasComoValorBrutoDeOperaciones()
    {
        var d = new DteDocumento
        {
            TipoDteCodigo = TipoDteCodigos.DocumentoContableLiquidacion,
            LiquidacionPorcentajeComision = 5m,
        };
        d.Detalles.Add(new DteDocumentoDetalle
        {
            NumeroLinea = 1, Codigo = "OP-1", Descripcion = "Ventas del período",
            Cantidad = 1m, PrecioUnitario = 1130m,
        });

        new DteCalculator().Recalcular(d);

        d.MontoTotalOperacion.Should().Be(1130m);     // valorOperaciones
        d.SubTotal.Should().Be(1130m);
        d.TotalGravada.Should().Be(1000m);            // montoSujetoPercepcion
        d.IvaTotal.Should().Be(130m);
        d.LiquidacionIvaPercibido.Should().Be(20m);
        d.LiquidacionComision.Should().Be(50m);
        d.LiquidacionIvaComision.Should().Be(6.50m);
        d.TotalPagar.Should().Be(1053.50m);           // liquidoApagar
        d.TotalLetras.Should().Be("MIL CINCUENTA Y TRES 50/100 DÓLARES");
    }

    [Fact]
    public void Recalcular_Dcl_AplicaComisionPorDefectoCuandoNoSeIndica()
    {
        var d = new DteDocumento { TipoDteCodigo = TipoDteCodigos.DocumentoContableLiquidacion };
        d.Detalles.Add(new DteDocumentoDetalle
        {
            NumeroLinea = 1, Codigo = "OP-1", Descripcion = "Ventas del período",
            Cantidad = 1m, PrecioUnitario = 1130m,
        });

        new DteCalculator().Recalcular(d);

        d.LiquidacionPorcentajeComision.Should().Be(DteLiquidacion.PorcentajeComisionDefault);
        d.LiquidacionComision.Should().Be(50m);
    }

    [Fact]
    public void Recalcular_ComprobanteLiquidacion_SeparaElIvaComoElCcf()
    {
        // El 08 sí es un documento de ventas: precio SIN IVA y tributo 20 aparte.
        var d = new DteDocumento { TipoDteCodigo = TipoDteCodigos.ComprobanteLiquidacion };
        d.Detalles.Add(new DteDocumentoDetalle
        {
            NumeroLinea = 1, Codigo = "76F19422-085D-45B7-A998-4374A3A8EAD7",
            Descripcion = "Venta por cuenta del mandante",
            Cantidad = 1m, PrecioUnitario = 100m,
        });

        new DteCalculator().Recalcular(d);

        d.TotalGravada.Should().Be(100m);
        d.IvaTotal.Should().Be(13m);
        d.MontoTotalOperacion.Should().Be(113m);
        d.TotalPagar.Should().Be(113m);
    }
}
