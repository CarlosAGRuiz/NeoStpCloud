using NeoSTP.Application.Rrhh;
using NeoSTP.Application.Rrhh.Dtos;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NeoSTP.Infrastructure.Rrhh;

/// <summary>Recibo/boleta de pago de nómina en PDF (QuestPDF), alineado al design system.</summary>
public class NominaPdfService : INominaPdfService
{
    private const string Primary = "#131B2E";
    private const string Secondary = "#6B38D4";
    private const string Ink = "#1E293B";
    private const string Muted = "#64748B";
    private const string Line = "#E2E8F0";
    private const string SoftBg = "#F1F5F9";

    static NominaPdfService() => QuestPDF.Settings.License = LicenseType.Community;

    public byte[] GenerarRecibo(ReciboNominaModel r) => Document.Create(container =>
    {
        container.Page(page =>
        {
            page.Size(PageSizes.Letter);
            page.MarginVertical(36);
            page.MarginHorizontal(40);
            page.DefaultTextStyle(x => x.FontSize(10).FontColor(Ink));
            page.Content().Element(c => Body(c, r));
            page.Footer().Text($"Generado por NeoSTP · {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(8).FontColor(Muted);
        });
    }).GeneratePdf();

    private static void Body(IContainer container, ReciboNominaModel r)
    {
        container.Column(col =>
        {
            // Encabezado
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(r.EmpresaNombre).FontSize(14).Bold().FontColor(Primary);
                    c.Item().Text("Recibo de pago de nómina").FontSize(10).FontColor(Muted);
                });
                row.ConstantItem(170).AlignRight().Column(c =>
                {
                    c.Item().Text($"Período {r.PeriodoEtiqueta}").Bold();
                    c.Item().Text($"{r.FechaInicio:dd/MM/yyyy} – {r.FechaFin:dd/MM/yyyy}").FontSize(9).FontColor(Muted);
                    c.Item().Text(r.EstadoCodigo).FontSize(8).FontColor(Secondary);
                });
            });

            col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Line);

            // Empleado
            col.Item().PaddingTop(10).Background(SoftBg).Padding(10).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text($"Empleado: {r.EmpleadoNombre}").Bold();
                    c.Item().Text($"Código: {r.EmpleadoCodigo}").FontSize(9).FontColor(Muted);
                    if (!string.IsNullOrWhiteSpace(r.Cargo)) c.Item().Text($"Cargo: {r.Cargo}").FontSize(9).FontColor(Muted);
                });
                row.RelativeItem().AlignRight().Column(c =>
                {
                    if (!string.IsNullOrWhiteSpace(r.IsssNumero)) c.Item().Text($"ISSS: {r.IsssNumero}").FontSize(9).FontColor(Muted);
                    if (!string.IsNullOrWhiteSpace(r.AfpInstitucion) || !string.IsNullOrWhiteSpace(r.AfpNumero))
                        c.Item().Text($"AFP: {r.AfpInstitucion} {r.AfpNumero}".Trim()).FontSize(9).FontColor(Muted);
                    c.Item().Text($"Salario mensual: $ {r.SalarioMensual:N2}").FontSize(9).FontColor(Muted);
                });
            });

            // Devengos / deducciones
            col.Item().PaddingTop(14).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Devengos").Bold().FontColor(Primary);
                    Linea(c, "Salario ordinario", r.Devengado - r.OtrosIngresos);
                    if (r.PrimaVacacion > 0) Linea(c, "Prima de vacaciones", r.PrimaVacacion);
                    if (r.Aguinaldo > 0) Linea(c, "Aguinaldo", r.Aguinaldo);
                    var otros = r.OtrosIngresos - r.PrimaVacacion - r.Aguinaldo;
                    if (otros > 0) Linea(c, "Otros ingresos", otros);
                    c.Item().PaddingTop(2).LineHorizontal(0.5f).LineColor(Line);
                    Linea(c, "Total devengado", r.Devengado, bold: true);
                });
                row.ConstantItem(20);
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Deducciones").Bold().FontColor(Primary);
                    Linea(c, "ISSS", r.Isss);
                    Linea(c, "AFP", r.Afp);
                    Linea(c, "Renta (ISR)", r.Renta);
                    if (r.OtrosDescuentos > 0) Linea(c, "Otros descuentos", r.OtrosDescuentos);
                    c.Item().PaddingTop(2).LineHorizontal(0.5f).LineColor(Line);
                    Linea(c, "Total deducciones", r.TotalDeducciones, bold: true);
                });
            });

            // Neto
            col.Item().PaddingTop(16).Background(Primary).Padding(12).Row(row =>
            {
                row.RelativeItem().Text("LÍQUIDO A PAGAR").FontColor("#FFFFFF").Bold();
                row.ConstantItem(140).AlignRight().Text($"$ {r.SalarioNeto:N2}").FontColor("#FFFFFF").FontSize(14).Bold();
            });

            col.Item().PaddingTop(24).Row(row =>
            {
                row.RelativeItem().AlignCenter().Column(c =>
                {
                    c.Item().PaddingTop(20).LineHorizontal(0.7f).LineColor(Ink);
                    c.Item().AlignCenter().Text("Recibí conforme").FontSize(9).FontColor(Muted);
                });
                row.ConstantItem(60);
                row.RelativeItem().AlignCenter().Column(c =>
                {
                    c.Item().PaddingTop(20).LineHorizontal(0.7f).LineColor(Ink);
                    c.Item().AlignCenter().Text("Entregado por").FontSize(9).FontColor(Muted);
                });
            });
        });
    }

    private static void Linea(ColumnDescriptor c, string label, decimal monto, bool bold = false)
        => c.Item().PaddingTop(3).Row(row =>
        {
            var lbl = row.RelativeItem().Text(label).FontSize(9);
            var val = row.ConstantItem(90).AlignRight().Text($"{monto:N2}").FontSize(9);
            if (bold) { lbl.Bold(); val.Bold(); }
        });
}
