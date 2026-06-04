using FluentAssertions;
using NeoSTP.Application.Cobranza;
using NeoSTP.Domain.Core.Cobranza;
using NeoSTP.Domain.Core.Dte;
using Xunit;

namespace NeoSTP.Tests.Unit.Cobranza;

/// <summary>
/// Reglas puras de cuentas por cobrar: cobrable, crédito, saldo, vencimiento y estado.
/// </summary>
public class CobranzaCalculatorTests
{
    [Theory]
    [InlineData("1", false)]
    [InlineData("2", true)]
    [InlineData("3", true)]
    public void EsCredito(string cond, bool esperado)
        => CobranzaCalculator.EsCredito(cond).Should().Be(esperado);

    [Theory]
    [InlineData(TipoDteCodigos.FacturaConsumidorFinal, true)]
    [InlineData(TipoDteCodigos.ComprobanteCreditoFiscal, true)]
    [InlineData(TipoDteCodigos.NotaCredito, false)]
    [InlineData(TipoDteCodigos.FacturaSujetoExcluido, false)]
    public void EsCobrable(string tipo, bool esperado)
        => CobranzaCalculator.EsCobrable(tipo).Should().Be(esperado);

    [Fact]
    public void Saldo_RestaPagosYNoBajaDeCero()
    {
        CobranzaCalculator.Saldo(100m, 30m).Should().Be(70m);
        CobranzaCalculator.Saldo(100m, 100m).Should().Be(0m);
        CobranzaCalculator.Saldo(100m, 150m).Should().Be(0m); // clamp
    }

    [Fact]
    public void Vencimiento_SumaPlazo()
    {
        var emision = new DateOnly(2026, 6, 1);
        CobranzaCalculator.Vencimiento(emision, 30).Should().Be(new DateOnly(2026, 7, 1));
        CobranzaCalculator.Vencimiento(emision, null).Should().Be(emision);
        CobranzaCalculator.Vencimiento(emision, 0).Should().Be(emision);
    }

    [Fact]
    public void EstadoCobro_PagadoPendienteVencido()
    {
        var hoy = new DateOnly(2026, 7, 5);
        CobranzaCalculator.EstadoCobro(0m, new DateOnly(2026, 6, 1), hoy).Should().Be(CobroEstados.Pagado);
        CobranzaCalculator.EstadoCobro(50m, new DateOnly(2026, 7, 10), hoy).Should().Be(CobroEstados.Pendiente);
        CobranzaCalculator.EstadoCobro(50m, new DateOnly(2026, 7, 1), hoy).Should().Be(CobroEstados.Vencido);
    }

    [Fact]
    public void DiasVencido_CuentaSoloSiVencida()
    {
        var hoy = new DateOnly(2026, 7, 5);
        CobranzaCalculator.DiasVencido(new DateOnly(2026, 7, 1), hoy).Should().Be(4);
        CobranzaCalculator.DiasVencido(new DateOnly(2026, 7, 10), hoy).Should().Be(0);
    }
}
