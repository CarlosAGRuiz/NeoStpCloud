using FluentAssertions;
using NeoSTP.Application.Rrhh;
using Xunit;

namespace NeoSTP.Tests.Unit.Rrhh;

public class PrestacionesCalculatorTests
{
    [Theory]
    [InlineData("2025-06-20", "2026-06-19", 0)]
    [InlineData("2025-06-20", "2026-06-20", 1)]
    [InlineData("2016-06-20", "2026-06-20", 10)]
    public void AntiguedadAnios_RespetaAniversario(string ingreso, string corte, int esperado)
        => PrestacionesCalculator.AntiguedadAnios(DateOnly.Parse(ingreso), DateOnly.Parse(corte))
            .Should().Be(esperado);

    [Fact]
    public void Vacaciones_DevengaPeriodosCompletos()
        => PrestacionesCalculator.DiasVacacionDevengados(
            new DateOnly(2024, 1, 15), new DateOnly(2026, 1, 14), 12, 15)
            .Should().Be(15);

    [Fact]
    public void PrimaVacacion_AplicaSalarioDiarioDiasYPorcentaje()
        => PrestacionesCalculator.PrimaVacacion(900m, 5, .30m).Should().Be(45m);

    [Theory]
    [InlineData(2, 15)]
    [InlineData(3, 19)]
    [InlineData(10, 21)]
    public void Aguinaldo_SeleccionaTramoPorAntiguedad(int anios, decimal dias)
    {
        var corte = new DateOnly(2026, 12, 12);
        var resultado = PrestacionesCalculator.CalcularAguinaldo(
            corte.AddYears(-anios), corte, 900m, 3, 10, 15m, 19m, 21m);

        resultado.Dias.Should().Be(dias);
        resultado.Monto.Should().Be(30m * dias);
        resultado.EsProporcional.Should().BeFalse();
    }

    [Fact]
    public void Aguinaldo_MenorDeUnAnio_EsProporcional()
    {
        var resultado = PrestacionesCalculator.CalcularAguinaldo(
            new DateOnly(2026, 7, 1), new DateOnly(2026, 12, 12), 900m, 3, 10, 15m, 19m, 21m);

        resultado.EsProporcional.Should().BeTrue();
        resultado.Dias.Should().BeApproximately(6.7808m, .0001m);
        resultado.Monto.Should().Be(203.42m);
    }
}
