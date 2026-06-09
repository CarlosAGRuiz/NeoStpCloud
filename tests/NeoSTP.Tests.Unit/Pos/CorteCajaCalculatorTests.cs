using FluentAssertions;
using NeoSTP.Application.Pos;
using Xunit;

namespace NeoSTP.Tests.Unit.Pos;

/// <summary>NEOPOS — corte de caja (puro): efectivo esperado y diferencia.</summary>
public class CorteCajaCalculatorTests
{
    [Fact]
    public void Esperado_SumaFondoYEfectivo()
        => CorteCajaCalculator.Esperado(50m, 120.50m).Should().Be(170.50m);

    [Fact]
    public void Diferencia_Sobrante_EsPositiva()
        => CorteCajaCalculator.Diferencia(175m, 170.50m).Should().Be(4.50m);

    [Fact]
    public void Diferencia_Faltante_EsNegativa()
        => CorteCajaCalculator.Diferencia(168m, 170.50m).Should().Be(-2.50m);

    [Fact]
    public void Diferencia_Cuadrada_EsCero()
        => CorteCajaCalculator.Diferencia(170.50m, 170.50m).Should().Be(0m);
}
