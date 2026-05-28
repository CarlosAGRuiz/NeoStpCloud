using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Domain.Core.Dte;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NeoSTP.Infrastructure.Dte;

/// <summary>
/// Renderiza la representación gráfica del DTE a PDF usando QuestPDF.
/// </summary>
public class DtePdfService : IDtePdfService
{
    static DtePdfService()
    {
        // Licencia community-friendly. La biblioteca exige declarar el modo
        // explícitamente al iniciar.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Generar(DteDocumento d)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Element(c => Header(c, d));
                page.Content().Element(c => Content(c, d));
                page.Footer().Element(c => Footer(c, d));
            });
        }).GeneratePdf();
    }

    // ---------- Header ----------

    private static void Header(IContainer container, DteDocumento d)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                var emisor = d.Empresa;
                col.Item().Text(emisor?.RazonSocial ?? "Empresa emisora").FontSize(14).Bold();
                if (!string.IsNullOrEmpty(emisor?.NombreComercial))
                    col.Item().Text(emisor!.NombreComercial).FontSize(10);
                col.Item().Text($"NIT: {emisor?.Nit ?? "—"}    NRC: {emisor?.Nrc ?? "—"}");
                col.Item().Text($"Actividad: {emisor?.CodigoActividad} - {emisor?.ActividadEconomica}");
                col.Item().Text($"{emisor?.Direccion}");
                col.Item().Text($"Tel: {emisor?.Telefono}   {emisor?.Correo}");
            });

            row.ConstantItem(220).Column(col =>
            {
                col.Item().AlignRight().Text(TipoNombre(d.TipoDteCodigo)).FontSize(12).Bold();
                col.Item().AlignRight().Text($"v{d.VersionDte}   Ambiente: {d.AmbienteCodigo}").FontSize(8);
                col.Item().PaddingTop(4).Border(0.5f).Padding(4).Column(box =>
                {
                    box.Item().Text("Número de control").FontSize(7).Italic();
                    box.Item().Text(d.NumeroControl).FontSize(8).FontFamily(Fonts.Consolas);
                    box.Item().PaddingTop(2).Text("Código de generación").FontSize(7).Italic();
                    box.Item().Text(d.CodigoGeneracion).FontSize(8).FontFamily(Fonts.Consolas);
                    if (!string.IsNullOrEmpty(d.SelloRecibido))
                    {
                        box.Item().PaddingTop(2).Text("Sello recibido").FontSize(7).Italic();
                        box.Item().Text(d.SelloRecibido).FontSize(7).FontFamily(Fonts.Consolas);
                    }
                });
                col.Item().PaddingTop(4).AlignRight()
                   .Text($"Emisión: {d.FechaEmision:yyyy-MM-dd} {d.HoraEmision:hh\\:mm}").FontSize(8);
                col.Item().AlignRight().Text($"Estado: {d.EstadoCodigo}").FontSize(8).SemiBold();
            });
        });
    }

    // ---------- Content ----------

    private static void Content(IContainer container, DteDocumento d)
    {
        container.PaddingVertical(8).Column(col =>
        {
            // Receptor
            col.Item().BorderBottom(1).PaddingBottom(2).Text("Receptor").Bold();
            col.Item().PaddingVertical(4).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(d.ReceptorNombre ?? "—").SemiBold();
                    c.Item().Text($"{d.ReceptorTipoDocumento}: {d.ReceptorNumeroDocumento}   NRC: {d.ReceptorNrc ?? "—"}");
                    c.Item().Text($"Actividad: {d.ReceptorCodigoActividad} - {d.ReceptorActividadEconomica}");
                    c.Item().Text($"{d.ReceptorDireccion}");
                });
                row.ConstantItem(180).Column(c =>
                {
                    c.Item().Text($"Correo: {d.ReceptorCorreo}");
                    c.Item().Text($"Tel: {d.ReceptorTelefono}");
                    c.Item().Text($"Condición: {CondicionNombre(d.CondicionOperacionCodigo)}");
                    if (!string.IsNullOrEmpty(d.NumeroDocumentoRelacionado))
                        c.Item().Text($"Doc. relacionado: {d.TipoDteRelacionado}/{d.NumeroDocumentoRelacionado}");
                });
            });

            // Detalle
            col.Item().PaddingTop(8).BorderBottom(1).PaddingBottom(2).Text("Detalle").Bold();
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(25);
                    c.ConstantColumn(60);
                    c.RelativeColumn();
                    c.ConstantColumn(45);
                    c.ConstantColumn(60);
                    c.ConstantColumn(50);
                    c.ConstantColumn(60);
                });

                t.Header(h =>
                {
                    HeadCell(h, "#"); HeadCell(h, "Código"); HeadCell(h, "Descripción");
                    HeadCell(h, "Cant.", right: true); HeadCell(h, "Precio U.", right: true);
                    HeadCell(h, "Desc.", right: true); HeadCell(h, "Total", right: true);
                });

                foreach (var l in d.Detalles.OrderBy(x => x.NumeroLinea))
                {
                    Cell(t, l.NumeroLinea.ToString());
                    Cell(t, l.Codigo);
                    Cell(t, l.Descripcion);
                    Cell(t, l.Cantidad.ToString("N4"), right: true);
                    Cell(t, l.PrecioUnitario.ToString("N4"), right: true);
                    Cell(t, l.MontoDescuento.ToString("N2"), right: true);
                    Cell(t, (l.VentaGravada + l.VentaExenta + l.VentaNoSujeta).ToString("N2"), right: true);
                }
            });

            // Totales
            col.Item().PaddingTop(6).AlignRight().Width(260).Border(0.5f).Padding(8).Column(c =>
            {
                Linea(c, "Total gravada", d.TotalGravada);
                Linea(c, "Total exenta", d.TotalExenta);
                Linea(c, "Total no sujeta", d.TotalNoSujeto);
                Linea(c, "Subtotal", d.SubTotal);
                Linea(c, "IVA 13%", d.IvaTotal);
                if (d.IvaRetenido > 0) Linea(c, "IVA retenido", d.IvaRetenido);
                if (d.ReteRenta > 0) Linea(c, "Renta retenida", d.ReteRenta);
                c.Item().BorderTop(1).PaddingTop(2).Row(r =>
                {
                    r.RelativeItem().Text("Total a pagar").SemiBold();
                    r.ConstantItem(90).AlignRight().Text($"$ {d.TotalPagar:N2}").Bold();
                });
            });

            // Total en letras
            if (!string.IsNullOrEmpty(d.TotalLetras))
                col.Item().PaddingTop(6).Text(d.TotalLetras).Italic();

            // Observaciones
            if (!string.IsNullOrEmpty(d.Observaciones))
            {
                col.Item().PaddingTop(8).BorderBottom(1).PaddingBottom(2).Text("Observaciones").Bold();
                col.Item().PaddingTop(2).Text(d.Observaciones);
            }
        });
    }

    private static void Footer(IContainer container, DteDocumento d)
    {
        container.AlignCenter().Text(t =>
        {
            t.Span("Documento Tributario Electrónico generado por NeoSTP Cloud · ").FontSize(7);
            t.Span($"{d.FechaEmision:yyyy-MM-dd}").FontSize(7);
            t.Span("   ·   Página ").FontSize(7);
            t.CurrentPageNumber().FontSize(7);
            t.Span("/").FontSize(7);
            t.TotalPages().FontSize(7);
        });
    }

    // ---------- helpers ----------

    private static void HeadCell(TableCellDescriptor h, string text, bool right = false)
    {
        var c = h.Cell().Background(Colors.Grey.Lighten3).PaddingVertical(3).PaddingHorizontal(4);
        if (right) c.AlignRight().Text(text).SemiBold().FontSize(8);
        else c.Text(text).SemiBold().FontSize(8);
    }

    private static void Cell(TableDescriptor t, string text, bool right = false)
    {
        var c = t.Cell().BorderBottom(0.25f).PaddingVertical(2).PaddingHorizontal(4);
        if (right) c.AlignRight().Text(text).FontSize(8);
        else c.Text(text).FontSize(8);
    }

    private static void Linea(ColumnDescriptor c, string label, decimal value)
    {
        c.Item().Row(r =>
        {
            r.RelativeItem().Text(label).FontSize(8);
            r.ConstantItem(90).AlignRight().Text($"$ {value:N2}").FontSize(8);
        });
    }

    private static string TipoNombre(string codigo) => codigo switch
    {
        TipoDteCodigos.FacturaConsumidorFinal => "FACTURA (DTE-01)",
        TipoDteCodigos.ComprobanteCreditoFiscal => "COMPROBANTE DE CRÉDITO FISCAL (DTE-03)",
        TipoDteCodigos.NotaCredito => "NOTA DE CRÉDITO (DTE-05)",
        TipoDteCodigos.NotaDebito => "NOTA DE DÉBITO (DTE-06)",
        TipoDteCodigos.FacturaSujetoExcluido => "FACTURA SUJETO EXCLUIDO (DTE-14)",
        _ => $"DTE-{codigo}",
    };

    private static string CondicionNombre(string codigo) => codigo switch
    {
        "1" => "Contado",
        "2" => "Crédito",
        "3" => "Otro",
        _ => codigo,
    };
}
