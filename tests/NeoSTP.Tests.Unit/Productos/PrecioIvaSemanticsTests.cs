using FluentAssertions;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Productos;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Productos;
using Xunit;

namespace NeoSTP.Tests.Unit.Productos;

public class PrecioIvaSemanticsTests
{
    private const decimal Iva = 0.13m;

    [Fact]
    public void IvaIncluido_CientoTrece_ProduceBaseCienEImpuestoTrece()
    {
        var r = PrecioIvaCalculator.CalcularLinea(
            new(1m, 113m, 0m, true, TipoPrecioCodigos.IvaIncluido), Iva);

        r.Subtotal.Should().Be(100m);
        r.Iva.Should().Be(13m);
        r.Total.Should().Be(113m);
    }

    [Fact]
    public void IvaExcluido_Cien_ProduceBaseCienEImpuestoTrece()
    {
        var r = PrecioIvaCalculator.CalcularLinea(
            new(1m, 100m, 0m, true, TipoPrecioCodigos.IvaExcluido), Iva);

        r.Subtotal.Should().Be(100m);
        r.Iva.Should().Be(13m);
        r.Total.Should().Be(113m);
    }

    [Fact]
    public void IvaExcluido_DescuentoSeAplicaAntesDelImpuesto()
    {
        var r = PrecioIvaCalculator.CalcularLinea(
            new(1m, 100m, 10m, true, TipoPrecioCodigos.IvaExcluido), Iva);

        r.Subtotal.Should().Be(90m);
        r.Iva.Should().Be(11.70m);
        r.Total.Should().Be(101.70m);
        r.DescuentoTotal.Should().Be(11.30m);
    }

    [Fact]
    public void ItemExento_NoSumaNiExtraeIva()
    {
        var r = PrecioIvaCalculator.CalcularLinea(
            new(1m, 100m, 0m, false, TipoPrecioCodigos.IvaExcluido), Iva);

        r.Subtotal.Should().Be(100m);
        r.Iva.Should().Be(0m);
        r.Total.Should().Be(100m);
    }

    [Fact]
    public void NormalizarFacturaYCCF_ConservaTotalComercial()
    {
        var factura = DtePrecioNormalizer.Normalizar(
            TipoDteCodigos.FacturaConsumidorFinal, 100m, 0m,
            TipoPrecioCodigos.IvaExcluido, "GRAVADA", false);
        var ccf = DtePrecioNormalizer.Normalizar(
            TipoDteCodigos.ComprobanteCreditoFiscal, 113m, 11.30m,
            TipoPrecioCodigos.IvaIncluido, "GRAVADA", false);

        factura.PrecioUnitario.Should().Be(113m);
        factura.TipoPrecio.Should().Be(TipoPrecioCodigos.IvaIncluido);
        ccf.PrecioUnitario.Should().Be(100m);
        ccf.MontoDescuento.Should().Be(10m);
        ccf.TipoPrecio.Should().Be(TipoPrecioCodigos.IvaExcluido);
    }

    [Fact]
    public void Normalizar_SinTipoExplicito_PreservaContratoDteHistorico()
    {
        var r = DtePrecioNormalizer.Normalizar(
            TipoDteCodigos.ComprobanteCreditoFiscal, 100m, 5m, null, "GRAVADA", false);

        r.PrecioUnitario.Should().Be(100m);
        r.MontoDescuento.Should().Be(5m);
        r.TipoPrecio.Should().BeNull();
    }

    [Fact]
    public void Dte_Exenta_NoGeneraIva()
    {
        var doc = new DteDocumento { TipoDteCodigo = TipoDteCodigos.ComprobanteCreditoFiscal };
        doc.Detalles.Add(new DteDocumentoDetalle
        {
            NumeroLinea = 1,
            Codigo = "EX-1",
            Descripcion = "Servicio exento",
            Cantidad = 1m,
            PrecioUnitario = 100m,
            Clasificacion = "EXENTA",
        });

        new DteCalculator().Recalcular(doc);

        doc.TotalExenta.Should().Be(100m);
        doc.TotalGravada.Should().Be(0m);
        doc.IvaTotal.Should().Be(0m);
        doc.TotalPagar.Should().Be(100m);
    }
}
