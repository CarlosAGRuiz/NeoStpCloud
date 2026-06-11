using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Notificaciones;
using NeoSTP.Infrastructure.Notificaciones;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Notificaciones;

/// <summary>V2.5-S2 — MetaWhatsAppSender: payload, respuesta, errores y normalización E.164.</summary>
public class MetaWhatsAppSenderTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request;
        public string? RequestBody;
        public HttpStatusCode Status = HttpStatusCode.OK;
        public string ResponseBody = """{"messages":[{"id":"wamid.TEST123"}]}""";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = request;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(Status)
            {
                Content = new StringContent(ResponseBody, Encoding.UTF8, "application/json"),
            };
        }
    }

    private static (MetaWhatsAppSender Sender, StubHandler Handler) Build(MetaWhatsAppOptions? options = null)
    {
        var handler = new StubHandler();
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(MetaWhatsAppSender.HttpClientName).Returns(_ => new HttpClient(handler));
        var sender = new MetaWhatsAppSender(
            factory,
            Options.Create(options ?? new MetaWhatsAppOptions { Token = "tok-1", PhoneNumberId = "999000" }),
            NullLogger<MetaWhatsAppSender>.Instance);
        return (sender, handler);
    }

    [Fact]
    public async Task Enviar_ConstruyePayloadYDevuelveMessageId()
    {
        var (sender, handler) = Build();

        var r = await sender.EnviarAsync(new WhatsAppMessage { To = "7000-0001", Body = "Recordatorio de pago" });

        r.Success.Should().BeTrue();
        r.MessageId.Should().Be("wamid.TEST123");
        handler.Request!.RequestUri!.ToString().Should().Be("https://graph.facebook.com/v20.0/999000/messages");
        handler.Request.Headers.Authorization!.ToString().Should().Be("Bearer tok-1");
        handler.RequestBody.Should().Contain("\"messaging_product\":\"whatsapp\"");
        handler.RequestBody.Should().Contain("\"to\":\"50370000001\""); // 8 dígitos → +503
        handler.RequestBody.Should().Contain("Recordatorio de pago");
    }

    [Fact]
    public async Task Enviar_ErrorDeMeta_DevuelveMensajeDeError()
    {
        var (sender, handler) = Build();
        handler.Status = HttpStatusCode.BadRequest;
        handler.ResponseBody = """{"error":{"message":"(#131030) Recipient phone number not in allowed list","code":131030}}""";

        var r = await sender.EnviarAsync(new WhatsAppMessage { To = "50370000001", Body = "x" });

        r.Success.Should().BeFalse();
        r.Error.Should().Contain("131030");
    }

    [Fact]
    public async Task Enviar_SinCredencialesOTelefonoInvalido_FallaSinLlamarRed()
    {
        var (sinCreds, handlerA) = Build(new MetaWhatsAppOptions());
        (await sinCreds.EnviarAsync(new WhatsAppMessage { To = "50370000001", Body = "x" }))
            .Success.Should().BeFalse();
        handlerA.Request.Should().BeNull();

        var (ok, handlerB) = Build();
        (await ok.EnviarAsync(new WhatsAppMessage { To = "123", Body = "x" })).Success.Should().BeFalse();
        handlerB.Request.Should().BeNull();
    }

    [Theory]
    [InlineData("7000-0001", "50370000001")]   // local SV con guion
    [InlineData("+503 7000 0001", "50370000001")]
    [InlineData("50370000001", "50370000001")]
    [InlineData("12025550123", "12025550123")] // internacional ya completo
    [InlineData("123", null)]                  // muy corto
    [InlineData("", null)]
    public void NormalizarTelefono_CasosE164(string entrada, string? esperado)
        => MetaWhatsAppSender.NormalizarTelefono(entrada, "503").Should().Be(esperado);
}
