using FluentAssertions;
using NeoSTP.Application.Reportes;
using Xunit;

namespace NeoSTP.Tests.Unit.Reportes;

/// <summary>NEOBI fiscal — LibroIvaCalculator (puro): libros IVA y F-07.</summary>
public class LibroIvaCalculatorTests
{
    private static VentaFiscalRow Venta(string tipo, decimal gravada, decimal iva, decimal exenta = 0, int dia = 5)
        => new(new DateOnly(2026, 6, dia), tipo, $"DTE-{tipo}-X-{Guid.NewGuid():N}", "Cliente", "123456-7", gravada, exenta, 0, iva);

    [Fact]
    public void VentasConsumidor_DesglosaIvaIncluido_PorDia()
    {
        // FC 01: gravada CON IVA (113 → neta 100, débito 13). Dos del mismo día se agrupan.
        var rows = new[] { Venta("01", 113m, 13m), Venta("01", 226m, 26m), Venta("01", 113m, 13m, dia: 6) };

        var libro = LibroIvaCalculator.VentasConsumidor(rows);

        libro.Should().HaveCount(2);
        libro[0].Documentos.Should().Be(2);
        libro[0].GravadasConIva.Should().Be(339m);
        libro[0].VentasNetas.Should().Be(300m);
        libro[0].DebitoFiscal.Should().Be(39m);
    }

    [Fact]
    public void VentasContribuyentes_NcResta_NdSuma()
    {
        // CCF 03: neto 1000 + débito 130 · NC 05 resta · ND 06 suma.
        var rows = new[] { Venta("03", 1000m, 130m), Venta("05", 200m, 26m), Venta("06", 100m, 13m) };

        var libro = LibroIvaCalculator.VentasContribuyentes(rows);

        libro.Should().HaveCount(3);
        libro.Sum(x => x.VentaNeta).Should().Be(900m);     // 1000 − 200 + 100
        libro.Sum(x => x.DebitoFiscal).Should().Be(117m);  // 130 − 26 + 13
        libro.Single(x => x.TipoDte == "05").VentaNeta.Should().Be(-200m);
    }

    [Fact]
    public void VentasConsumidor_IgnoraTiposContribuyente_YViceversa()
    {
        var rows = new[] { Venta("01", 113m, 13m), Venta("03", 1000m, 130m) };

        LibroIvaCalculator.VentasConsumidor(rows).Should().HaveCount(1);
        LibroIvaCalculator.VentasContribuyentes(rows).Should().HaveCount(1);
    }

    [Fact]
    public void Compras_CreditoSoloSiDeducible()
    {
        var rows = new[]
        {
            new CompraFiscalRow(new DateOnly(2026, 6, 3), "F-001", "Proveedor A", "111", 500m, 65m, IvaDeducible: true),
            new CompraFiscalRow(new DateOnly(2026, 6, 4), "F-002", "Proveedor B", "222", 300m, 39m, IvaDeducible: false),
        };

        var libro = LibroIvaCalculator.Compras(rows);

        libro[0].CreditoFiscal.Should().Be(65m);
        libro[1].CreditoFiscal.Should().Be(0m);
        libro[1].IvaNoDeducible.Should().Be(39m);
        libro.Sum(x => x.Total).Should().Be(904m);
    }

    [Fact]
    public void F07_DebitoMayor_ImpuestoAPagar()
    {
        var ventas = new[] { Venta("01", 113m, 13m), Venta("03", 1000m, 130m) };
        var compras = new[] { new CompraFiscalRow(new DateOnly(2026, 6, 3), "F-1", "P", null, 500m, 65m, true) };

        var f07 = LibroIvaCalculator.F07(
            LibroIvaCalculator.VentasConsumidor(ventas),
            LibroIvaCalculator.VentasContribuyentes(ventas),
            LibroIvaCalculator.Compras(compras));

        f07.VentasNetasGravadas.Should().Be(1100m); // 100 (neta FC) + 1000 (CCF)
        f07.DebitoFiscal.Should().Be(143m);          // 13 + 130
        f07.CreditoFiscal.Should().Be(65m);
        f07.ImpuestoDeterminado.Should().Be(78m);    // 143 − 65
        f07.RemanenteCredito.Should().Be(0m);
    }

    [Fact]
    public void F07_CreditoMayor_Remanente()
    {
        var ventas = new[] { Venta("01", 113m, 13m) };
        var compras = new[] { new CompraFiscalRow(new DateOnly(2026, 6, 3), "F-1", "P", null, 1000m, 130m, true) };

        var f07 = LibroIvaCalculator.F07(
            LibroIvaCalculator.VentasConsumidor(ventas),
            LibroIvaCalculator.VentasContribuyentes(ventas),
            LibroIvaCalculator.Compras(compras));

        f07.ImpuestoDeterminado.Should().Be(0m);
        f07.RemanenteCredito.Should().Be(117m); // 130 − 13
    }
}
