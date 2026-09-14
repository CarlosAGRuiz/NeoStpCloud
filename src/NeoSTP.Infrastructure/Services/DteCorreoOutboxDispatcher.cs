using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Comunicaciones;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Application.Notificaciones;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Notificaciones;
using NeoSTP.Infrastructure.Branding;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

public sealed class DteCorreoOutboxDispatcher : IDteCorreoOutboxDispatcher
{
    private readonly NeoStpDbContext _db;
    private readonly IDtePdfService _pdf;
    private readonly ITenantEmailSender _email;

    public DteCorreoOutboxDispatcher(
        NeoStpDbContext db,
        IDtePdfService pdf,
        ITenantEmailSender email)
    {
        _db = db;
        _pdf = pdf;
        _email = email;
    }

    public async Task<EmailSendResult> EnviarAsync(
        NotificationOutboxMessage message,
        DteCorreoOutboxPayload payload,
        CancellationToken ct = default)
    {
        if (payload.EmpresaId != message.EmpresaId
            || payload.DteDocumentoId != message.EntidadId
            || payload.Finalidad != message.Finalidad
            || payload.Finalidad is not (DteCorreoFinalidades.Receptor or DteCorreoFinalidades.Emisor)
            || string.IsNullOrWhiteSpace(message.Destinatario))
            return new EmailSendResult { Success = false, Mensaje = "PAYLOAD_INVALIDO" };

        var doc = await _db.DteDocumentos.AsNoTracking()
            .Include(x => x.Detalles)
            .Include(x => x.Json)
            .Include(x => x.Empresa)
            .FirstOrDefaultAsync(x => x.EmpresaId == message.EmpresaId
                && x.Id == payload.DteDocumentoId, ct);
        if (doc is null)
            return new EmailSendResult { Success = false, Mensaje = "DTE_NO_ENCONTRADO" };
        if (doc.EstadoCodigo != DteEstadoCodigos.Procesado || string.IsNullOrWhiteSpace(doc.SelloRecibido))
            return new EmailSendResult { Success = false, Mensaje = "DTE_NO_PROCESADO" };

        byte[] pdf;
        try
        {
            pdf = _pdf.Generar(doc);
        }
        catch
        {
            return new EmailSendResult { Success = false, Mensaje = "PDF_NO_DISPONIBLE" };
        }

        var safeNumero = (doc.NumeroControl ?? "documento").Replace(" ", "_");
        var emisor = doc.Empresa?.RazonSocial ?? "su proveedor";
        var tieneLogo = BrandingImageValidator.TryValidate(doc.Empresa?.LogoBlob, null,
            out var logoContentType, out _)
            && doc.Empresa!.LogoBlob is { Length: > 0 };
        var body = DteDocumentosService.BuildBody(doc, emisor, tieneLogo);
        var subject = $"DTE {doc.TipoDteCodigo} {doc.NumeroControl} - {emisor}";
        if (payload.Finalidad == DteCorreoFinalidades.Emisor)
        {
            subject = $"Copia emisor - {subject}";
            body = "<p><strong>Copia automática para el emisor.</strong> Esta entrega es independiente del correo enviado al receptor.</p>" + body;
        }

        var outgoing = new EmailMessage
        {
            To = message.Destinatario,
            Subject = subject,
            HtmlBody = body,
            Cc = null,
            Bcc = null,
        };
        outgoing.Attachments.Add(new EmailAttachment
        {
            FileName = $"{safeNumero}.pdf",
            MediaType = "application/pdf",
            Content = pdf,
        });
        if (!string.IsNullOrWhiteSpace(doc.Json?.JsonDte))
        {
            outgoing.Attachments.Add(new EmailAttachment
            {
                FileName = $"{safeNumero}.json",
                MediaType = "application/json",
                Content = System.Text.Encoding.UTF8.GetBytes(doc.Json.JsonDte),
            });
        }
        if (tieneLogo)
        {
            outgoing.InlineImages.Add(new EmailInlineImage
            {
                ContentId = "logo",
                MediaType = logoContentType,
                Content = doc.Empresa!.LogoBlob!,
            });
        }

        return await _email.EnviarAsync(message.EmpresaId, outgoing, ct);
    }

    public async Task ActualizarAlertaAsync(
        NotificationOutboxMessage message,
        DteCorreoOutboxPayload payload,
        CancellationToken ct = default)
    {
        if (message.Estado is not (NotificationOutboxEstados.Sent
            or NotificationOutboxEstados.Failed
            or NotificationOutboxEstados.Dead))
            return;

        var key = $"DTE_CORREO:{payload.DteDocumentoId}:{payload.Finalidad}";
        var alerta = await _db.Alertas.FirstOrDefaultAsync(x =>
            x.EmpresaId == message.EmpresaId && x.Clave == key, ct);
        var finalidad = payload.Finalidad == DteCorreoFinalidades.Receptor
            ? "receptor" : "emisor";
        var sent = message.Estado == NotificationOutboxEstados.Sent;
        var retrying = message.Estado == NotificationOutboxEstados.Failed;
        var now = DateTime.UtcNow;

        if (alerta is null)
        {
            alerta = new Alerta
            {
                EmpresaId = message.EmpresaId,
                TipoCodigo = AlertaTipos.DteCorreoEstado,
                Clave = key,
                EntidadTipo = "DteDocumento",
                EntidadId = payload.DteDocumentoId,
                CreatedAt = now,
                CreatedBy = "NotificationOutboxWorker",
            };
            _db.Alertas.Add(alerta);
        }

        alerta.Severidad = sent ? AlertaSeveridades.Info
            : retrying ? AlertaSeveridades.Advertencia
            : AlertaSeveridades.Critica;
        alerta.Titulo = sent
            ? $"Correo DTE enviado al {finalidad}"
            : retrying
                ? $"Correo DTE pendiente de reintento para el {finalidad}"
                : $"Correo DTE fallido para el {finalidad}";
        alerta.Mensaje = sent
            ? "El proveedor de correo aceptó el mensaje."
            : retrying
                ? $"El envío no fue confirmado y será reintentado automáticamente. Intento {message.Intentos} de {message.MaxIntentos}."
                : $"El envío agotó sus intentos. Revise la configuración SMTP y reencólelo desde el documento. Intentos: {message.Intentos}.";
        alerta.EstadoCodigo = AlertaEstados.Pendiente;
        alerta.LeidaAt = null;
        alerta.ResueltaAt = null;
        alerta.UpdatedAt = now;
        alerta.UpdatedBy = "NotificationOutboxWorker";
    }
}
