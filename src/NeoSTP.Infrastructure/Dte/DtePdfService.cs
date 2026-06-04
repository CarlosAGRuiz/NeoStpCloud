using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Domain.Core.Dte;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NeoSTP.Infrastructure.Dte;

/// <summary>
/// Renderiza la representación gráfica del DTE a PDF usando QuestPDF.
/// Diseño alineado al design system de NeoSTP (Deep Tech Blue + Modern Violet).
/// Todo el cuerpo va dentro de page.Content() (que respeta los márgenes de forma
/// consistente); el componente Table se sustituye por filas manuales para garantizar
/// que el contenido nunca se desborde del área imprimible.
/// </summary>
public class DtePdfService : IDtePdfService
{
    // ── Paleta (design tokens NeoSTP) ─────────────────────────────────────────
    private const string Primary = "#131B2E";   // Deep Tech Blue
    private const string Secondary = "#6B38D4";  // Modern Violet
    private const string Ink = "#1E293B";
    private const string Muted = "#64748B";
    private const string Line = "#E2E8F0";
    private const string SoftBg = "#F1F5F9";
    private const string Zebra = "#F8FAFC";
    private const float Margin = 32f;

    // Pesos de las columnas del detalle (#, Código, Descripción, Cant, Precio, Desc, Total)
    private const float WNum = 0.6f, WCod = 1.7f, WDesc = 5.0f, WCant = 1.3f, WPrecio = 1.7f, WDescto = 1.3f, WTotal = 1.7f;

    static DtePdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Generar(DteDocumento d) => BuildDocument(d).GeneratePdf();

    /// <summary>Rasteriza la primera página a PNG (solo para QA visual del diseño).</summary>
    internal static byte[] GenerarImagenPrimeraPagina(DteDocumento d)
        => BuildDocument(d).GenerateImages().First();

