using FluentAssertions;
using NeoSTP.Application.Pos;
using Xunit;

namespace NeoSTP.Tests.Unit.Pos;

/// <summary>NEOPOS — cálculo puro de ventas (precios IVA incluido, tasa 13%).</summary>
public class PosCalculatorTests
{
    private const decimal Iva = 0.13m;

    [Fact]
    public void Linea_PrecioIvaIncluido_ExtraeIva()
    {
        var c = PosCalculator.CalcularLinea(new PosCalculator.LineaInput(2m, 11.30m, 0m, true), Iva);

        c.Total.Should().Be(22.60m);
        c.IvaLinea.Should().Be(2.60m);   // 22.60 - 22.60/1.13
        c.Subtotal.Should().Be(20.00m);
    }

    [Fact]
    public void Linea_Exenta_SinIva()
    {
        var c = PosCalculator.CalcularLinea(new PosCalculator.LineaInput(1m, 10m, 0m, false), Iva);

        c.IvaLinea.Should().Be(0m);
        c.Subtotal.Should().Be(10m);
        c.Total.Should().Be(10m);
    }

    [Fact]
    public void Linea_ConDescuento_RestaAntesDeIva()
    {
        var c = PosCalculator.CalcularLinea(new PosCalculator.LineaInput(1m, 113m, 13m, true), Iva);

        c.Total.Should().Be(100m);
        c.IvaLinea.Should().Be(11.50m); // 100 - 100/1.13 = 11.504 → 11.50
    }

    [Fact]
    public void Venta_SumaLineas()
    {
        var lineas = new[]
        {
            new PosCalculator.LineaInput(2m, 11.30m, 0m, true),  // total 22.60
            new PosCalculator.LineaInput(1m, 10m, 0m, false),    // total 10.00 exenta
        };

        var t = PosCalculator.CalcularVenta(lineas, Iva);

        t.Total.Should().Be(32.60m);
        t.IvaTotal.Should().Be(2.60m);
        t.Subtotal.Should().Be(30.00m);
    }

    [Theory]
    [InlineData(20, 50, 30)]
    [InlineData(20, 20, 0)]
    [InlineData(20, 10, 0)] // insuficiente → 0
    public void Cambio_CalculaDevuelto(decimal total, decimal recibido, decimal esperado)
        => PosCalculator.Cambio(total, recibido).Should().Be(esperado);

    [Fact]
    public void Cambio_SinEfectivo_Cero()
        => PosCalculator.Cambio(20m, null).Should().Be(0m);
}
