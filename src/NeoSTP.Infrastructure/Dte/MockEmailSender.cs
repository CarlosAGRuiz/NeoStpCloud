using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Abstractions;

namespace NeoSTP.Infrastructure.Dte;

/// <summary>
/// Sender de correo "mock" para desarrollo. En lugar de enviar por SMTP,
/// escribe el mensaje (cabeceras + body + adjuntos) en una carpeta local
/// que el usuario puede inspeccionar. No requiere credenciales SMTP.
/// </summary>
public class MockEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<MockEmailSender> _logger;

    public MockEmailSender(IOptions<EmailOptions> options, ILogger<MockEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<EmailSendResult> EnviarAsync(EmailMessage m, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(m.To))
            return new() { Success = false, Mensaje = "TO_REQUERIDO", Detalle = "El destinatario es obligatorio." };

        try
        {
            Directory.CreateDirectory(_options.MockOutbox);
            var messageId = $"<{Guid.NewGuid():N}@neostp.local>";
            var safeTo = SafeName(m.To);
            var file = Path.Combine(_options.MockOutbox,
                $"{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{safeTo}.eml");

            await using var fs = File.Create(file);
            await using var w = new StreamWriter(fs, Encoding.UTF8);
            await w.WriteLineAsync($"Message-ID: {messageId}");
            await w.WriteLineAsync($"Date: {DateTime.UtcNow:R}");
            await w.WriteLineAsync($"From: \"{_options.From.DisplayName}\" <{_options.From.Address}>");
            await w.WriteLineAsync($"To: {m.To}");
            if (!string.IsNullOrEmpty(m.Cc)) await w.WriteLineAsync($"Cc: {m.Cc}");
            if (!string.IsNullOrEmpty(m.Bcc)) await w.WriteLineAsync($"Bcc: {m.Bcc}");
            if (!string.IsNullOrEmpty(m.ReplyTo)) await w.WriteLineAsync($"Reply-To: {m.ReplyTo}");
            await w.WriteLineAsync($"Subject: {m.Subject}");
            await w.WriteLineAsync("MIME-Version: 1.0");
            await w.WriteLineAsync("Content-Type: text/html; charset=utf-8");
            await w.WriteLineAsync();
            await w.WriteLineAsync(m.HtmlBody);
            await w.WriteLineAsync();

            foreach (var att in m.Attachments)
            {
                await w.WriteLineAsync($"--- ATTACHMENT: {att.FileName} ({att.MediaType}, {att.Content.Length} bytes) ---");
                // Guardamos los adjuntos en paralelo al .eml para que el dev los pueda abrir
                var attPath = Path.Combine(_options.MockOutbox,
                    $"{Path.GetFileNameWithoutExtension(file)}__{att.FileName}");
                await File.WriteAllBytesAsync(attPath, att.Content, ct);
            }

            _logger.LogInformation("MockEmailSender: correo a {To} guardado en {File}.", m.To, file);

            return new EmailSendResult
            {
                Success = true,
                MessageId = messageId,
                Mensaje = "OK",
                Detalle = $"Correo mock persistido en {file}",
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MockEmailSender: error al persistir correo");
            return new EmailSendResult { Success = false, Mensaje = "IO_ERROR", Detalle = ex.Message };
        }
    }

    private static string SafeName(string s)
    {
        var chars = s.Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' or '@' ? c : '_');
        return new string(chars.ToArray());
    }
}
