using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MimeKit;
using NeoSTP.Application.Comunicaciones;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Comunicaciones;

/// <summary>
/// Envía correo resolviendo la configuración SMTP por empresa. Si la empresa tiene una
/// configuración activa, envía con sus credenciales (vía MailKit); si no, recae en el
/// <see cref="IEmailSender"/> global del sistema.
/// </summary>
public class TenantEmailSender : ITenantEmailSender
{
    private readonly NeoStpDbContext _db;
    private readonly IEmailSender _globalSender;
    private readonly ISecretProtector _protector;
    private readonly ILogger<TenantEmailSender> _logger;

    public TenantEmailSender(NeoStpDbContext db, IEmailSender globalSender, ISecretProtector protector, ILogger<TenantEmailSender> logger)
    {
        _db = db;
        _globalSender = globalSender;
        _protector = protector;
        _logger = logger;
    }

    public async Task<EmailSendResult> EnviarAsync(int empresaId, EmailMessage message, CancellationToken ct = default)
    {
        var cfg = await _db.ConfiguracionesCorreo.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EmpresaId == empresaId && c.Activo, ct);
        if (cfg is null || string.IsNullOrWhiteSpace(cfg.Host))
            return await _globalSender.EnviarAsync(message, ct); // fallback global

        if (string.IsNullOrWhiteSpace(message.To))
            return new() { Success = false, Mensaje = "TO_REQUERIDO", Detalle = "El destinatario es obligatorio." };

        try
        {
            var mime = Build(message, cfg.FromNombre, cfg.FromEmail);
            using var client = new SmtpClient();
            var secure = cfg.UsarStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.SslOnConnect;
            await client.ConnectAsync(cfg.Host, cfg.Puerto, secure, ct);
            if (!string.IsNullOrWhiteSpace(cfg.Usuario))
            {
                var pass = _protector.UnprotectOrNull(cfg.PasswordProtegida) ?? "";
                await client.AuthenticateAsync(cfg.Usuario, pass, ct);
            }
            await client.SendAsync(mime, ct);
            await client.DisconnectAsync(true, ct);
            return new() { Success = true, Mensaje = "ENVIADO", MessageId = mime.MessageId };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fallo SMTP por empresa {EmpresaId}", empresaId);
            return new() { Success = false, Mensaje = "SMTP_FAILED", Detalle = ex.Message };
        }
    }

    private static MimeMessage Build(EmailMessage m, string fromNombre, string fromEmail)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(fromNombre, fromEmail));
        foreach (var to in m.To.Split(',', ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            mime.To.Add(MailboxAddress.Parse(to));
        if (!string.IsNullOrEmpty(m.Cc))
            foreach (var cc in m.Cc.Split(',', ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                mime.Cc.Add(MailboxAddress.Parse(cc));
        if (!string.IsNullOrEmpty(m.Bcc))
            foreach (var bcc in m.Bcc.Split(',', ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                mime.Bcc.Add(MailboxAddress.Parse(bcc));
        if (!string.IsNullOrEmpty(m.ReplyTo)) mime.ReplyTo.Add(MailboxAddress.Parse(m.ReplyTo));
        mime.Subject = m.Subject;

        var builder = new BodyBuilder { HtmlBody = m.HtmlBody, TextBody = m.TextBody };
        foreach (var img in m.InlineImages)
        {
            var res = builder.LinkedResources.Add(img.ContentId, img.Content, ContentType.Parse(img.MediaType));
            res.ContentId = img.ContentId;
        }
        foreach (var att in m.Attachments)
            builder.Attachments.Add(att.FileName, att.Content, ContentType.Parse(att.MediaType));
        mime.Body = builder.ToMessageBody();
        return mime;
    }
}
