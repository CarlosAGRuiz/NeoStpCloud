using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Infrastructure.Dte;
using Xunit;

namespace NeoSTP.Tests.Unit.Dte;

/// <summary>
/// Sender SMTP: valida el guard de destinatario sin abrir conexión de red.
/// (El envío real se valida manualmente desde el panel de Operación → Diagnóstico de correo.)
/// </summary>
public class SmtpEmailSenderTests
{
    private static SmtpEmailSender NewSender()
    {
        var opts = Options.Create(new EmailOptions
        {
            Provider = "Smtp",
            Smtp = new SmtpEmailOptions { Host = "smtp.invalid.local", Port = 587 },
            From = new EmailFromOptions { Address = "noreply@neostp.local", DisplayName = "NeoSTP" },
        });
        return new SmtpEmailSender(opts, NullLogger<SmtpEmailSender>.Instance);
    }

    [Fact]
    public async Task Enviar_SinDestinatario_FallaSinConectar()
    {
        var r = await NewSender().EnviarAsync(new EmailMessage { To = "", Subject = "x", HtmlBody = "x" });

        r.Success.Should().BeFalse();
        r.Mensaje.Should().Be("TO_REQUERIDO");
    }
}