    private static QuestPDF.Infrastructure.IDocument BuildDocument(DteDocumento d)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.MarginVertical(30);
                page.MarginHorizontal(Margin);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(Ink));

                page.Content().Element(c => Body(c, d));
                // El Footer no aplica el margen derecho como el Content; se compensa.
                page.Footer().PaddingRight(Margin).Element(c => Footer(c, d));
            });
        });
    }

    // ---------- Cuerpo completo ----------

    private static void Body(IContainer container, DteDocumento d)
    {
        container.Column(col =>
        {
            col.Item().Element(c => Encabezado(c, d));
            col.Item().PaddingTop(12).Element(c => Receptor(c, d));
            col.Item().PaddingTop(12).Element(c => Detalle(c, d));
            col.Item().PaddingTop(10).Element(c => Totales(c, d));
        });
    }

    // ---------- Encabezado (banda + identificadores) ----------

    private static void Encabezado(IContainer container, DteDocumento d)
    {
        var emisor = d.Empresa;

        container.Column(outer =>
        {
            // Banda de marca
            outer.Item().Background(Primary).Padding(14).Row(row =>
            {
                row.RelativeItem(2.4f).PaddingRight(10).Column(col =>
                {
                    col.Item().Text(emisor?.RazonSocial ?? "Empresa emisora")
                        .FontSize(13).Bold().FontColor("#FFFFFF");
                    if (!string.IsNullOrEmpty(emisor?.NombreComercial))
                        col.Item().Text(emisor!.NombreComercial).FontSize(8.5f).FontColor("#C7D2FE");
                    col.Item().PaddingTop(3).Text($"NIT {emisor?.Nit ?? "—"}   ·   NRC {emisor?.Nrc ?? "—"}")
                        .FontSize(7.5f).FontColor("#CBD5E1");
                    if (!string.IsNullOrEmpty(emisor?.ActividadEconomica))
                        col.Item().Text($"{emisor?.CodigoActividad} · {emisor?.ActividadEconomica}")
                            .FontSize(7.5f).FontColor("#CBD5E1");
                });

                row.RelativeItem(1.4f).Column(col =>
                {
                    col.Item().Text(t =>
                    {
                        t.AlignRight();
                        t.Span(TipoNombre(d.TipoDteCodigo)).FontSize(9.5f).Bold().FontColor("#FFFFFF");
                    });
                    col.Item().Text(t =>
                    {
                        t.AlignRight();
                        t.Span($"v{d.VersionDte} · {AmbienteNombre(d.AmbienteCodigo)}").FontSize(7).FontColor("#A5B4FC");
                    });
                    col.Item().PaddingTop(6).AlignRight().Element(c => EstadoBadge(c, d.EstadoCodigo));
                });
            });

            // Franja de identificadores
            outer.Item().Background(SoftBg).BorderBottom(1).BorderColor(Line).Padding(10).Row(row =>
            {
                IdField(row, "Número de control", d.NumeroControl, mono: true);
                IdField(row, "Código de generación", d.CodigoGeneracion, mono: true);
                IdField(row, "Emisión", $"{d.FechaEmision:dd/MM/yyyy} {d.HoraEmision:hh\\:mm}");
            });

            if (!string.IsNullOrEmpty(d.SelloRecibido))
            {
                outer.Item().Background(SoftBg).PaddingHorizontal(10).PaddingBottom(8).Column(c =>
                {
                    c.Item().Text("Sello de recepción (Hacienda)").FontSize(6.5f).FontColor(Muted);
                    c.Item().Text(d.SelloRecibido).FontSize(7).FontFamily(Fonts.Consolas).FontColor(Ink);
                });
            }
        });
    }

    private static void IdField(RowDescriptor row, string label, string? value, bool mono = false)
    {
        row.RelativeItem().PaddingRight(8).Column(c =>
        {
            c.Item().Text(label.ToUpperInvariant()).FontSize(6.5f).FontColor(Muted);
            var t = c.Item().Text(value ?? "—").FontSize(mono ? 7.5f : 8.5f).FontColor(Ink);
            if (mono) t.FontFamily(Fonts.Consolas);
        });
    }

    private static void EstadoBadge(IContainer container, string estado)
    {
        var (bg, fg) = estado switch
        {
            DteEstadoCodigos.Procesado => ("#DCFCE7", "#15803D"),
            DteEstadoCodigos.Rechazado => ("#FEE2E2", "#B91C1C"),
            DteEstadoCodigos.Contingencia => ("#FEF3C7", "#B45309"),
            DteEstadoCodigos.Invalidado => ("#E2E8F0", "#334155"),
            _ => ("#E0E7FF", "#3730A3"),
        };
        container.Background(bg).PaddingVertical(3).PaddingHorizontal(8)
            .Text(estado).FontSize(8).Bold().FontColor(fg);
    }

    // ---------- Receptor ----------

    private static void Receptor(IContainer container, DteDocumento d)
    {
        container.Border(1).BorderColor(Line).Background("#FFFFFF").Padding(10).Column(card =>
        {
            card.Item().Text("RECEPTOR").FontSize(7.5f).Bold().FontColor(Secondary);
            card.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(d.ReceptorNombre ?? "Consumidor final").FontSize(10).SemiBold();
                    c.Item().Text($"{d.ReceptorTipoDocumento}: {d.ReceptorNumeroDocumento ?? "—"}   ·   NRC: {d.ReceptorNrc ?? "—"}").FontColor(Muted);
                    if (!string.IsNullOrEmpty(d.ReceptorActividadEconomica))
                        c.Item().Text($"{d.ReceptorCodigoActividad} · {d.ReceptorActividadEconomica}").FontColor(Muted);
                    if (!string.IsNullOrEmpty(d.ReceptorDireccion))
                        c.Item().Text(d.ReceptorDireccion).FontColor(Muted);
                });
                row.ConstantItem(190).Column(c =>
                {
                    c.Item().Text($"Correo: {d.ReceptorCorreo ?? "—"}").FontColor(Muted);
                    c.Item().Text($"Tel: {d.ReceptorTelefono ?? "—"}").FontColor(Muted);
                    c.Item().Text($"Condición: {CondicionNombre(d.CondicionOperacionCodigo)}").FontColor(Muted);
                    if (!string.IsNullOrEmpty(d.NumeroDocumentoRelacionado))
                        c.Item().Text($"Doc. rel.: {d.TipoDteRelacionado}/{d.NumeroDocumentoRelacionado}").FontColor(Muted);
                });
            });
        });
    }

    // ---------- Detalle (tabla manual basada en filas) ----------

    private static void Detalle(IContainer container, DteDocumento d)
    {
        container.Column(col =>
        {
            // Encabezado de columnas
            col.Item().Background(Primary).Row(row =>
            {
                HeadCell(row, WNum, "#");
                HeadCell(row, WCod, "Código");
                HeadCell(row, WDesc, "Descripción");
                HeadCell(row, WCant, "Cant.", right: true);
                HeadCell(row, WPrecio, "Precio U.", right: true);
                HeadCell(row, WDescto, "Desc.", right: true);
                HeadCell(row, WTotal, "Total", right: true);
            });

            var i = 0;
            foreach (var l in d.Detalles.OrderBy(x => x.NumeroLinea))
            {
                var bg = (i++ % 2 == 0) ? "#FFFFFF" : Zebra;
                col.Item().Background(bg).BorderBottom(0.5f).BorderColor(Line).Row(row =>
                {
                    Cell(row, WNum, l.NumeroLinea.ToString());
                    Cell(row, WCod, l.Codigo, mono: true);
                    Cell(row, WDesc, l.Descripcion);
                    Cell(row, WCant, l.Cantidad.ToString("N2"), right: true);
                    Cell(row, WPrecio, l.PrecioUnitario.ToString("N4"), right: true);
                    Cell(row, WDescto, l.MontoDescuento.ToString("N2"), right: true);
                    Cell(row, WTotal, (l.VentaGravada + l.VentaExenta + l.VentaNoSujeta).ToString("N2"), right: true, semi: true);
                });
            }
        });
    }

    // ---------- Totales ----------

    private static void Totales(IContainer container, DteDocumento d)
    {
        container.Row(row =>
        {
            row.RelativeItem(3f).PaddingRight(12).Column(c =>
            {
                if (!string.IsNullOrEmpty(d.TotalLetras))
                {
                    c.Item().Text("VALOR EN LETRAS").FontSize(6.5f).Bold().FontColor(Muted);
                    c.Item().Text(d.TotalLetras).Italic().FontColor(Ink);
                }
                if (!string.IsNullOrEmpty(d.Observaciones))
                {
                    c.Item().PaddingTop(8).Text("OBSERVACIONES").FontSize(6.5f).Bold().FontColor(Muted);
                    c.Item().Text(d.Observaciones).FontColor(Muted);
                }
            });

            row.RelativeItem(2.2f).Border(1).BorderColor(Line).Column(box =>
            {
                box.Item().Background(SoftBg).Padding(8).Column(c =>
                {
                    Linea(c, "Total gravada", d.TotalGravada);
                    Linea(c, "Total exenta", d.TotalExenta);
                    Linea(c, "Total no sujeta", d.TotalNoSujeto);
                    Linea(c, "Subtotal", d.SubTotal);
                    Linea(c, "IVA 13%", d.IvaTotal);
                    if (d.IvaRetenido > 0) Linea(c, "IVA retenido", d.IvaRetenido);
                    if (d.ReteRenta > 0) Linea(c, "Renta retenida", d.ReteRenta);
                });
                box.Item().Background(Primary).Padding(8).Row(r =>
                {
                    r.RelativeItem().Text(t => { t.Span("TOTAL A PAGAR").FontSize(9).Bold().FontColor("#FFFFFF"); });
                    r.RelativeItem().Text(t => { t.AlignRight(); t.Span($"$ {d.TotalPagar:N2}").FontSize(11).Bold().FontColor("#FFFFFF"); });
                });
            });
        });
    }

    private static void Footer(IContainer container, DteDocumento d)
    {
        container.PaddingTop(8).BorderTop(1).BorderColor(Line).PaddingTop(6).Row(row =>
        {
            if (!string.IsNullOrEmpty(d.CodigoGeneracion))
                row.ConstantItem(54).Image(GenerarQr(d.CodigoGeneracion));
            else
                row.ConstantItem(54);

            row.RelativeItem().PaddingLeft(8).AlignMiddle().Column(c =>
            {
                c.Item().Text(t =>
                {
                    t.Span("Documento Tributario Electrónico").FontSize(7.5f).SemiBold().FontColor(Ink);
                    t.Span("  ·  generado por NeoSTP Cloud").FontSize(7.5f).FontColor(Muted);
                });
                c.Item().Text("Consulte la validez de este documento en el portal del Ministerio de Hacienda de El Salvador.")
                    .FontSize(6.5f).FontColor(Muted);
            });

            row.ConstantItem(120).AlignRight().AlignBottom().Text(t =>
            {
                t.Span($"{d.FechaEmision:dd/MM/yyyy}  ·  Pág. ").FontSize(7).FontColor(Muted);
                t.CurrentPageNumber().FontSize(7).FontColor(Muted);
                t.Span("/").FontSize(7).FontColor(Muted);
                t.TotalPages().FontSize(7).FontColor(Muted);
            });
        });
    }

    /// <summary>Genera un PNG del QR con el contenido dado usando QRCoder.</summary>
    private static byte[] GenerarQr(string contenido)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(contenido, QRCodeGenerator.ECCLevel.M);
        using var qr = new PngByteQRCode(data);
        return qr.GetGraphic(4);
    }

    // ---------- helpers ----------

    private static void HeadCell(RowDescriptor row, float weight, string text, bool right = false)
    {
        row.RelativeItem(weight).PaddingVertical(5).PaddingHorizontal(5).Text(t =>
        {
            if (right) t.AlignRight();
            t.Span(text).Bold().FontSize(8).FontColor("#FFFFFF");
        });
    }

    private static void Cell(RowDescriptor row, float weight, string text, bool right = false, bool semi = false, bool mono = false)
    {
        row.RelativeItem(weight).PaddingVertical(3).PaddingHorizontal(5).Text(t =>
        {
            if (right) t.AlignRight();
            var span = t.Span(text).FontSize(8).FontColor(Ink);
            if (semi) span.SemiBold();
            if (mono) span.FontFamily(Fonts.Consolas);
        });
    }

    private static void Linea(ColumnDescriptor c, string label, decimal value)
    {
        c.Item().PaddingVertical(1).Row(r =>
        {
            r.RelativeItem().Text(label).FontSize(8).FontColor(Muted);
            r.RelativeItem().Text(t => { t.AlignRight(); t.Span($"$ {value:N2}").FontSize(8).FontColor(Ink); });
        });
    }

    private static string AmbienteNombre(string codigo) => codigo switch
    {
        "PRODUCCION" => "Producción",
        "PRUEBAS" => "Pruebas",
        _ => codigo,
    };

    private static string TipoNombre(string codigo) => codigo switch
    {
        TipoDteCodigos.FacturaConsumidorFinal => "FACTURA · DTE-01",
        TipoDteCodigos.ComprobanteCreditoFiscal => "COMPROBANTE DE CRÉDITO FISCAL · DTE-03",
        TipoDteCodigos.NotaCredito => "NOTA DE CRÉDITO · DTE-05",
        TipoDteCodigos.NotaDebito => "NOTA DE DÉBITO · DTE-06",
        TipoDteCodigos.FacturaSujetoExcluido => "SUJETO EXCLUIDO · DTE-14",
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
