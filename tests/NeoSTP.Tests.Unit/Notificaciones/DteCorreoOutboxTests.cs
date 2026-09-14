using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Comunicaciones;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Application.Notificaciones;
using NeoSTP.Application.Workers;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Notificaciones;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Notificaciones;

public sealed class DteCorreoOutboxTests
{
    [Fact]
    public async Task ProcessorSendsReceiverAndIssuerAsIndependentMessagesAndPublishesStatus()
    {
        await using var db = await NewDbAsync();
        var delivery = new DteCorreoEntregaService(db);
        var first = await delivery.ReencolarAsync(
            10, 20, DteCorreoFinalidades.Ambos, "manual-key-001", "tester");
        var duplicate = await delivery.ReencolarAsync(
            10, 20, DteCorreoFinalidades.Ambos, "manual-key-001", "tester");

        first.IsSuccess.Should().BeTrue();
        duplicate.IsSuccess.Should().BeTrue();
        (await db.NotificationOutbox.CountAsync()).Should().Be(2);

        var sent = new List<EmailMessage>();
        var email = Substitute.For<ITenantEmailSender>();
        email.EnviarAsync(10, Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var message = call.ArgAt<EmailMessage>(1);
                sent.Add(message);
                return new EmailSendResult
                {
                    Success = true,
                    Mensaje = "accepted",
                    MessageId = $"provider-{sent.Count}",
                };
            });
        var processor = CreateProcessor(db, email);

        (await processor.ProcessPendingAsync()).Should().Be(2);

        sent.Should().HaveCount(2);
        sent.Select(x => x.To).Should().BeEquivalentTo(
            "receiver@example.invalid", "issuer@example.invalid");
        sent.Should().OnlyContain(x => x.Cc == null && x.Bcc == null);
        sent.Should().OnlyContain(x => x.Attachments.Count == 2);
        sent.Single(x => x.To == "receiver@example.invalid").Subject
            .Should().NotStartWith("Copia emisor -");
        sent.Single(x => x.To == "issuer@example.invalid").Subject
            .Should().StartWith("Copia emisor -");

        var rows = await db.NotificationOutbox.OrderBy(x => x.Id).ToListAsync();
        rows.Should().OnlyContain(x => x.Estado == NotificationOutboxEstados.Sent);
        rows.Should().OnlyContain(x => x.ProveedorMessageId != null);
        (await db.Alertas.CountAsync(x => x.TipoCodigo == AlertaTipos.DteCorreoEstado))
            .Should().Be(2);

        var status = await delivery.GetEstadoAsync(10, 20);
        status.IsSuccess.Should().BeTrue();
        status.Value!.EstadoGeneral.Should().Be(DteCorreoEstados.Enviado);
        status.Value.Receptor.Destinatario.Should().Be("re***@example.invalid");
        status.Value.Emisor.Destinatario.Should().Be("is***@example.invalid");
        (await delivery.GetEstadoAsync(99, 20)).ErrorCode.Should().Be("DTE_NOT_FOUND");
    }

    [Fact]
    public async Task TemporaryProviderFailureSchedulesRetryWithoutChangingProcessedDte()
    {
        await using var db = await NewDbAsync();
        var delivery = new DteCorreoEntregaService(db);
        (await delivery.ReencolarAsync(
            10, 20, DteCorreoFinalidades.Receptor, "manual-key-002", "tester"))
            .IsSuccess.Should().BeTrue();
        var email = Substitute.For<ITenantEmailSender>();
        email.EnviarAsync(10, Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(new EmailSendResult { Success = false, Mensaje = "temporary" });

        (await CreateProcessor(db, email).ProcessPendingAsync()).Should().Be(1);

        var outbox = await db.NotificationOutbox.SingleAsync();
        outbox.Estado.Should().Be(NotificationOutboxEstados.Failed);
        outbox.Intentos.Should().Be(1);
        outbox.DisponibleDesde.Should().BeAfter(DateTime.UtcNow);
        (await db.DteDocumentos.SingleAsync(x => x.Id == 20)).EstadoCodigo
            .Should().Be(DteEstadoCodigos.Procesado);
        var status = await delivery.GetEstadoAsync(10, 20);
        status.Value!.Receptor.Estado.Should().Be(DteCorreoEstados.Reintentando);
        var alert = await db.Alertas.SingleAsync();
        alert.Severidad.Should().Be(AlertaSeveridades.Advertencia);
        alert.Mensaje.Should().Contain("reintentado automáticamente");
    }

    [Fact]
    public async Task RequeueBothValidatesAllDestinationsBeforeAddingAnyMessage()
    {
        await using var db = await NewDbAsync();
        var issuer = await db.Empresas.SingleAsync(x => x.Id == 10);
        issuer.Correo = null;
        await db.SaveChangesAsync();
        var delivery = new DteCorreoEntregaService(db);

        var result = await delivery.ReencolarAsync(
            10, 20, DteCorreoFinalidades.Ambos, "manual-key-003", "tester");

        result.ErrorCode.Should().Be("EMAIL_DESTINATION_MISSING");
        db.ChangeTracker.Entries<NotificationOutboxMessage>()
            .Should().NotContain(x => x.State == EntityState.Added);
        (await db.NotificationOutbox.CountAsync()).Should().Be(0);
    }

    private static async Task<NeoStpDbContext> NewDbAsync()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"dte-email-outbox-{Guid.NewGuid():N}")
            .Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.AddRange(
            new Empresa
            {
                Id = 10,
                Nit = "00000000000000",
                RazonSocial = "Issuer Synthetic",
                Correo = "issuer@example.invalid",
                EstadoCodigo = "ACTIVA",
            },
            new Empresa
            {
                Id = 99,
                Nit = "11111111111111",
                RazonSocial = "Other Tenant",
                Correo = "other@example.invalid",
                EstadoCodigo = "ACTIVA",
            });
        db.DteDocumentos.Add(new DteDocumento
        {
            Id = 20,
            EmpresaId = 10,
            AmbienteCodigo = DteAmbientes.Produccion,
            TipoDteCodigo = TipoDteCodigos.FacturaConsumidorFinal,
            NumeroControl = "DTE-01-M001P001-000000000000020",
            CodigoGeneracion = Guid.NewGuid().ToString().ToUpperInvariant(),
            FechaEmision = new DateTime(2026, 9, 14),
            EstadoCodigo = DteEstadoCodigos.Procesado,
            SelloRecibido = "SELLO-SYNTHETIC",
            ReceptorNombre = "Receiver Synthetic",
            ReceptorCorreo = "receiver@example.invalid",
            TotalPagar = 113m,
            Json = new DteDocumentoJson { JsonDte = "{\"synthetic\":true}" },
        });
        await db.SaveChangesAsync();
        return db;
    }

    private static NotificationOutboxProcessor CreateProcessor(
        NeoStpDbContext db,
        ITenantEmailSender email)
    {
        var pdf = Substitute.For<IDtePdfService>();
        pdf.Generar(Arg.Any<DteDocumento>()).Returns("%PDF-synthetic"u8.ToArray());
        var dispatcher = new DteCorreoOutboxDispatcher(db, pdf, email);
        return new NotificationOutboxProcessor(
            db,
            Substitute.For<IPushSender>(),
            Options.Create(new WorkerOptions()),
            NullLogger<NotificationOutboxProcessor>.Instance,
            dispatcher);
    }
}
