using FluentAssertions;
using NeoSTP.Application.Profit;
using NeoSTP.Domain.Core.Dte;
using Xunit;

namespace NeoSTP.Tests.Unit.Profit;

/// <summary>
/// NeoProfit — reglas del ProfitCalculator: solo PROCESADO, NC resta, ND suma,
/// Sujeto Excluido sin IVA, y utilidad bruta con "costo pendiente".
/// </summary>
public class ProfitCalculatorTests
{
    private const string Procesado = "PROCESADO";

    private static VentaDteInput Venta(string tipo, string estado, decimal gravada, decimal iva = 0, decimal exenta = 0, decimal noSujeta = 0)
        => new(tipo, estado, gravada, exenta, noSujeta, iva);

    // ─── Ventas ──────────────────────────────────────────────────────────────

    [Fact]
    public void Ventas_SoloCuentaProcesado()
    {
        var docs = new[]
        {
            Venta(TipoDteCodigos.FacturaConsumidorFinal, Procesado, 100m, 13m),
            Venta(TipoDteCodigos.FacturaConsumidorFinal, "BORRADOR", 999m, 130m),
            Venta(TipoDteCodigos.FacturaConsumidorFinal, "RECHAZADO", 999m, 130m),
            Venta(TipoDteCodigos.FacturaConsumidorFinal, "INVALIDADO", 999m, 130m),
        };

        var r = ProfitCalculator.CalcularVentas(docs);

        r.Documentos.Should().Be(1);
        r.VentasGravadas.Should().Be(100m);
        r.IvaGenerado.Should().Be(13m);
        r.VentaNeta.Should().Be(100m);
    }

    [Fact]
    public void Ventas_NotaCreditoResta_NotaDebitoSuma()
    {
        var docs = new[]
        {
            Venta(TipoDteCodigos.ComprobanteCreditoFiscal, Procesado, 1000m, 130m),
            Venta(TipoDteCodigos.NotaCredito, Procesado, 200m, 26m),  // resta
            Venta(TipoDteCodigos.NotaDebito, Procesado, 50m, 6.5m),   // suma
        };

        var r = ProfitCalculator.CalcularVentas(docs);

        r.VentasGravadas.Should().Be(1000m - 200m + 50m); // 850
        r.IvaGenerado.Should().Be(130m - 26m + 6.5m);      // 110.5
        r.VentaNeta.Should().Be(850m);
        r.Documentos.Should().Be(3);
    }

    [Fact]
    public void Ventas_SujetoExcluido_NoGeneraIva()
    {
        // Aunque venga IvaTotal != 0, el SE no debe aportar IVA.
        var docs = new[]
        {
            Venta(TipoDteCodigos.FacturaSujetoExcluido, Procesado, 300m, 39m),
        };

        var r = ProfitCalculator.CalcularVentas(docs);

        r.VentasGravadas.Should().Be(300m);
        r.IvaGenerado.Should().Be(0m);
    }

    [Fact]
    public void Ventas_SumaGravadaExentaNoSujeta()
    {
        var docs = new[]
        {
            Venta(TipoDteCodigos.ComprobanteCreditoFiscal, Procesado, gravada: 100m, iva: 13m, exenta: 20m, noSujeta: 5m),
        };

        var r = ProfitCalculator.CalcularVentas(docs);

        r.VentaNeta.Should().Be(125m);
        r.VentasExentas.Should().Be(20m);
        r.VentasNoSujetas.Should().Be(5m);
    }

    // ─── Ganancia ────────────────────────────────────────────────────────────

    [Fact]
    public void Ganancia_UsaCostoUnitario_YMargen()
    {
        var lineas = new[]
        {
            new CostoLineaInput(TipoDteCodigos.FacturaConsumidorFinal, Procesado, Cantidad: 10m, MontoVenta: 1000m, CostoUnitario: 60m),
        };

        var r = ProfitCalculator.CalcularGanancia(lineas);

        r.CostoVentas.Should().Be(600m);     // 10 * 60
        r.GananciaBruta.Should().Be(400m);   // 1000 - 600
        r.MargenPorcentaje.Should().Be(40m); // 400/1000
        r.LineasSinCosto.Should().Be(0);
    }

    [Fact]
    public void Ganancia_LineaSinCosto_SeReportaPendiente()
    {
        var lineas = new[]
        {
            new CostoLineaInput(TipoDteCodigos.FacturaConsumidorFinal, Procesado, 5m, 500m, CostoUnitario: null),
            new CostoLineaInput(TipoDteCodigos.FacturaConsumidorFinal, Procesado, 2m, 200m, CostoUnitario: 30m),
        };

        var r = ProfitCalculator.CalcularGanancia(lineas);

        r.LineasSinCosto.Should().Be(1);
        r.CostoVentas.Should().Be(60m);   // solo la línea con costo (2*30)
        r.GananciaBruta.Should().Be(640m); // 700 - 60
    }

    [Fact]
    public void Ganancia_NotaCredito_RestaVentaYCosto()
    {
        var lineas = new[]
        {
            new CostoLineaInput(TipoDteCodigos.FacturaConsumidorFinal, Procesado, 10m, 1000m, 60m),
            new CostoLineaInput(TipoDteCodigos.NotaCredito, Procesado, 2m, 200m, 60m), // devolución
        };

        var r = ProfitCalculator.CalcularGanancia(lineas);

        r.CostoVentas.Should().Be(600m - 120m); // 480
        r.GananciaBruta.Should().Be(800m - 480m); // venta 1000-200=800; 800-480=320
    }

    [Fact]
    public void Ganancia_SinVentas_MargenCero()
    {
        var r = ProfitCalculator.CalcularGanancia(Array.Empty<CostoLineaInput>());

        r.GananciaBruta.Should().Be(0m);
        r.MargenPorcentaje.Should().Be(0m);
    }

    [Fact]
    public void Ganancia_IgnoraLineasNoProcesadas()
    {
        var lineas = new[]
        {
            new CostoLineaInput(TipoDteCodigos.FacturaConsumidorFinal, "BORRADOR", 10m, 1000m, 60m),
        };

        var r = ProfitCalculator.CalcularGanancia(lineas);

        r.CostoVentas.Should().Be(0m);
        r.GananciaBruta.Should().Be(0m);
    }
}
