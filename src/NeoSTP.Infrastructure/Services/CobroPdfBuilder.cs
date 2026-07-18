using NeoSTP.Application.Cobranza;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NeoSTP.Infrastructure.Services;

public sealed class CobroPdfModel
{
    public string EmpresaNombre { get; set; } = string.Empty;
    public byte[]? LogoPng { get; set; }
    public CobroQrDto Cobro { get; set; } = null!;
}

/// <summary>
/// Solicitud de cobro en PDF (media carta): branding, monto, datos de cuenta y QR.
/// Pensada para adjuntarse a un correo o compartirse por WhatsApp.
/// </summary>
public static class CobroPdfBuilder
{
    private const string Ink = "#111111";
    private const string Muted = "#555555";
    private const string Accent = "#0d6efd";

    static CobroPdfBuilder() => QuestPDF.Settings.License = LicenseType.Community;

    public static byte[] Generar(CobroPdfModel m) => Document.Create(container =>
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A5);
            page.Margin(28);
            page.DefaultTextStyle(x => x.FontSize(10).FontColor(Ink));
            page.Content().Element(c => Body(c, m));
        });
    }).GeneratePdf();

    private static void Body(IContainer container, CobroPdfModel m)
    {
        var c = m.Cobro;
        container.Column(col =>
        {
            col.Spacing(6);

            col.Item().Row(r =>
            {
                if (m.LogoPng is { Length: > 0 })
                    r.ConstantItem(64).MaxHeight(48).Image(m.LogoPng).FitArea();
                r.RelativeItem().AlignMiddle().Column(h =>
                {
                    h.Item().Text(m.EmpresaNombre).Bold().FontSize(14);
                    h.Item().Text("Solicitud de cobro").FontColor(Muted);
                });
            });

            col.Item().LineHorizontal(0.8f);

            col.Item().Row(r =>
            {
                r.RelativeItem().Column(info =>
                {
                    info.Spacing(2);
                    info.Item().Text(t => { t.Span("Referencia: ").FontColor(Muted); t.Span(c.Referencia).Bold(); });
                    info.Item().Text(t => { t.Span("Fecha: ").FontColor(Muted); t.Span($"{DateTime.Now:dd/MM/yyyy}"); });
                    info.Item().PaddingTop(6).Text("Monto a pagar").FontColor(Muted);
                    info.Item().Text($"$ {c.Monto:N2}").Bold().FontSize(22).FontColor(Accent);

                    if (c.EsLink)
                    {
                        info.Item().PaddingTop(6).Text("Pague en línea (tarjeta):").FontColor(Muted);
                        info.Item().Hyperlink(c.Payload).Text(c.Payload).FontSize(8).FontColor(Accent).Underline();
                    }
                    else
                    {
                        info.Item().PaddingTop(6).Text("Datos para transferencia:").FontColor(Muted);
                        if (!string.IsNullOrWhiteSpace(c.Banco)) info.Item().Text($"Banco: {c.Banco}");
                        if (!string.IsNullOrWhiteSpace(c.NumeroCuenta)) info.Item().Text($"Cuenta: {c.NumeroCuenta}");
                        if (!string.IsNullOrWhiteSpace(c.Titular)) info.Item().Text($"Titular: {c.Titular}");
                    }
                });

                r.ConstantItem(150).AlignTop().Column(qc =>
                {
                    qc.Item().Border(0.8f).Padding(6).Image(Convert.FromBase64String(c.QrPngBase64)).FitWidth();
                    qc.Item().AlignCenter().Text(c.EsLink ? "Escanee para pagar" : "Escanee para ver los datos")
                        .FontSize(8).FontColor(Muted);
                });
            });

            col.Item().PaddingTop(8).LineHorizontal(0.8f);
            col.Item().Text($"Al completar el pago, indique la referencia {c.Referencia} para aplicarlo a su cuenta.")
                .FontSize(8).FontColor(Muted);
        });
    }
}
