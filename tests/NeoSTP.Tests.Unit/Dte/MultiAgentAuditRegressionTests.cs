using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Comunicaciones;
using NeoSTP.Application.Connect;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Domain.Core.Connect;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Dte;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;

namespace NeoSTP.Tests.Unit.Dte;

/// <summary>
/// GL1D acceptance regressions converted from the independently reproduced defects.
/// Uses real DteDocumentosService, synthetic InMemory storage and simulated MH.
/// </summary>
public class MultiAgentAuditRegressionTests
{
    [Fact]
    public async Task SignedGeneration_CannotDowngradeThroughValidationOrReplaceJwsThroughSigning()
    {
        await using var db = DteFiscalIsolationTests.Db();
        var doc = await Seed(db);
        var generatedAt = new DateTime(2026, 9, 4, 10, 0, 0, DateTimeKind.Utc);
        var signedAt = generatedAt.AddMinutes(1);
        doc.GeneradoAt = generatedAt;
        doc.Json!.GeneradoAt = generatedAt;
        doc.Json.FirmadoAt = signedAt;
        await db.SaveChangesAsync();
        var originalJson = doc.Json.JsonDte;
        var originalJws = doc.Json.JsonFirmado;
        var signer = Substitute.For<IDteSignerService>();
        var reception = Substitute.For<IHaciendaReceptionClient>();
        var service = Service(db, reception, Auth(), signerOverride: signer);

        var validation = await service.ValidarAsync(10, doc.Id, "audit");
        validation.ErrorCode.Should().Be("INVALID_STATE", "validation must not reopen a signed generation");
        var signing = await service.FirmarAsync(10, doc.Id, "audit");
        signing.IsSuccess.Should().BeTrue("repeated signing returns the compatible existing signature");

        db.ChangeTracker.Clear();
        var actual = await db.DteDocumentos.AsNoTracking().Include(d => d.Json).SingleAsync(d => d.Id == doc.Id);
        actual.EstadoCodigo.Should().Be(DteEstadoCodigos.Firmado);
        actual.GeneradoAt.Should().Be(generatedAt);
        actual.EnviadoAt.Should().BeNull();
        actual.Json!.GeneradoAt.Should().Be(generatedAt);
        actual.Json.FirmadoAt.Should().Be(signedAt);
        actual.Json.JsonDte.Should().Be(originalJson);
        actual.Json.JsonFirmado.Should().Be(originalJws);
        await signer.DidNotReceive().FirmarAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await reception.DidNotReceive().EnviarAsync(Arg.Any<HaciendaReceptionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LegacyEnviadoWithoutTimestamp_DoesNotResendOrChangeSignedPayload()
    {
        await using var db = DteFiscalIsolationTests.Db();
        var doc = await Seed(db);
        doc.EstadoCodigo = DteEstadoCodigos.Enviado;
        doc.EnviadoAt = null;
        var originalJson = doc.Json!.JsonDte;
        var originalJws = doc.Json.JsonFirmado;
        await db.SaveChangesAsync();
        var reception = Substitute.For<IHaciendaReceptionClient>();
        var auth = Auth();
        var service = Service(db, reception, auth);

        var result = await service.EnviarAsync(10, doc.Id, "audit");

        result.ErrorCode.Should().Be("DTE_RESULTADO_INCIERTO");
        result.Value!.Diagnostico!.RequiereConsultaHacienda.Should().BeTrue();
        db.ChangeTracker.Clear();
        var actual = await db.DteDocumentos.Include(d => d.Json).SingleAsync(d => d.Id == doc.Id);
        actual.EstadoCodigo.Should().Be(DteEstadoCodigos.Enviado);
        actual.EnviadoAt.Should().BeNull();
        actual.Json!.JsonDte.Should().Be(originalJson);
        actual.Json.JsonFirmado.Should().Be(originalJws);
        await reception.DidNotReceive().EnviarAsync(Arg.Any<HaciendaReceptionRequest>(), Arg.Any<CancellationToken>());
        await auth.DidNotReceive().AutenticarAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InternalNoteSavedWhileHttpIsInFlight_DoesNotDiscardProcessedAcknowledgement()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"note-during-http-{Guid.NewGuid():N}").Options;
        int id;
        await using (var seed = new NeoStpDbContext(options)) id = (await Seed(seed)).Id;
        await using var senderDb = new NeoStpDbContext(options);
        await using var noteDb = new NeoStpDbContext(options);
        var reception = Substitute.For<IHaciendaReceptionClient>();
        reception.EnviarAsync(Arg.Any<HaciendaReceptionRequest>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                var claimed = await noteDb.DteDocumentos.AsNoTracking().SingleAsync(d => d.Id == id);
                claimed.EstadoCodigo.Should().Be(DteEstadoCodigos.Enviado);
                claimed.EnviadoAt.Should().NotBeNull();
                var notes = Service(noteDb, Substitute.For<IHaciendaReceptionClient>(), Auth());
                var saved = await notes.GuardarNotaInternaAsync(10, id, "Nota sintética durante HTTP", "audit-note");
                saved.IsSuccess.Should().BeTrue();
                return new HaciendaReceptionResult {
                    Success = true, CodigoHttp = 200, Estado = "PROCESADO", SelloRecibido = "synthetic-ack",
                    Raw = """{"estado":"PROCESADO","selloRecibido":"synthetic-ack"}"""
                };
            });

        var result = await Service(senderDb, reception, Auth()).EnviarAsync(10, id, "audit-send");

        result.IsSuccess.Should().BeTrue("an operational note must not invalidate a valid fiscal acknowledgement");
        await using var verify = new NeoStpDbContext(options);
        var persisted = await verify.DteDocumentos.AsNoTracking().Include(d => d.Json).SingleAsync(d => d.Id == id);
        persisted.EstadoCodigo.Should().Be(DteEstadoCodigos.Procesado);
        persisted.SelloRecibido.Should().Be("synthetic-ack");
        persisted.Json!.RespuestaHacienda.Should().Contain("synthetic-ack");
        persisted.NotaInterna.Should().Be("Nota sintética durante HTTP");
        await reception.Received(1).EnviarAsync(Arg.Any<HaciendaReceptionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LocalInvalidationAfterTimeout_PreservesRequiredReconciliationAndDoesNotEmitWebhook()
    {
        await using var db = DteFiscalIsolationTests.Db();
        var doc = await Seed(db);
        var reception = Substitute.For<IHaciendaReceptionClient>();
        reception.EnviarAsync(Arg.Any<HaciendaReceptionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new HaciendaReceptionResult
            {
                CodigoHttp = 0, Estado = "CONTINGENCIA", ClasificaMsg = "TIMEOUT", DescripcionMsg = "synthetic timeout"
            });
        var webhooks = Substitute.For<IConnectWebhookDispatcher>();
        var service = Service(db, reception, Auth(), webhooks);

        var sent = await service.EnviarAsync(10, doc.Id, "audit");
        sent.ErrorCode.Should().Be("DTE_RESULTADO_INCIERTO");
        sent.Value!.EstadoCodigo.Should().Be(DteEstadoCodigos.Enviado);
        sent.Value.Diagnostico!.RequiereConsultaHacienda.Should().BeTrue();

        var invalidated = await service.InvalidarAsync(10, doc.Id, "synthetic", "audit");
        db.ChangeTracker.Clear();
        var persisted = await service.GetByIdAsync(10, doc.Id);

        invalidated.ErrorCode.Should().Be("DTE_RESULTADO_INCIERTO");
        persisted.Value!.EstadoCodigo.Should().Be(DteEstadoCodigos.Enviado);
        persisted.Value.Diagnostico!.RequiereConsultaHacienda.Should().BeTrue();
        persisted.Value.RespuestaHacienda.Should().Contain("TIMEOUT");
        await webhooks.DidNotReceive().DispatchAsync(Arg.Is<ConnectDteEventoPayload>(x =>
            x.Evento == ConnectEventos.DteInvalidado && x.DteId == doc.Id), Arg.Any<CancellationToken>());
        await reception.Received(1).EnviarAsync(Arg.Any<HaciendaReceptionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TwoConcurrentClaims_TransmitOnceAndPreserveProcessedState()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"multiagent-audit-{Guid.NewGuid():N}").Options;
        int id;
        await using (var seed = new NeoStpDbContext(options)) id = (await Seed(seed)).Id;
        await using var firstDb = new NeoStpDbContext(options);
        await using var secondDb = new NeoStpDbContext(options);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var bothPassedGuard = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseLateResponse = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var authCalls = 0;
        var receptionCalls = 0;
        var auth = Substitute.For<IHaciendaAuthClient>();
        auth.AutenticarAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                if (Interlocked.Increment(ref authCalls) == 2) bothPassedGuard.TrySetResult();
                await bothPassedGuard.Task.WaitAsync(deadline.Token);
                return new HaciendaAuthResult { Success = true, Token = "synthetic", ExpiresAt = DateTime.UtcNow.AddHours(1) };
            });
        var reception = Substitute.For<IHaciendaReceptionClient>();
        reception.EnviarAsync(Arg.Any<HaciendaReceptionRequest>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                if (Interlocked.Increment(ref receptionCalls) == 1)
                    return new HaciendaReceptionResult
                    {
                        Success = true, CodigoHttp = 200, Estado = "PROCESADO", SelloRecibido = "synthetic-sello",
                        Raw = """{"estado":"PROCESADO","selloRecibido":"synthetic-sello"}"""
                    };
                await releaseLateResponse.Task.WaitAsync(deadline.Token);
                return new HaciendaReceptionResult
                {
                    CodigoHttp = 200, Estado = "ERROR", CodigoMsg = "008",
                    Raw = """{"estado":"ERROR","codigoMsg":"008","descripcionMsg":"[emisor.codActividad] NO CORRESPONDE A CONTRIBUYENTE"}"""
                };
            });
        var first = Service(firstDb, reception, auth).EnviarAsync(10, id, "audit", deadline.Token);
        var second = Service(secondDb, reception, auth).EnviarAsync(10, id, "audit", deadline.Token);
        try
        {
            var completedFirst = await Task.WhenAny(first, second).WaitAsync(deadline.Token);
            // The losing claim may finish before the winning HTTP request.
            (await completedFirst).Should().NotBeNull();
        }
        finally { releaseLateResponse.TrySetResult(); }
        var results = await Task.WhenAll(first, second);
        await using var verify = new NeoStpDbContext(options);
        var persisted = await verify.DteDocumentos.AsNoTracking().SingleAsync(d => d.Id == id);

