using NeoSTP.Application.Pos;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NeoSTP.Infrastructure.Pos;

/// <summary>Ticket de venta en PDF con formato térmico (58/80mm), monoespaciado.</summary>
public class TicketPdfService : ITicketPdfService
{
    private const string Ink = "#111111";
    private const string Muted = "#555555";

    static TicketPdfService() => QuestPDF.Settings.License = LicenseType.Community;

    public byte[] GenerarTicket(TicketModel t) => Document.Create(container =>
    {
        // Ancho del rollo en puntos (1mm ≈ 2.834 pt). Alto dinámico (rollo continuo).
        var anchoPt = t.AnchoMm * 2.834f;
        container.Page(page =>
        {
            page.ContinuousSize(anchoPt, Unit.Point);
            page.MarginHorizontal(6);
            page.MarginVertical(8);
            page.DefaultTextStyle(x => x.FontFamily(Fonts.Consolas).FontSize(8).FontColor(Ink));
            page.Content().Element(c => Body(c, t));
        });
    }).GeneratePdf();

    private static void Body(IContainer container, TicketModel t)
    {
        var money = t.MonedaSimbolo;
        container.Column(col =>
        {
            col.Spacing(2);

            if (t.LogoPng is { Length: > 0 })
                col.Item().AlignCenter().MaxWidth(120).Image(t.LogoPng).FitWidth();

            col.Item().AlignCenter().Text(t.EmpresaNombre).Bold().FontSize(10);
            if (!string.IsNullOrWhiteSpace(t.EmpresaNit)) col.Item().AlignCenter().Text($"NIT: {t.EmpresaNit}").FontColor(Muted);
            if (!string.IsNullOrWhiteSpace(t.EmpresaNrc)) col.Item().AlignCenter().Text($"NRC: {t.EmpresaNrc}").FontColor(Muted);
            if (!string.IsNullOrWhiteSpace(t.Direccion)) col.Item().AlignCenter().Text(t.Direccion!).FontColor(Muted);
            if (!string.IsNullOrWhiteSpace(t.Telefono)) col.Item().AlignCenter().Text($"Tel: {t.Telefono}").FontColor(Muted);

            col.Item().PaddingVertical(2).LineHorizontal(0.5f);

            col.Item().Text($"Ticket: {t.Numero}").Bold();
            col.Item().Text($"Fecha: {t.Fecha.ToLocalTime():dd/MM/yyyy HH:mm}");
            col.Item().Text($"Cliente: {t.ClienteNombre}");
            if (t.EstadoCodigo == "ANULADA") col.Item().AlignCenter().Text("*** ANULADA ***").Bold();

            col.Item().PaddingVertical(2).LineHorizontal(0.5f);

            // Cabecera de columnas
            col.Item().Row(r =>
            {
                r.RelativeItem(5).Text("Descripción").Bold();
                r.RelativeItem(3).AlignRight().Text("Total").Bold();
            });

            foreach (var l in t.Lineas)
            {
                col.Item().Row(r =>
                {
                    r.RelativeItem(5).Text($"{l.Descripcion}");
                    r.RelativeItem(3).AlignRight().Text($"{money}{l.Total:N2}");
                });
                col.Item().Text($"  {l.Cantidad:0.##} x {money}{l.PrecioUnitario:N2}").FontColor(Muted).FontSize(7);
            }

            col.Item().PaddingVertical(2).LineHorizontal(0.5f);

            void Tot(string label, decimal val, bool bold = false)
                => col.Item().Row(r =>
                {
                    var left = r.RelativeItem().Text(label);
                    var right = r.ConstantItem(70).AlignRight().Text($"{money}{val:N2}");
                    if (bold) { left.Bold(); right.Bold(); }
                });

            if (t.TotalDescuento > 0) Tot("Descuento", t.TotalDescuento);
            Tot("Subtotal", t.Subtotal);
            Tot("IVA", t.IvaTotal);
            col.Item().PaddingTop(1);
            Tot("TOTAL", t.Total, bold: true);

            col.Item().PaddingTop(2).Text($"Forma de pago: {t.FormaPago}");
            if (t.EfectivoRecibido is decimal rec) { Tot("Efectivo", rec); Tot("Cambio", t.Cambio ?? 0m); }

            if (!string.IsNullOrWhiteSpace(t.Nota))
            {
                col.Item().PaddingVertical(2).LineHorizontal(0.5f);
                col.Item().Text(t.Nota!).Italic().FontColor(Muted);
            }

            col.Item().PaddingVertical(2).LineHorizontal(0.5f);
            col.Item().AlignCenter().Text(t.PieTicket).Bold();
            col.Item().AlignCenter().Text("Comprobante no fiscal").FontSize(7).FontColor(Muted);
        });
    }
}
