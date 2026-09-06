using System.Text;
using FluentAssertions;
using NeoSTP.Application.Cobranza;
using NeoSTP.Application.Pos;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Dte;
using NeoSTP.Infrastructure.Pos;
using NeoSTP.Infrastructure.Services;
using NeoSTP.Tests.Unit.Empresas;
using SkiaSharp;

namespace NeoSTP.Tests.Unit.Dte;

public class BrandingPdfSafetyTests
{
    [Fact]
    public void InvalidLegacyLogoAndSignatureFallBackWithoutChangingFiscalValues()
    {
        var document = Document();
        document.Empresa!.LogoBlob = "not-an-image"u8.ToArray();
        document.Empresa.FirmaBlob = [137, 80, 78, 71];
        var total = document.TotalPagar; var json = document.Json?.JsonDte;
        var pdf = new DtePdfService().Generar(document);
        Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");
        pdf.Length.Should().BeGreaterThan(1_000);
        document.TotalPagar.Should().Be(total); document.Json?.JsonDte.Should().Be(json);
        document.Empresa.LogoBlob.Should().NotBeNull("fallback must not mutate the tracked tenant entity");
    }

    [Fact]
    public void ValidLogoAndSignatureRenderNormally()
    {
        var document = Document();
        document.Empresa!.LogoBlob = BrandingImageSafetyTests.Image(SKEncodedImageFormat.Png, 220, 90);
        document.Empresa.FirmaBlob = BrandingImageSafetyTests.Image(SKEncodedImageFormat.Jpeg, 240, 80);
        var pdf = new DtePdfService().Generar(document);
        Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-"); pdf.Length.Should().BeGreaterThan(2_000);
    }

    [Fact]
    public void InvalidLegacyLogoFallsBackInCobroPdfWithoutMutatingAmountOrModel()
    {
        var invalid = "invalid-legacy-logo"u8.ToArray();
        var model = new CobroPdfModel {
            EmpresaNombre = "Empresa sintética QA", LogoPng = invalid,
            Cobro = new CobroQrDto { Monto = 77.25m, Referencia = "COBRO-QA-1", CuentaNombre = "Cuenta QA",
                Banco = "Banco sintético", NumeroCuenta = "0001", Titular = "Titular QA", Payload = "pago sintético",
                QrPngBase64 = Convert.ToBase64String(BrandingImageSafetyTests.Image(SKEncodedImageFormat.Png, 64, 64)) }
        };

        var pdf = CobroPdfBuilder.Generar(model);

        Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");
        model.Cobro.Monto.Should().Be(77.25m); model.Cobro.Referencia.Should().Be("COBRO-QA-1");
        model.LogoPng.Should().BeEquivalentTo(invalid);
    }

    [Fact]
    public void InvalidLegacyLogoFallsBackInTicketPdfWithoutMutatingTotalsOrModel()
    {
        var invalid = "invalid-legacy-logo"u8.ToArray();
        var model = new TicketModel { EmpresaNombre = "Empresa sintética QA", LogoPng = invalid,
            Numero = "TICKET-QA-1", Fecha = new DateTime(2026, 9, 4, 10, 30, 0, DateTimeKind.Utc),
            Subtotal = 50m, IvaTotal = 5.75m, Total = 50m };
        model.Lineas.Add(new TicketLinea { Descripcion = "Producto sintético", Cantidad = 1, PrecioUnitario = 50m, Total = 50m });

        var pdf = new TicketPdfService().GenerarTicket(model);

        Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");
        model.Subtotal.Should().Be(50m); model.IvaTotal.Should().Be(5.75m); model.Total.Should().Be(50m);
        model.LogoPng.Should().BeEquivalentTo(invalid); model.Lineas.Should().ContainSingle();
    }

    internal static DteDocumento Document()
    {
        var document = new DteDocumento { EmpresaId = 20, TipoDteCodigo = "01", AmbienteCodigo = "PRUEBAS",
            EstadoCodigo = DteEstadoCodigos.Procesado, NumeroControl = "DTE-01-M001P001-000000000000001",
            CodigoGeneracion = "00000000-0000-4000-8000-000000000020", FechaEmision = new DateTime(2026, 9, 4),
            ReceptorNombre = "Cliente sintético", TotalGravada = 100, SubTotalVentas = 100, SubTotal = 100,
            MontoTotalOperacion = 100, TotalPagar = 100, IvaTotal = 11.50m,
            Empresa = new Empresa { Id = 20, Nit = "00000000000000", RazonSocial = "Empresa sintética QA", FirmaTexto = "Firma autorizada" },
            Json = new DteDocumentoJson { JsonDte = "{\"synthetic\":true}" } };
        document.Detalles.Add(new DteDocumentoDetalle { NumeroLinea = 1, Codigo = "QA", Descripcion = "Servicio sintético",
            Cantidad = 1, PrecioUnitario = 100, VentaGravada = 100, IvaItem = 11.50m });
        return document;
    }
}
