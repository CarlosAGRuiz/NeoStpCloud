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

    [Fact]
    public void Generar_MuestraRica_ParaRevisionVisual()
    {
        var d = new DteDocumento
        {
            TipoDteCodigo = TipoDteCodigos.ComprobanteCreditoFiscal,
            VersionDte = 3, AmbienteCodigo = "PRUEBAS",
            NumeroControl = "DTE-03-00010001-000000000000123",
            CodigoGeneracion = "B5F1C2A3-9D4E-4F6A-8B7C-1234567890AB",
            SelloRecibido = "2025ABCD1234EF5678901234567890ABCDEF1234",
            FechaEmision = new DateTime(2026, 6, 3), HoraEmision = new TimeSpan(10, 35, 0),
            EstadoCodigo = DteEstadoCodigos.Procesado, CondicionOperacionCodigo = "1",
            ReceptorNombre = "Comercial Los Andes, S.A. de C.V.",
            ReceptorTipoDocumento = "36", ReceptorNumeroDocumento = "0614-050505-102-3", ReceptorNrc = "987654-3",
            ReceptorCodigoActividad = "46900", ReceptorActividadEconomica = "Venta al por mayor",
            ReceptorDireccion = "Santa Tecla, La Libertad", ReceptorCorreo = "compras@losandes.com.sv",
            ReceptorTelefono = "2233-4455",
            Observaciones = "Entrega en bodega central. Pago a 30 días.",
            Empresa = new Empresa
            {
                Id = 1, RazonSocial = "Distribuidora El Salvador, S.A. de C.V.", NombreComercial = "DISAL",
                Nit = "0614-010101-001-2", Nrc = "123456-7", CodigoActividad = "47190",
                ActividadEconomica = "Venta al por menor en comercios no especializados",
                Direccion = "Col. Escalón, Av. Las Palmas #123, San Salvador", Telefono = "2222-3333",
                Correo = "ventas@disal.com.sv",
            },
        };
        d.Detalles.Add(new DteDocumentoDetalle { NumeroLinea = 1, Codigo = "PRD-001", Descripcion = "Caja de papel bond carta (10 resmas)", Cantidad = 5m, PrecioUnitario = 45.5000m });
        d.Detalles.Add(new DteDocumentoDetalle { NumeroLinea = 2, Codigo = "PRD-002", Descripcion = "Tóner negro compatible HP 26A", Cantidad = 8m, PrecioUnitario = 62.7500m, MontoDescuento = 10m });
        d.Detalles.Add(new DteDocumentoDetalle { NumeroLinea = 3, Codigo = "SRV-010", Descripcion = "Servicio de instalación y configuración", Cantidad = 1m, PrecioUnitario = 280.50m });
        new DteCalculator().Recalcular(d);

        var pdf = new DtePdfService().Generar(d);
        pdf.Length.Should().BeGreaterThan(2000);

        var dir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tmp");
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, "factura-demo.pdf"), pdf);
            File.WriteAllBytes(Path.Combine(dir, "factura-demo.png"), DtePdfService.GenerarImagenPrimeraPagina(d));
        }
        catch { /* la muestra es best-effort */ }
    }
}
