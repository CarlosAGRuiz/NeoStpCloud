using FluentAssertions;
using NeoSTP.Application.Dte;
using NeoSTP.Domain.Core.Dte;
using Xunit;

namespace NeoSTP.Tests.Unit.Dte;

public class DteCalculatorTests
{
    private readonly DteCalculator _calc = new();

    private static DteDocumento NewDoc(string tipo, params (decimal cant, decimal precio, decimal desc)[] lineas)
    {
        var d = new DteDocumento { TipoDteCodigo = tipo };
        int i = 1;
        foreach (var (c, p, ds) in lineas)
        {
            d.Detalles.Add(new DteDocumentoDetalle
            {
                NumeroLinea = i++,
                Codigo = $"P{i}",
                Descripcion = "Producto",
                Cantidad = c,
                PrecioUnitario = p,
                MontoDescuento = ds,
            });
        }
        return d;
    }

    [Fact]
    public void Factura_01_IvaIncluido_EnGravada()
    {
        // Factura: precio incluye IVA. 2 x 11.30 = 22.60 (gravada con IVA)
        var d = NewDoc(TipoDteCodigos.FacturaConsumidorFinal,
            (2, 11.30m, 0));
        _calc.Recalcular(d);

        d.TotalGravada.Should().Be(22.60m);
        d.SubTotalVentas.Should().Be(22.60m);
        d.SubTotal.Should().Be(22.60m);
        d.MontoTotalOperacion.Should().Be(22.60m);
        // IVA informativo = 22.60 * 0.13/1.13 ≈ 2.60
        d.IvaTotal.Should().Be(2.60m);
        d.TotalPagar.Should().Be(22.60m);
        d.TotalLetras.Should().Contain("DÓLARES");
    }

    [Fact]
    public void CCF_03_IvaSeparado()
    {
        // CCF: precio sin IVA. 2 x 10.00 = 20.00 gravada + 2.60 IVA = 22.60 total
        var d = NewDoc(TipoDteCodigos.ComprobanteCreditoFiscal,
            (2, 10m, 0));
        _calc.Recalcular(d);

        d.TotalGravada.Should().Be(20.00m);
        d.SubTotalVentas.Should().Be(20.00m);
        d.SubTotal.Should().Be(20.00m);
        d.IvaTotal.Should().Be(2.60m);
        d.MontoTotalOperacion.Should().Be(22.60m);
        d.TotalPagar.Should().Be(22.60m);
    }

    [Fact]
    public void NotaCredito_05_CalculaComoCcf()
    {
        var d = NewDoc(TipoDteCodigos.NotaCredito, (1, 100m, 0));
        _calc.Recalcular(d);
        d.TotalGravada.Should().Be(100m);
        d.IvaTotal.Should().Be(13m);
        d.MontoTotalOperacion.Should().Be(113m);
    }

    [Fact]
    public void SujetoExcluido_14_SinIva_VaComoNoSujeta()
    {
        var d = NewDoc(TipoDteCodigos.FacturaSujetoExcluido, (3, 50m, 0));
        _calc.Recalcular(d);

        d.TotalGravada.Should().Be(0m);
        d.TotalNoSujeto.Should().Be(150m);
        d.IvaTotal.Should().Be(0m);
        d.MontoTotalOperacion.Should().Be(150m);
        d.TotalPagar.Should().Be(150m);
    }

    [Fact]
    public void Descuento_PorLinea_AfectaTotales()
    {
        var d = NewDoc(TipoDteCodigos.ComprobanteCreditoFiscal,
            (1, 100m, 10m),
            (2, 50m, 0));
        _calc.Recalcular(d);

        // Línea 1: 100 - 10 = 90 gravada
        // Línea 2: 100 gravada
        d.TotalGravada.Should().Be(190m);
        d.IvaTotal.Should().Be(24.70m);
        d.MontoTotalOperacion.Should().Be(214.70m);
    }

    [Fact]
    public void NoGravado_VaComoNoSujeta()
    {
        var d = NewDoc(TipoDteCodigos.ComprobanteCreditoFiscal,
            (1, 100m, 0));
        d.Detalles.First().NoGravado = true;
        _calc.Recalcular(d);

        d.TotalGravada.Should().Be(0m);
        d.TotalNoSujeto.Should().Be(100m);
        d.IvaTotal.Should().Be(0m);
    }

    [Fact]
    public void ReteRenta_DescuentaDelTotal()
    {
        var d = NewDoc(TipoDteCodigos.ComprobanteCreditoFiscal, (1, 1000m, 0));
        d.ReteRenta = 100m;
        _calc.Recalcular(d);

        d.MontoTotalOperacion.Should().Be(1130m);
        d.TotalPagar.Should().Be(1030m);
    }

    [Fact]
    public void MontoEnLetras_FormatoCorrecto()
    {
        DteCalculator.MontoEnLetras(0m).Should().Be("CERO 00/100 DÓLARES");
        DteCalculator.MontoEnLetras(1.50m).Should().Be("UNO 50/100 DÓLARES");
        DteCalculator.MontoEnLetras(250.35m).Should().Contain("DOSCIENTOS CINCUENTA");
        DteCalculator.MontoEnLetras(250.35m).Should().Contain("35/100 DÓLARES");
        DteCalculator.MontoEnLetras(1000m).Should().Contain("MIL");
        DteCalculator.MontoEnLetras(1234567.89m).Should().Contain("MILLÓN");
    }
}
