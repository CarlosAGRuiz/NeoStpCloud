using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Common;
using NeoSTP.Application.Dte.Eventos;
using NeoSTP.Domain.Core.Dte.Eventos;
using NeoSTP.Infrastructure.Persistence;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NeoSTP.Infrastructure.Dte;

/// <summary>
/// Sprint 15.3 — Representación gráfica de un evento DTE (invalidación,
/// contingencia, retorno, operaciones especiales). Sigue el mismo formato
/// QuestPDF que <see cref="DtePdfService"/>: cabecera con datos del emisor,
/// bloque de identificación del evento, sección específica por tipo, tabla
/// de DTE relacionados y la última respuesta de Hacienda al pie.
/// </summary>
public class DteEventoPdfService : IDteEventoPdfService
{
    private readonly NeoStpDbContext _db;

    static DteEventoPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public DteEventoPdfService(NeoStpDbContext db)
    {
        _db = db;
    }

    public async Task<Result<DteEventoPdfDto>> GenerarAsync(int empresaId, int eventoId, CancellationToken ct = default)
    {
        var evento = await _db.DteEventos.AsNoTracking()
            .Include(e => e.Empresa)
            .Include(e => e.Respuestas)
            .Include(e => e.DocumentosRelacionados).ThenInclude(d => d.Documento)
            .FirstOrDefaultAsync(e => e.Id == eventoId && e.EmpresaId == empresaId, ct);
        if (evento is null) return Result<DteEventoPdfDto>.Fail("Evento no encontrado.", "EVENTO_NOT_FOUND");

        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Element(c => Header(c, evento));
                page.Content().Element(c => Content(c, evento));
                page.Footer().Element(c => Footer(c, evento));
            });
        }).GeneratePdf();

        var safeTipo = evento.TipoEventoCodigo.ToLowerInvariant().Replace('_', '-');
        return Result<DteEventoPdfDto>.Ok(new DteEventoPdfDto
        {
            FileName = $"evento-{safeTipo}-{evento.CodigoGeneracion}.pdf",
            Content = bytes,
        });
    }

    // ---------- Header ----------

    private static void Header(IContainer container, DteEvento e)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                var emp = e.Empresa;
                col.Item().Text(emp?.RazonSocial ?? "Empresa emisora").FontSize(14).Bold();
                if (!string.IsNullOrEmpty(emp?.NombreComercial))
                    col.Item().Text(emp!.NombreComercial).FontSize(10);
                col.Item().Text($"NIT: {emp?.Nit ?? "—"}    NRC: {emp?.Nrc ?? "—"}");
                if (!string.IsNullOrEmpty(emp?.Direccion))
                    col.Item().Text(emp!.Direccion);
                col.Item().Text($"Tel: {emp?.Telefono}   {emp?.Correo}");
            });

            row.ConstantItem(230).Column(col =>
            {
                col.Item().AlignRight().Text(TipoNombre(e.TipoEventoCodigo)).FontSize(12).Bold();
                col.Item().AlignRight().Text($"v{e.Version}   Ambiente: {e.AmbienteCodigo}").FontSize(8);
                col.Item().PaddingTop(4).Border(0.5f).Padding(4).Column(box =>
                {
                    box.Item().Text("Código de generación").FontSize(7).Italic();
                    box.Item().Text(e.CodigoGeneracion).FontSize(8).FontFamily(Fonts.Consolas);
                    if (!string.IsNullOrEmpty(e.NumeroControlReferencia))
                    {
                        box.Item().PaddingTop(2).Text("DTE referenciado").FontSize(7).Italic();
                        box.Item().Text(e.NumeroControlReferencia).FontSize(8).FontFamily(Fonts.Consolas);
                    }
                    if (!string.IsNullOrEmpty(e.SelloRecibido))
                    {
                        box.Item().PaddingTop(2).Text("Sello recibido").FontSize(7).Italic();
                        box.Item().Text(e.SelloRecibido).FontSize(7).FontFamily(Fonts.Consolas);
                    }
                });
                col.Item().PaddingTop(4).AlignRight()
                    .Text($"Transmisión: {e.FechaTransmision:yyyy-MM-dd HH:mm}").FontSize(8);
                col.Item().AlignRight().Text($"Estado: {e.EstadoCodigo}")
                    .FontSize(8).SemiBold().FontColor(EstadoColor(e.EstadoCodigo));
            });
        });
    }

    // ---------- Content ----------

    private static void Content(IContainer container, DteEvento e)
    {
        container.PaddingVertical(10).Column(col =>
        {
            // Motivo / descripción libre (si aplica al tipo)
            if (!string.IsNullOrWhiteSpace(e.MotivoLibre))
            {
                col.Item().PaddingTop(6).Element(c =>
                {
                    c.Border(0.5f).Padding(6).Column(box =>
                    {
                        box.Item().Text(MotivoLabel(e.TipoEventoCodigo)).FontSize(8).Italic();
                        box.Item().Text(e.MotivoLibre!).FontSize(9);
                    });
                });
            }

            // Tabla de DTE relacionados
            if (e.DocumentosRelacionados.Count > 0)
            {
                col.Item().PaddingTop(10).Text(RelacionadosLabel(e.TipoEventoCodigo)).FontSize(10).Bold();
                col.Item().PaddingTop(2).Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(40);  // #
                        c.ConstantColumn(60);  // TipoDte
                        c.ConstantColumn(170); // numeroControl
                        c.ConstantColumn(80);  // rol
                        c.RelativeColumn();    // codigoGeneracion
                    });

                    t.Header(h =>
                    {
                        h.Cell().Element(HeaderCell).Text("#");
                        h.Cell().Element(HeaderCell).Text("Tipo");
                        h.Cell().Element(HeaderCell).Text("Número de control");
                        h.Cell().Element(HeaderCell).Text("Rol");
                        h.Cell().Element(HeaderCell).Text("Código de generación");
                    });

                    var i = 1;
                    foreach (var r in e.DocumentosRelacionados)
                    {
                        t.Cell().Element(BodyCell).Text(i++.ToString()).FontSize(8);
                        t.Cell().Element(BodyCell).Text(r.Documento?.TipoDteCodigo ?? "—").FontSize(8);
                        t.Cell().Element(BodyCell)
                            .Text(r.NumeroControlSnapshot ?? r.Documento?.NumeroControl ?? "—")
                            .FontSize(7).FontFamily(Fonts.Consolas);
                        t.Cell().Element(BodyCell).Text(r.RolCodigo).FontSize(8);
                        t.Cell().Element(BodyCell)
                            .Text(r.Documento?.CodigoGeneracion ?? "—")
                            .FontSize(7).FontFamily(Fonts.Consolas);
                    }
                });
            }

            // Última respuesta MH
            var ultima = e.Respuestas.OrderByDescending(r => r.RecibidoAt).FirstOrDefault();
            if (ultima is not null)
            {
                col.Item().PaddingTop(14).Text("Respuesta de Hacienda").FontSize(10).Bold();
                col.Item().PaddingTop(2).Border(0.5f).Padding(6).Column(box =>
                {
                    box.Item().Row(r =>
                    {
                        r.RelativeItem().Text(t =>
                        {
                            t.Span("Estado: ").Bold();
                            t.Span(ultima.Estado ?? "—");
                        });
                        r.RelativeItem().Text(t =>
                        {
                            t.Span("Código: ").Bold();
                            t.Span(ultima.CodigoMsg ?? "—");
                        });
                        r.RelativeItem().AlignRight().Text($"{ultima.RecibidoAt:yyyy-MM-dd HH:mm}").FontSize(8);
                    });
                    if (!string.IsNullOrEmpty(ultima.DescripcionMsg))
                        box.Item().PaddingTop(2).Text(ultima.DescripcionMsg).FontSize(8);
                    if (!string.IsNullOrEmpty(ultima.SelloRecibido))
                    {
                        box.Item().PaddingTop(2).Text(t =>
                        {
                            t.Span("Sello: ").Bold();
                            t.Span(ultima.SelloRecibido!).FontFamily(Fonts.Consolas).FontSize(8);
                        });
                    }
                });
            }
        });
    }

    // ---------- Footer ----------

    private static void Footer(IContainer container, DteEvento e)
    {
        container.PaddingTop(8).BorderTop(0.5f).PaddingTop(4).Row(row =>
        {
            row.RelativeItem().Text($"Generado: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC").FontSize(7).Italic();
            row.RelativeItem().AlignRight().Text("NeoSTP Cloud · representación gráfica del evento").FontSize(7).Italic();
        });
    }

    // ---------- Helpers ----------

    private static IContainer HeaderCell(IContainer c)
        => c.Background(Colors.Grey.Lighten3).Padding(3).BorderBottom(0.5f);

    private static IContainer BodyCell(IContainer c)
        => c.Padding(3).BorderBottom(0.3f);

    private static string TipoNombre(string codigo) => codigo switch
    {
        TipoEventoCodigos.Invalidacion => "Evento de Invalidación",
        TipoEventoCodigos.Contingencia => "Evento de Contingencia",
        TipoEventoCodigos.Retorno => "Evento de Retorno",
        TipoEventoCodigos.OperacionesEspeciales => "Operaciones Especiales",
        _ => codigo,
    };

    private static string MotivoLabel(string tipo) => tipo switch
    {
        TipoEventoCodigos.Invalidacion => "Motivo de invalidación",
        TipoEventoCodigos.Contingencia => "Motivo de contingencia",
        TipoEventoCodigos.OperacionesEspeciales => "Descripción de la operación",
        _ => "Motivo / descripción",
    };

    private static string RelacionadosLabel(string tipo) => tipo switch
    {
        TipoEventoCodigos.Invalidacion => "DTE invalidado",
        TipoEventoCodigos.Contingencia => "DTE incluidos en el lote",
        TipoEventoCodigos.Retorno => "DTE origen del retorno",
        TipoEventoCodigos.OperacionesEspeciales => "Documentos relacionados",
        _ => "Documentos relacionados",
    };

    private static Color EstadoColor(string estado) => estado switch
    {
        DteEventoEstadoCodigos.Procesado => Colors.Green.Darken2,
        DteEventoEstadoCodigos.Rechazado => Colors.Red.Darken2,
        DteEventoEstadoCodigos.Error => Colors.Red.Darken1,
        DteEventoEstadoCodigos.Enviado => Colors.Blue.Darken1,
        DteEventoEstadoCodigos.Firmado => Colors.Blue.Lighten1,
        _ => Colors.Grey.Darken1,
    };
}
