using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NeoSTP.Application.Connect;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Dte.Contingencia;
using NeoSTP.Domain.Core.Dte.Eventos;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Dte;

// These tests exercise state/evidence rules. Atomic multi-document rollback is verified on SQL separately.
public class ContingenciaLoteSafetyTests
{
    [Theory]
    [InlineData("PROCESADO", false)]
    [InlineData("INVALIDADO", false)]
    [InlineData("ENVIADO", false)]
    [InlineData("ERROR", false)]
    [InlineData("CONTINGENCIA", true)]
    [InlineData("FIRMADO", true)]
    public async Task IneligibleDocumentNeverContactsAuthOrTransport(string state, bool attempted)
    {
        await using var db = DteFiscalIsolationTests.Db();
        var (doc, evento) = await Seed(db);
        doc.EstadoCodigo = state;
        if (attempted) doc.EnviadoAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        var send = Substitute.For<IHaciendaLoteClient>();
        var auth = Auth();
        var result = await Service(db, send, auth: auth).CrearYEnviarLoteAsync(evento.Id, 10, "test");
        result.ErrorCode.Should().Be("DTE_LOTE_ESTADO_INVALIDO");
        send.ReceivedCalls().Should().BeEmpty();
        auth.ReceivedCalls().Should().BeEmpty();
        (await db.DteContingenciaLotes.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task LostResponseLeavesDurableClaimAndReplayDoesNotTransmit()
    {
        await using var db = DteFiscalIsolationTests.Db();
        var (doc, evento) = await Seed(db);
        var send = Substitute.For<IHaciendaLoteClient>();
        send.EnviarLoteAsync(Arg.Any<HaciendaLoteRequest>(), Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            var claimed = await db.DteDocumentos.AsNoTracking().SingleAsync();
            claimed.EstadoCodigo.Should().Be(DteEstadoCodigos.Enviado);
            claimed.EnviadoAt.Should().NotBeNull();
            (await db.DteContingenciaLotes.AsNoTracking().SingleAsync()).Intentos.Should().Be(1);
            return new HaciendaLoteResult { CodigoHttp = 0, CodigoMsg = "TIMEOUT", Raw = "synthetic-timeout" };
        });
        var service = Service(db, send);
        var first = await service.CrearYEnviarLoteAsync(evento.Id, 10, "test");
        var replay = await service.CrearYEnviarLoteAsync(evento.Id, 10, "test");
        first.ErrorCode.Should().Be("DTE_RESULTADO_INCIERTO");
        replay.ErrorCode.Should().Be(first.ErrorCode);
        replay.Value!.LoteId.Should().Be(first.Value!.LoteId);
        (await service.ReintentarDocumentoAsync(doc.Id, 10)).ErrorCode.Should().Be("DTE_RESULTADO_INCIERTO");
        await send.Received(1).EnviarLoteAsync(Arg.Any<HaciendaLoteRequest>(), Arg.Any<CancellationToken>());
        (await db.DteContingenciaLotes.SingleAsync()).RawEnvio.Should().Be("synthetic-timeout");
    }

    [Theory]
    [InlineData("RECIBIDO", null)]
    [InlineData("ERROR", null)]
    [InlineData("PROCESADO", null)]
    [InlineData("RECIBIDO", "isolated-seal")]
    public async Task NonterminalEvidenceKeepsPollingWithoutMarkingRejected(string state, string? seal)
    {
        await using var db = DteFiscalIsolationTests.Db();
        var (doc, evento) = await Seed(db);
        var lote = await Lote(db, doc, evento);
        var query = Query(doc, state, seal);
        var result = await Service(db, query: query).ConsultarLoteAsync(lote.Id, 10);
        result.IsSuccess.Should().BeTrue();
        result.Value!.EstadoCodigo.Should().Be(DteContingenciaLoteEstados.Enviado);
        lote.Detalles.Single().EstadoCodigo.Should().Be(DteContingenciaLoteEstados.Pendiente);
        doc.EstadoCodigo.Should().Be(DteEstadoCodigos.Enviado);
        doc.SelloRecibido.Should().BeNull();
    }

    [Fact]
    public async Task FailedQueryCannotTurnAnAcceptedBatchIntoError()
    {
        await using var db = DteFiscalIsolationTests.Db();
        var (doc, evento) = await Seed(db);
        var lote = await Lote(db, doc, evento);
        var query = Substitute.For<IHaciendaConsultaLoteClient>();
        query.ConsultarLoteAsync(Arg.Any<HaciendaConsultaLoteRequest>(), Arg.Any<CancellationToken>())
            .Returns(new HaciendaConsultaLoteResult { CodigoHttp = 503, Raw = "synthetic-failure" });
        var result = await Service(db, query: query).ConsultarLoteAsync(lote.Id, 10);
        result.IsFailure.Should().BeTrue();
        lote.EstadoCodigo.Should().Be(DteContingenciaLoteEstados.Enviado);
        lote.RawConsulta.Should().Be("synthetic-failure");
        doc.EstadoCodigo.Should().Be(DteEstadoCodigos.Enviado);
    }

    [Fact]
    public async Task ConfirmedSuccessPersistsIndividualEvidenceAndOnlyNotifiesOnce()
    {
        await using var db = DteFiscalIsolationTests.Db();
        var (doc, evento) = await Seed(db);
        var lote = await Lote(db, doc, evento);
        var hooks = Substitute.For<IConnectWebhookDispatcher>();
        var service = Service(db, query: Query(doc, "PROCESADO", "confirmed"), hooks: hooks);
        (await service.ConsultarLoteAsync(lote.Id, 10)).IsSuccess.Should().BeTrue();
        (await service.ConsultarLoteAsync(lote.Id, 10)).IsSuccess.Should().BeTrue();
        doc.EstadoCodigo.Should().Be(DteEstadoCodigos.Procesado);
        doc.ProcesadoAt.Should().NotBeNull();
        doc.Json!.RespuestaHacienda.Should().Contain("confirmed").And.Contain("codigoLote");
        lote.EstadoCodigo.Should().Be(DteContingenciaLoteEstados.Procesado);
        await hooks.Received(1).DispatchAsync(Arg.Is<ConnectDteEventoPayload>(p => p.DteId == doc.Id), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("INVALIDADO", null)]
    [InlineData("PROCESADO", "older-confirmed")]
    public async Task ConsultationCannotOverwriteTerminalEvidence(string state, string? seal)
    {
        await using var db = DteFiscalIsolationTests.Db();
        var (doc, evento) = await Seed(db);
        var lote = await Lote(db, doc, evento);
        doc.EstadoCodigo = state;
        doc.SelloRecibido = seal;
        doc.Json!.RespuestaHacienda = "original-evidence";
        await db.SaveChangesAsync();
        var result = await Service(db, query: Query(doc, "PROCESADO", "different")).ConsultarLoteAsync(lote.Id, 10);
        result.ErrorCode.Should().Be("DTE_LOTE_EVIDENCIA_CONFLICTIVA");
        var persisted = await db.DteDocumentos.AsNoTracking().Include(d => d.Json).SingleAsync();
        persisted.EstadoCodigo.Should().Be(state);
        persisted.SelloRecibido.Should().Be(seal);
        persisted.Json!.RespuestaHacienda.Should().Be("original-evidence");
    }

    [Fact]
    public async Task AllRejectedIsNotReportedAsCompletelyProcessed()
    {
        await using var db = DteFiscalIsolationTests.Db();
        var (doc, evento) = await Seed(db);
        var lote = await Lote(db, doc, evento);
        var result = await Service(db, query: Query(doc, "RECHAZADO", null)).ConsultarLoteAsync(lote.Id, 10);
        result.Value!.EstadoCodigo.Should().Be(DteContingenciaLoteEstados.Error);
        result.Value.Mensaje.Should().Contain("rechazados");
        doc.EstadoCodigo.Should().Be(DteEstadoCodigos.Rechazado);
        doc.ProcesadoAt.Should().BeNull();
    }

    private static async Task<(DteDocumento, DteEvento)> Seed(NeoStpDbContext db)
    {
        db.Empresas.Add(new Empresa { Id = 10, Nit = "00000000000000", RazonSocial = "SYNTHETIC" });
        db.DteConfiguracion.Add(new() { EmpresaId = 10, AmbienteCodigo = "PRUEBAS", UsuarioMh = "fixture", PasswordMhCifrado = "fixture" });
        var doc = DteFiscalIsolationTests.Documento();
        doc.EstadoCodigo = DteEstadoCodigos.Firmado;
        doc.Json = new() { JsonDte = DteFiscalIsolationTests.Payload(doc), JsonFirmado = DteFiscalIsolationTests.Jws(DteFiscalIsolationTests.Payload(doc)) };
        var evento = new DteEvento { EmpresaId = 10, AmbienteCodigo = "PRUEBAS", TipoEventoCodigo = TipoEventoCodigos.Contingencia,
            EstadoCodigo = DteEventoEstadoCodigos.Procesado, SelloRecibido = "synthetic-event", CodigoGeneracion = Guid.NewGuid().ToString() };
        db.AddRange(doc, evento);
        await db.SaveChangesAsync();
        db.DteEventoDocumentosRelacionados.Add(new() { EventoId = evento.Id, DocumentoId = doc.Id, RolCodigo = DteEventoRolCodigos.LoteContingencia });
        await db.SaveChangesAsync();
        return (doc, evento);
    }

    private static async Task<DteContingenciaLote> Lote(NeoStpDbContext db, DteDocumento doc, DteEvento evento)
    {
        doc.EstadoCodigo = DteEstadoCodigos.Enviado;
        doc.EnviadoAt = DateTime.UtcNow;
        var lote = new DteContingenciaLote { EmpresaId = 10, EventoContingenciaId = evento.Id, AmbienteCodigo = "PRUEBAS",
            EstadoCodigo = DteContingenciaLoteEstados.Enviado, CodigoLote = "synthetic-batch", EnviadoAt = doc.EnviadoAt, Intentos = 1,
            Detalles = [new() { DteDocumentoId = doc.Id, CodigoGeneracion = doc.CodigoGeneracion, TipoDteCodigo = doc.TipoDteCodigo }] };
        db.Add(lote); await db.SaveChangesAsync(); return lote;
    }

    private static IHaciendaConsultaLoteClient Query(DteDocumento doc, string state, string? seal)
    {
        var query = Substitute.For<IHaciendaConsultaLoteClient>();
        query.ConsultarLoteAsync(Arg.Any<HaciendaConsultaLoteRequest>(), Arg.Any<CancellationToken>())
            .Returns(new HaciendaConsultaLoteResult { Success = true, CodigoHttp = 200, Raw = "synthetic-query",
                Items = [new() { CodigoGeneracion = doc.CodigoGeneracion, Estado = state, SelloRecibido = seal }] });
        return query;
    }

    private static IHaciendaAuthClient Auth()
    {
        var auth = Substitute.For<IHaciendaAuthClient>();
        auth.AutenticarAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new HaciendaAuthResult { Success = true, Token = "synthetic", ExpiresAt = DateTime.UtcNow.AddHours(1) });
        return auth;
    }

    private static ContingenciaLoteService Service(NeoStpDbContext db, IHaciendaLoteClient? send = null,
        IHaciendaConsultaLoteClient? query = null, IHaciendaAuthClient? auth = null, IConnectWebhookDispatcher? hooks = null)
        => new(db, send ?? Substitute.For<IHaciendaLoteClient>(), query ?? Substitute.For<IHaciendaConsultaLoteClient>(),
            auth ?? Auth(), DteFiscalIsolationTests.Protector(), NullLogger<ContingenciaLoteService>.Instance, hooks);
}
