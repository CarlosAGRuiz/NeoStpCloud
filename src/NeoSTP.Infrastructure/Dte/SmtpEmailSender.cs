using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Abstractions;

namespace NeoSTP.Infrastructure.Dte;

/// <summary>
/// Sender SMTP real con MailKit. Soporta STARTTLS (puerto 587) y SSL implícito
/// (puerto 465) según la configuración. Credenciales y host vienen de
/// <see cref="EmailOptions.Smtp"/>.
/// </summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<EmailSendResult> EnviarAsync(EmailMessage m, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(m.To))
            return new() { Success = false, Mensaje = "TO_REQUERIDO", Detalle = "El destinatario es obligatorio." };

        var smtp = _options.Smtp;
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_options.From.DisplayName, _options.From.Address));
        foreach (var to in m.To.Split(',', ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            mime.To.Add(MailboxAddress.Parse(to));
        if (!string.IsNullOrEmpty(m.Cc))
            foreach (var cc in m.Cc.Split(',', ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                mime.Cc.Add(MailboxAddress.Parse(cc));
        if (!string.IsNullOrEmpty(m.Bcc))
            foreach (var bcc in m.Bcc.Split(',', ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                mime.Bcc.Add(MailboxAddress.Parse(bcc));
        if (!string.IsNullOrEmpty(m.ReplyTo))
            mime.ReplyTo.Add(MailboxAddress.Parse(m.ReplyTo));
        mime.Subject = m.Subject;

        var body = new BodyBuilder
        {
            HtmlBody = m.HtmlBody,
            TextBody = m.TextBody ?? StripTags(m.HtmlBody),
        };
        foreach (var att in m.Attachments)
            body.Attachments.Add(att.FileName, att.Content, ContentType.Parse(att.MediaType));
        mime.Body = body.ToMessageBody();

        using var client = new SmtpClient();
        try
        {
            var secureOption = smtp.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;
            await client.ConnectAsync(smtp.Host, smtp.Port, secureOption, ct);
            if (!string.IsNullOrEmpty(smtp.Username))
                await client.AuthenticateAsync(smtp.Username, smtp.Password ?? string.Empty, ct);

            var response = await client.SendAsync(mime, ct);
            await client.DisconnectAsync(true, ct);

            _logger.LogInformation("SmtpEmailSender: correo a {To} enviado vía {Host}:{Port}. Server={Resp}",
                m.To, smtp.Host, smtp.Port, response);
            return new EmailSendResult
            {
                Success = true, MessageId = mime.MessageId,
                Mensaje = "OK",
                Detalle = $"SMTP {smtp.Host}:{smtp.Port} -> {response}",
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SmtpEmailSender: error enviando correo");
            return new EmailSendResult
            {
                Success = false, Mensaje = "SMTP_ERROR", Detalle = ex.Message,
            };
        }
    }

    private static string StripTags(string html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;
        var sb = new System.Text.StringBuilder(html.Length);
        bool inTag = false;
        foreach (var c in html)
        {
            if (c == '<') inTag = true;
            else if (c == '>') inTag = false;
            else if (!inTag) sb.Append(c);
        }
        return sb.ToString();
    }
}
