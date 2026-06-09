using FluentAssertions;
using NeoSTP.Application.Inventario;
using Xunit;

namespace NeoSTP.Tests.Unit.Inventario;

/// <summary>INVENTARIO — costeo por promedio ponderado (puro).</summary>
public class CostoPromedioCalculatorTests
{
    [Fact]
    public void Entrada_DesdeCero_FijaCosto()
    {
        var s = CostoPromedioCalculator.Entrada(new(0, 0), 10, 2m);
        s.Cantidad.Should().Be(10);
        s.CostoPromedio.Should().Be(2m);
    }

    [Fact]
    public void Entrada_PromediaPonderado()
    {
        // 10 @ 2.00 + 10 @ 4.00 = 20 @ 3.00
        var s1 = CostoPromedioCalculator.Entrada(new(0, 0), 10, 2m);
        var s2 = CostoPromedioCalculator.Entrada(s1, 10, 4m);
        s2.Cantidad.Should().Be(20);
        s2.CostoPromedio.Should().Be(3m);
    }

    [Fact]
    public void Salida_NoCambiaCosto_BajaCantidad()
    {
        var s = CostoPromedioCalculator.Salida(new(20, 3m), 5);
        s.Cantidad.Should().Be(15);
        s.CostoPromedio.Should().Be(3m);
    }

    [Fact]
    public void Salida_NoNegativa()
    {
        var s = CostoPromedioCalculator.Salida(new(5, 3m), 10);
        s.Cantidad.Should().Be(0);
    }

    [Fact]
    public void Ajuste_FijaCantidadYConservaCostoSiNull()
    {
        var s = CostoPromedioCalculator.Ajuste(new(20, 3m), 12, null);
        s.Cantidad.Should().Be(12);
        s.CostoPromedio.Should().Be(3m);
    }

    [Fact]
    public void Ajuste_ConCosto_FijaCosto()
    {
        var s = CostoPromedioCalculator.Ajuste(new(20, 3m), 12, 5m);
        s.CostoPromedio.Should().Be(5m);
    }

    [Fact]
    public void ValorInventario_Multiplica()
        => CostoPromedioCalculator.ValorInventario(10, 2.5m).Should().Be(25m);
}
