using FluentAssertions;
using NeoSTP.Application.Rrhh;
using Xunit;

namespace NeoSTP.Tests.Unit.Rrhh;

/// <summary>
/// NEORRHH Sprint 1 — NominaCalculator (El Salvador): ISSS/AFP con tope, retención de Renta
/// por tramos y salario neto. Tasas/tablas parametrizables (defaults 2026).
/// </summary>
public class NominaCalculatorTests
{
    private readonly NominaCalculator _calc = new();
    private static NominaOptions Opts() => new();

    [Fact]
    public void Mensual_Salario1000_DesglosaIsssAfpRentaYNeto()
    {
        var r = _calc.CalcularMensual(1000m, Opts());

        r.IsssEmpleado.Should().Be(30.00m);   // 3% de 1000
        r.IsssPatronal.Should().Be(75.00m);   // 7.5% de 1000
        r.AfpEmpleado.Should().Be(72.50m);    // 7.25% de 1000
        r.AfpPatronal.Should().Be(87.50m);    // 8.75% de 1000
        r.BaseRenta.Should().Be(897.50m);     // 1000 - 30 - 72.50
        r.Renta.Should().Be(60.45m);          // tramo III: 60 + 20% de (897.50-895.24)
        r.TotalDeduccionesEmpleado.Should().Be(162.95m);
        r.SalarioNeto.Should().Be(837.05m);
        r.CostoPatronal.Should().Be(1162.50m);
    }

    [Fact]
    public void Mensual_AplicaTopeIsss()
    {
        var r = _calc.CalcularMensual(1500m, Opts());
        r.BaseIsss.Should().Be(1000m);        // tope ISSS
        r.IsssEmpleado.Should().Be(30.00m);
        r.AfpEmpleado.Should().Be(108.75m);   // AFP sin tope a este nivel: 7.25% de 1500
    }

    [Fact]
    public void Mensual_AplicaTopeAfp()
    {
        var r = _calc.CalcularMensual(8000m, Opts());
        r.BaseAfp.Should().Be(7401.10m);      // tope AFP
        r.AfpEmpleado.Should().Be(536.58m);   // 7.25% de 7401.10
    }

    [Theory]
    [InlineData(400, 0)]          // Tramo I: exento
    [InlineData(472, 0)]          // límite del exento
    [InlineData(600, 30.47)]      // Tramo II: 17.67 + 10% de (600-472)
    [InlineData(1361.25, 153.20)] // Tramo III: 60 + 20% de (1361.25-895.24)
    [InlineData(3000, 577.14)]    // Tramo IV: 288.57 + 30% de (3000-2038.10)
    public void Renta_PorTramos(decimal baseGravable, decimal esperado)
        => _calc.CalcularRenta(baseGravable, Opts().RentaMensual).Should().Be(esperado);

    [Fact]
    public void Mensual_SalarioBajo_SinRenta()
    {
        var r = _calc.CalcularMensual(365m, Opts());
        r.Renta.Should().Be(0m);              // base gravable < 472
        r.IsssEmpleado.Should().Be(10.95m);
        r.AfpEmpleado.Should().Be(26.46m);
        r.SalarioNeto.Should().Be(327.59m);
    }

    [Fact]
    public void Mensual_SalarioNegativo_SeTrataComoCero()
    {
        var r = _calc.CalcularMensual(-100m, Opts());
        r.SalarioBruto.Should().Be(0m);
        r.TotalDeduccionesEmpleado.Should().Be(0m);
        r.SalarioNeto.Should().Be(0m);
    }

    [Fact]
    public void Quincena_Salario1000_EsLaMitadDelMensual()
    {
        var r = _calc.CalcularQuincena(1000m, Opts());
        r.SalarioBruto.Should().Be(500.00m);
        r.IsssEmpleado.Should().Be(15.00m);   // 30 / 2
        r.AfpEmpleado.Should().Be(36.25m);    // 72.50 / 2
        r.Renta.Should().Be(30.23m);          // 60.45 / 2 (redondeo)
        r.TotalDeduccionesEmpleado.Should().Be(81.48m);
        r.SalarioNeto.Should().Be(418.52m);
        r.CostoPatronal.Should().Be(581.25m); // 500 + 37.50 + 43.75
    }

    [Fact]
    public void Quincena_DosQuincenas_ReconcilianElMes_AproxCentavo()
    {
        var opts = Opts();
        var q = _calc.CalcularQuincena(1000m, opts);
        var m = _calc.CalcularMensual(1000m, opts);
        (q.SalarioNeto * 2).Should().BeApproximately(m.SalarioNeto, 0.02m);
    }

    [Fact]
    public void Mensual_TasasParametrizables_SeRespetan()
    {
        var opts = new NominaOptions
        {
            Isss = new CotizacionOptions { PorcentajeEmpleado = 0.04m, PorcentajePatronal = 0.08m, TopeMensual = 0m },
            Afp = new CotizacionOptions { PorcentajeEmpleado = 0.07m, PorcentajePatronal = 0.08m, TopeMensual = 0m },
            RentaMensual = new List<RentaTramo>(), // sin tabla → sin renta
        };

        var r = _calc.CalcularMensual(2000m, opts);
        r.IsssEmpleado.Should().Be(80.00m);   // 4% de 2000, sin tope
        r.AfpEmpleado.Should().Be(140.00m);   // 7% de 2000
        r.Renta.Should().Be(0m);              // tabla vacía
    }
}
