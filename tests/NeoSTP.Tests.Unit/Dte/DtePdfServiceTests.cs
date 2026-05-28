using System.Text;
using FluentAssertions;
using NeoSTP.Application.Dte;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Dte;
using Xunit;

namespace NeoSTP.Tests.Unit.Dte;

public class DtePdfServiceTests
{
    private static DteDocumento BuildDoc()
    {
        var d = new DteDocumento
        {
            TipoDteCodigo = TipoDteCodigos.ComprobanteCreditoFiscal,
            AmbienteCodigo = "PRUEBAS",
            NumeroControl = "DTE-03-00010001-000000000000001",
            CodigoGeneracion = Guid.NewGuid().ToString().ToUpperInvariant(),
            FechaEmision = new DateTime(2026, 1, 15),
            HoraEmision = new TimeSpan(10, 30, 0),
            CondicionOperacionCodigo = "1",
            ReceptorNombre = "Cliente Demo S.A.",
            ReceptorTipoDocumento = "36",
            ReceptorNumeroDocumento = "06140101001234",
            ReceptorCorreo = "demo@cliente.local",
            SelloRecibido = "ABC123",
            Empresa = new Empresa
            {
                Id = 1,
                Nit = "06140101001234",
                Nrc = "12345",
                RazonSocial = "Empresa Demo S.A. de C.V.",
                CodigoActividad = "47190",
                ActividadEconomica = "Comercio",
                Direccion = "San Salvador",
            },
        };
        d.Detalles.Add(new DteDocumentoDetalle
        {
            NumeroLinea = 1,
            Codigo = "P001",
            Descripcion = "Servicio mensual",
            UnidadMedidaCodigo = "59",
            Cantidad = 1,
            PrecioUnitario = 100m,
        });
        new DteCalculator().Recalcular(d);
        return d;
    }

    [Fact]
    public void Generar_DevuelveBytesPdfNoVacios()
    {
        var pdf = new DtePdfService().Generar(BuildDoc());
        pdf.Should().NotBeNull();
        pdf.Length.Should().BeGreaterThan(1000); // un PDF mínimo pesa al menos algunos KB
    }

    [Fact]
    public void Generar_ProduceSignatureValidaPdf()
    {
        var pdf = new DtePdfService().Generar(BuildDoc());
        // Los PDFs comienzan con "%PDF-"
        Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");
    }

    [Fact]
    public void Generar_DocumentoSinDetalles_NoTira()
    {
        var d = BuildDoc();
        d.Detalles.Clear();
        var pdf = new DtePdfService().Generar(d);
        pdf.Length.Should().BeGreaterThan(500);
    }
}
