using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Notificaciones;
using NeoSTP.Application.Workers;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Notificaciones;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Notificaciones;

public sealed class NotificationOutboxTests
{
    private static NeoStpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"notification-outbox-{Guid.NewGuid()}")
            .Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.AddRange(
            new Empresa { Id = 1, Nit = "1", RazonSocial = "Uno", EstadoCodigo = "ACTIVA" },
            new Empresa { Id = 2, Nit = "2", RazonSocial = "Dos", EstadoCodigo = "ACTIVA" });
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task Enqueue_DeduplicaPorEmpresaYClave()
    {
        await using var db = NewDb();
        var service = new NotificationOutboxService(db);
        var request = Request(1, "same-key");

        var first = await service.EnqueueAsync(request);
        var duplicate = await service.EnqueueAsync(request);
        var otherTenant = await service.EnqueueAsync(Request(2, "same-key"));

        duplicate.Should().Be(first);
        otherTenant.Should().NotBe(first);
        (await db.NotificationOutbox.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Processor_FallaConBackoffYTerminaDeadAlAgotarIntentos()
    {
        await using var db = NewDb();
        db.DispositivosNotificacion.Add(new DispositivoNotificacion
        {
            EmpresaId = 1,
            UsuarioId = 10,
            Token = "retry-token",
            Activo = true,
        });
        var alerta = new Alerta
        {
            EmpresaId = 1,
            TipoCodigo = AlertaTipos.DteRechazado,
            Severidad = AlertaSeveridades.Critica,
            Titulo = "DTE rechazado",
            Mensaje = "Revisar",
            EntidadTipo = "DteDocumento",
            EntidadId = 7,
            Clave = "DTE:7",
        };
        db.Alertas.Add(alerta);
        await db.SaveChangesAsync();

        var service = new NotificationOutboxService(db);
        await service.EnqueueAsync(Request(1, "retry", alerta.Id, maxAttempts: 6));
        var push = Substitute.For<IPushSender>();
        push.EnviarAsync(Arg.Any<PushMessage>(), Arg.Any<CancellationToken>())
            .Returns(new PushResult { Success = false, Detalle = "raw-provider-detail" });
        var processor = CreateProcessor(db, push);

        var before = DateTime.UtcNow;
        (await processor.ProcessPendingAsync()).Should().Be(1);
        var message = await db.NotificationOutbox.SingleAsync();
        message.Estado.Should().Be(NotificationOutboxEstados.Failed);
        message.Intentos.Should().Be(1);
        message.DisponibleDesde.Should().BeCloseTo(before.AddMinutes(1), TimeSpan.FromSeconds(10));
        message.ErrorUltimo.Should().NotContain("raw-provider-detail");

        for (var attempt = 2; attempt <= 6; attempt++)
        {
            message.DisponibleDesde = DateTime.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
            (await processor.ProcessPendingAsync()).Should().Be(1);
            message = await db.NotificationOutbox.SingleAsync();
        }

        message.Intentos.Should().Be(6);
        message.Estado.Should().Be(NotificationOutboxEstados.Dead);
        await push.Received(6).EnviarAsync(Arg.Any<PushMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Processor_RecuperaLeaseVencido()
    {
        await using var db = NewDb();
        var alerta = new Alerta
        {
            EmpresaId = 1,
            TipoCodigo = AlertaTipos.DteRechazado,
            Severidad = AlertaSeveridades.Advertencia,
            Titulo = "Aviso",
            Mensaje = "Mensaje",
            Clave = "A:1",
        };
        db.Alertas.Add(alerta);
        await db.SaveChangesAsync();
        db.NotificationOutbox.Add(new NotificationOutboxMessage
        {
            EmpresaId = 1,
            Tipo = NotificationOutboxTipos.AlertaCreada,
            Canal = NotificationOutboxCanales.Push,
            Payload = JsonSerializer.Serialize(new AlertaPushOutboxPayload(1, alerta.Id)),
            ClaveIdempotencia = "expired-lease",
            Estado = NotificationOutboxEstados.Processing,
            Intentos = 1,
            MaxIntentos = 6,
            LeaseId = "abandoned",
            LeaseExpiresAt = DateTime.UtcNow.AddMinutes(-1),
        });
        await db.SaveChangesAsync();
        var push = Substitute.For<IPushSender>();
        push.EnviarAsync(Arg.Any<PushMessage>(), Arg.Any<CancellationToken>())
            .Returns(new PushResult { Success = true });

        (await CreateProcessor(db, push).ProcessPendingAsync()).Should().Be(1);

        var message = await db.NotificationOutbox.SingleAsync();
        message.Estado.Should().Be(NotificationOutboxEstados.Sent);
        message.Intentos.Should().Be(2);
        message.LeaseId.Should().BeNull();
    }

    [Fact]
    public async Task Processor_EnviaADedLetterPayloadNoSoportado()
    {
        await using var db = NewDb();
        db.NotificationOutbox.Add(new NotificationOutboxMessage
        {
            EmpresaId = 1,
            Tipo = "DESCONOCIDO",
            Canal = NotificationOutboxCanales.Email,
            Payload = "{}",
            ClaveIdempotencia = "unsupported",
        });
        await db.SaveChangesAsync();

        var push = Substitute.For<IPushSender>();
        (await CreateProcessor(db, push).ProcessPendingAsync()).Should().Be(1);

        var message = await db.NotificationOutbox.SingleAsync();
        message.Estado.Should().Be(NotificationOutboxEstados.Dead);
        message.ErrorUltimo.Should().Be("Tipo o canal no soportado por el dispatcher.");
        await push.DidNotReceive().EnviarAsync(Arg.Any<PushMessage>(), Arg.Any<CancellationToken>());
    }

    private static NotificationOutboxRequest Request(
        int empresaId,
        string key,
        int alertaId = 1,
        int maxAttempts = 6)
        => new(
            empresaId,
            NotificationOutboxTipos.AlertaCreada,
            NotificationOutboxCanales.Push,
            null,
            JsonSerializer.Serialize(new AlertaPushOutboxPayload(empresaId, alertaId)),
            key,
            maxAttempts);

    private static NotificationOutboxProcessor CreateProcessor(NeoStpDbContext db, IPushSender push)
        => new(
            db,
            push,
            Options.Create(new WorkerOptions()),
            NullLogger<NotificationOutboxProcessor>.Instance);
}
