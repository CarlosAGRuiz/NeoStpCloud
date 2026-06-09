using FluentAssertions;
using NeoSTP.Application.Compras;
using NeoSTP.Domain.Core.Compras;
using Xunit;

namespace NeoSTP.Tests.Unit.Compras;

/// <summary>NEOCOMPRAS — reglas puras de cuentas por pagar.</summary>
public class CuentasPagarCalculatorTests
{
    [Theory]
    [InlineData(100, 0, 100)]
    [InlineData(100, 40, 60)]
    [InlineData(100, 100, 0)]
    [InlineData(100, 150, 0)] // nunca negativo
    public void Saldo_RestaPagosConfirmados(decimal total, decimal pagado, decimal esperado)
        => CuentasPagarCalculator.Saldo(total, pagado).Should().Be(esperado);

    [Fact]
    public void Estado_SinPagos_Pendiente()
        => CuentasPagarCalculator.Estado(100, 0).Should().Be(FacturaCompraEstados.Pendiente);

    [Fact]
    public void Estado_PagoParcial_Parcial()
        => CuentasPagarCalculator.Estado(100, 40).Should().Be(FacturaCompraEstados.Parcial);

    [Fact]
    public void Estado_PagoTotal_Pagada()
        => CuentasPagarCalculator.Estado(100, 100).Should().Be(FacturaCompraEstados.Pagada);

    [Fact]
    public void Vencimiento_SumaPlazo()
        => CuentasPagarCalculator.Vencimiento(new DateOnly(2026, 6, 1), 30).Should().Be(new DateOnly(2026, 7, 1));

    [Fact]
    public void EstaVencida_SaldoYFechaPasada_True()
        => CuentasPagarCalculator.EstaVencida(50m, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 10)).Should().BeTrue();

    [Fact]
    public void EstaVencida_SinSaldo_False()
        => CuentasPagarCalculator.EstaVencida(0m, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 10)).Should().BeFalse();

    [Fact]
    public void DiasVencido_CuentaDias()
        => CuentasPagarCalculator.DiasVencido(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 9)).Should().Be(8);
}