        receptionCalls.Should().Be(1, "only the winner of the durable claim may cross HTTP");
        results.Count(x => x.IsSuccess).Should().Be(1);
        results.Single(x => x.IsFailure).ErrorCode.Should().Be("DTE_CONCURRENCY_CONFLICT");
        persisted.EstadoCodigo.Should().Be(DteEstadoCodigos.Procesado);
        persisted.SelloRecibido.Should().Be("synthetic-sello");
        persisted.ProcesadoAt.Should().NotBeNull();
        (await verify.DteDocumentos.CountAsync()).Should().Be(1);
    }

    private static async Task<DteDocumento> Seed(NeoStpDbContext db)
    {
        db.Empresas.Add(new Empresa { Id = 10, Nit = "00000000000000", RazonSocial = "SYNTHETIC", Correo = "audit@example.invalid", Telefono = "22222222" });
        db.DteConfiguracion.Add(new() { EmpresaId = 10, AmbienteCodigo = "PRUEBAS", UsuarioMh = "fixture", PasswordMhCifrado = "fixture", CertificadoBlob = [1] });
        var doc = DteFiscalIsolationTests.Documento();
        doc.EstadoCodigo = DteEstadoCodigos.Firmado;
        doc.Json = new() { JsonDte = DteFiscalIsolationTests.Payload(doc), JsonFirmado = DteFiscalIsolationTests.Jws(DteFiscalIsolationTests.Payload(doc)) };
        db.DteDocumentos.Add(doc);
        await db.SaveChangesAsync();
        return doc;
    }

    private static IHaciendaAuthClient Auth()
    {
        var auth = Substitute.For<IHaciendaAuthClient>();
        auth.AutenticarAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new HaciendaAuthResult { Success = true, Token = "synthetic", ExpiresAt = DateTime.UtcNow.AddHours(1) });
        return auth;
    }

    private static DteDocumentosService Service(NeoStpDbContext db, IHaciendaReceptionClient reception,
        IHaciendaAuthClient auth, IConnectWebhookDispatcher? webhooks = null, IDteSignerService? signerOverride = null)
    {
        var generator = Substitute.For<IDteGeneratorService>();
        generator.Generar(Arg.Any<DteDocumento>(), Arg.Any<DteConfiguracion>())
            .Returns(x => Result<string>.Ok(DteFiscalIsolationTests.Payload(x.Arg<DteDocumento>())));
        var signer = signerOverride ?? Substitute.For<IDteSignerService>();
        signer.FirmarAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(x => new DteSignResult { Success = true, JsonFirmado = DteFiscalIsolationTests.Jws(x.ArgAt<string>(0)) });
        return new(db, new DteCalculator(), generator, signer, reception,
        Substitute.For<IHaciendaContingenciaClient>(), Substitute.For<IHaciendaEventoClient>(), auth,
        DteFiscalIsolationTests.Protector(), Substitute.For<IDtePdfService>(), Substitute.For<ITenantEmailSender>(),
        Substitute.For<IAuditoriaService>(), webhooks ?? Substitute.For<IConnectWebhookDispatcher>());
    }
}
