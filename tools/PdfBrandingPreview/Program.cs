using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Dte;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;
var output = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "output", "pdf"));
Directory.CreateDirectory(output);

var valid = Document();
valid.Empresa!.LogoBlob = QuestPDF.Fluent.Document.Create(container => container.Page(page => {
    page.Size(220, 90); page.Margin(0); page.Content().Background("#6B38D4").AlignCenter().AlignMiddle()
        .Text("NeoSTP").Bold().FontColor(Colors.White).FontSize(26);
})).GenerateImages().Single();
valid.Empresa.LogoContentType = "image/png";

var legacy = Document();
legacy.Empresa!.LogoBlob = "INVALID LEGACY LOGO - SYNTHETIC"u8.ToArray();
legacy.Empresa.LogoContentType = "image/png";
legacy.Empresa.FirmaBlob = [137, 80, 78, 71];

File.WriteAllBytes(Path.Combine(output, "gl1f-branding-valid.pdf"), new DtePdfService().Generar(valid));
File.WriteAllBytes(Path.Combine(output, "gl1f-branding-legacy-fallback.pdf"), new DtePdfService().Generar(legacy));

static DteDocumento Document()
{
    var d = new DteDocumento {
        EmpresaId = 20, TipoDteCodigo = "01", AmbienteCodigo = "PRUEBAS", EstadoCodigo = DteEstadoCodigos.Procesado,
        NumeroControl = "DTE-01-M001P001-000000000000001",
        CodigoGeneracion = "00000000-0000-4000-8000-000000000020",
        SelloRecibido = "SELLO-SINTETICO-PARA-QA-VISUAL", FechaEmision = new DateTime(2026, 9, 4),
        HoraEmision = new TimeSpan(10, 30, 0), ReceptorNombre = "Cliente sintético de prueba",
        ReceptorNumeroDocumento = "00000000-0", ReceptorCorreo = "qa@example.test", TotalGravada = 100,
        SubTotalVentas = 100, SubTotal = 100, MontoTotalOperacion = 100, TotalPagar = 100, IvaTotal = 11.50m,
        Empresa = new Empresa { Id = 20, Nit = "00000000000000", Nrc = "000000-0", RazonSocial = "Empresa sintética NeoSTP",
            NombreComercial = "NeoSTP QA", CodigoActividad = "62010", ActividadEconomica = "Servicios tecnológicos",
            Direccion = "San Salvador", FirmaTexto = "Firma autorizada - QA sintética" }
    };
    d.Detalles.Add(new DteDocumentoDetalle { NumeroLinea = 1, Codigo = "QA-001",
        Descripcion = "Servicio sintético para verificación visual", UnidadMedidaCodigo = "59", Cantidad = 1,
        PrecioUnitario = 100, VentaGravada = 100, IvaItem = 11.50m });
    return d;
}
