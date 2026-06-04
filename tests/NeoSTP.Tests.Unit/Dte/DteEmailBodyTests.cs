using FluentAssertions;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Services;
using Xunit;

namespace NeoSTP.Tests.Unit.Dte;

/// <summary>
/// Verifica el cuerpo HTML del correo de envío de DTE (cuadro de datos + total).
/// También deja una muestra en tmp/email-demo.html para revisión visual.
/// </summary>
public class DteEmailBodyTests
{
    private static DteDocumento Sample()
    {
        var d = new DteDocumento
        {
            TipoDteCodigo = TipoDteCodigos.ComprobanteCreditoFiscal,
            NumeroControl = "DTE-03-00010001-000000000000123",
            CodigoGeneracion = "B5F1C2A3-9D4E-4F6A-8B7C-1234567890AB",
            SelloRecibido = "2025ABCD1234EF5678901234567890ABCDEF1234",
            FechaEmision = new DateTime(2026, 6, 3), HoraEmision = new TimeSpan(10, 35, 0),
            EstadoCodigo = DteEstadoCodigos.Procesado,
            ReceptorNombre = "Comercial Los Andes, S.A. de C.V.",
            TotalPagar = 1130.01m,
            Empresa = new Empresa { Id = 1, RazonSocial = "Distribuidora El Salvador, S.A. de C.V." },
        };
        return d;
    }

    [Fact]
    public void BuildBody_ContieneCuadroDeDatosYTotal()
    {
        var html = DteDocumentosService.BuildBody(Sample(), "Distribuidora El Salvador, S.A. de C.V.");

        html.Should().Contain("DATOS DEL DOCUMENTO");
        html.Should().Contain("DTE-03-00010001-000000000000123");
        html.Should().Contain("B5F1C2A3-9D4E-4F6A-8B7C-1234567890AB");
        html.Should().Contain("Comercial Los Andes");
        html.Should().Contain("TOTAL A PAGAR");
        html.Should().Contain("1,130.01");
        html.Should().Contain("PROCESADO");

        var dir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tmp");
        try { Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir, "email-demo.html"), html); }
        catch { /* muestra best-effort */ }
    }

    [Fact]
    public void BuildBody_SinSello_OmiteLaFila()
    {
        var d = Sample();
        d.SelloRecibido = null;

        var html = DteDocumentosService.BuildBody(d, "Emisor");

        html.Should().NotContain("Sello de recepción");
    }
}
