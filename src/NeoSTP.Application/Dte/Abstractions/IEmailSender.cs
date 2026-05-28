namespace NeoSTP.Application.Dte.Abstractions;

public class EmailAttachment
{
    public string FileName { get; set; } = null!;
    public string MediaType { get; set; } = "application/octet-stream";
    public byte[] Content { get; set; } = Array.Empty<byte>();
}

public class EmailMessage
{
    public string To { get; set; } = null!;
    public string Subject { get; set; } = null!;
    public string HtmlBody { get; set; } = null!;
    public string? TextBody { get; set; }
    public string? Cc { get; set; }
    public string? Bcc { get; set; }
    public string? ReplyTo { get; set; }
    public List<EmailAttachment> Attachments { get; set; } = new();
}

public class EmailSendResult
{
    public bool Success { get; set; }
    public string? Mensaje { get; set; }
    public string? Detalle { get; set; }
    public string? MessageId { get; set; }
}

/// <summary>
/// Envío de correo electrónico. Implementaciones:
///   - MockEmailSender (default): persiste el correo en disco para inspección.
///   - SmtpEmailSender: SMTP real vía MailKit.
/// Toggle por configuración Email:Provider (Mock | Smtp).
/// </summary>
public interface IEmailSender
{
    Task<EmailSendResult> EnviarAsync(EmailMessage message, CancellationToken ct = default);
}
