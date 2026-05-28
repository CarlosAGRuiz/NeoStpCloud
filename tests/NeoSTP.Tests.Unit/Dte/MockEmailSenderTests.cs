using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Infrastructure.Dte;
using Xunit;

namespace NeoSTP.Tests.Unit.Dte;

public class MockEmailSenderTests : IDisposable
{
    private readonly string _outbox = Path.Combine(Path.GetTempPath(), $"neostp-test-{Guid.NewGuid():N}");

    private MockEmailSender BuildSender()
    {
        var opts = Options.Create(new EmailOptions
        {
            Provider = "Mock",
            MockOutbox = _outbox,
            From = new EmailFromOptions { Address = "test@neostp.local", DisplayName = "NeoSTP Test" },
        });
        return new MockEmailSender(opts, NullLogger<MockEmailSender>.Instance);
    }

    [Fact]
    public async Task Enviar_PersisteEmlEnDisco()
    {
        var sender = BuildSender();
        var r = await sender.EnviarAsync(new EmailMessage
        {
            To = "cliente@demo.local",
            Subject = "Test DTE",
            HtmlBody = "<p>Hola</p>",
        });

        r.Success.Should().BeTrue();
        r.MessageId.Should().NotBeNullOrEmpty();
        Directory.GetFiles(_outbox, "*.eml").Should().HaveCount(1);
        var content = File.ReadAllText(Directory.GetFiles(_outbox, "*.eml")[0]);
        content.Should().Contain("To: cliente@demo.local");
        content.Should().Contain("Subject: Test DTE");
        content.Should().Contain("<p>Hola</p>");
    }

    [Fact]
    public async Task Enviar_TambienGuardaAdjuntos()
    {
        var sender = BuildSender();
        var r = await sender.EnviarAsync(new EmailMessage
        {
            To = "cliente@demo.local",
            Subject = "Test con adjuntos",
            HtmlBody = "<p>Con adjuntos</p>",
            Attachments =
            {
                new EmailAttachment { FileName = "demo.json", MediaType = "application/json",
                    Content = Encoding.UTF8.GetBytes("{\"hola\":true}") },
            },
        });

        r.Success.Should().BeTrue();
        var attachments = Directory.GetFiles(_outbox, "*demo.json");
        attachments.Should().HaveCount(1);
        File.ReadAllText(attachments[0]).Should().Contain("hola");
    }

    [Fact]
    public async Task Enviar_DestinatarioVacio_Falla()
    {
        var sender = BuildSender();
        var r = await sender.EnviarAsync(new EmailMessage { To = "", Subject = "x", HtmlBody = "x" });
        r.Success.Should().BeFalse();
        r.Mensaje.Should().Be("TO_REQUERIDO");
    }

    public void Dispose()
    {
        if (Directory.Exists(_outbox))
            Directory.Delete(_outbox, true);
    }
}
