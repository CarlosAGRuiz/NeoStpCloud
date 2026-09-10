using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Comunicaciones;
using NeoSTP.Application.Connect;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Dte.Contingencia;
using NeoSTP.Domain.Core.Dte.Eventos;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Dte;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;

namespace NeoSTP.Tests.Unit.Dte;

// Independent attempt/evidence acceptance: real services and generator, EF InMemory,
// synthetic JWS and substituted Hacienda clients. No hosts, sockets, certificates or customer data.
public sealed class ContingenciaLoteAttemptAuditTests
{
    [Theory]
    [InlineData("03")]
    [InlineData("01")]
    [InlineData("")]
    [InlineData("01,invalid")]
    public async Task Unauthorized_type_blocks_the_whole_batch_before_auth_claim_or_transmission(string authorizedTypes)
    {
        await using var db = new NeoStpDbContext(DbOptions());
        var (document, evento) = await Seed(db);
        var second = DteFiscalIsolationTests.Documento();
        second.TipoDteCodigo = "03";
        second.NumeroControl = "DTE-03-M001P001-000000000000002";
        second.EstadoCodigo = DteEstadoCodigos.Firmado;
        second.Json = new() { JsonDte = DteFiscalIsolationTests.Payload(second), JsonFirmado = DteFiscalIsolationTests.Jws(DteFiscalIsolationTests.Payload(second)) };
        db.DteDocumentos.Add(second);
        await db.SaveChangesAsync();
        db.DteEventoDocumentosRelacionados.Add(new() { EventoId = evento.Id, DocumentoId = second.Id, RolCodigo = DteEventoRolCodigos.LoteContingencia });
        (await db.DteConfiguracion.SingleAsync()).TiposDteAutorizadosCsv = authorizedTypes;
        // A different company's wider allowance must not authorize this batch.
        db.DteConfiguracion.Add(new DteConfiguracion { EmpresaId = 99, TiposDteAutorizadosCsv = "01,03,11,14" });
        await db.SaveChangesAsync();
        var auth = Auth();
        var send = Substitute.For<IHaciendaLoteClient>();
        var service = new ContingenciaLoteService(db, send, Substitute.For<IHaciendaConsultaLoteClient>(), auth,
            DteFiscalIsolationTests.Protector(), NullLogger<ContingenciaLoteService>.Instance);

        var result = await service.CrearYEnviarLoteAsync(evento.Id, 10, "synthetic-operator");

        result.ErrorCode.Should().Be("DTE_TIPO_NO_AUTORIZADO");
        auth.ReceivedCalls().Should().BeEmpty();
        send.ReceivedCalls().Should().BeEmpty();
        (await db.DteContingenciaLotes.CountAsync()).Should().Be(0);
        (await db.DteContingenciaLoteDetalles.CountAsync()).Should().Be(0);
        document.EstadoCodigo.Should().Be(DteEstadoCodigos.Firmado);
        document.EnviadoAt.Should().BeNull();
        second.EstadoCodigo.Should().Be(DteEstadoCodigos.Firmado);
        second.EnviadoAt.Should().BeNull();
        db.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).Should().BeEmpty();
    }

    [Theory]
    [InlineData("RECHAZADO", null)]
    [InlineData("PROCESADO", "old-attempt-seal")]
    public async Task RejectedThenReallyRegeneratedDocumentRejectsResultsFromItsOldBatch(string oldState, string? oldSeal)
    {
        var options = DbOptions();
        int docId, eventId, loteId;
        string uuid, numero;
        var hooks = Substitute.For<IConnectWebhookDispatcher>();
        await using (var db = new NeoStpDbContext(options))
        {
            var seeded = await Seed(db);
            docId = seeded.Doc.Id; eventId = seeded.Evento.Id;
            uuid = seeded.Doc.CodigoGeneracion; numero = seeded.Doc.NumeroControl;
            var lote = await AttachLote(db, seeded.Doc, seeded.Evento);
            loteId = lote.Id;
            var rejected = await Service(db, query: Query(uuid, "RECHAZADO", null), hooks: hooks)
                .ConsultarLoteAsync(loteId, 10);
            rejected.IsSuccess.Should().BeTrue();
            seeded.Doc.EstadoCodigo.Should().Be(DteEstadoCodigos.Rechazado);
        }

        string newJson;
        DateTime? generation;
        int historyCount;
        await using (var regenerationDb = new NeoStpDbContext(options))
        {
            var generator = new DteGeneratorService(Options.Create(new TerritorialOptions()), new ConfigurationBuilder().Build());
            var documentService = new DteDocumentosService(regenerationDb, new DteCalculator(), generator,
                Substitute.For<IDteSignerService>(), Substitute.For<IHaciendaReceptionClient>(),
                Substitute.For<IHaciendaContingenciaClient>(), Substitute.For<IHaciendaEventoClient>(), Auth(),
                DteFiscalIsolationTests.Protector(), Substitute.For<IDtePdfService>(), Substitute.For<ITenantEmailSender>(),
                Substitute.For<IAuditoriaService>(), hooks);
            var generated = await documentService.GenerarAsync(10, docId, "synthetic-recovery-audit");
            generated.IsSuccess.Should().BeTrue("real regeneration must succeed: {0}", generated.Error);
            var current = await regenerationDb.DteDocumentos.AsNoTracking().Include(d => d.Json).SingleAsync();
            current.EstadoCodigo.Should().Be(DteEstadoCodigos.Generado);
            current.EnviadoAt.Should().BeNull();
            current.CodigoGeneracion.Should().Be(uuid);
            current.NumeroControl.Should().Be(numero);
            newJson = current.Json!.JsonDte;
            generation = current.GeneradoAt;
            generation.Should().NotBeNull();
            using var payload = JsonDocument.Parse(newJson);
            payload.RootElement.GetProperty("cuerpoDocumento").GetArrayLength().Should().Be(1,
                "this is the actual generator output, not a manually reset state or placeholder JSON");
            historyCount = await regenerationDb.DteErrorOcurrencias.CountAsync();
            historyCount.Should().BeGreaterThan(0, "the first rejected response must survive regeneration");
        }

        var priorNotifications = hooks.ReceivedCalls().Count();
        await using (var lateQueryDb = new NeoStpDbContext(options))
        {
            var late = await Service(lateQueryDb, query: Query(uuid, oldState, oldSeal), hooks: hooks)
                .ConsultarLoteAsync(loteId, 10);
            late.ErrorCode.Should().Be("DTE_LOTE_INTENTO_INCOMPATIBLE");
        }
        await using var verify = new NeoStpDbContext(options);
        var persisted = await verify.DteDocumentos.AsNoTracking().Include(d => d.Json).SingleAsync();
        persisted.EstadoCodigo.Should().Be(DteEstadoCodigos.Generado);
        persisted.EnviadoAt.Should().BeNull();
        persisted.SelloRecibido.Should().BeNull();
        persisted.ProcesadoAt.Should().BeNull();
        persisted.GeneradoAt.Should().Be(generation);
        persisted.Json!.JsonDte.Should().Be(newJson);
        persisted.Json.RespuestaHacienda.Should().BeNull();
        (await verify.DteErrorOcurrencias.CountAsync()).Should().Be(historyCount);
        var oldBatch = await verify.DteContingenciaLotes.AsNoTracking().Include(l => l.Detalles).SingleAsync();
        oldBatch.EventoContingenciaId.Should().Be(eventId);
        oldBatch.Detalles.Single().EstadoCodigo.Should().Be(DteContingenciaLoteEstados.Error);
        oldBatch.Detalles.Single().SelloRecibido.Should().BeNull();
        hooks.ReceivedCalls().Count().Should().Be(priorNotifications);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ContradictoryDuplicateResultsAreRejectedInEitherOrderWithoutPersistingAnyItem(bool rejectedFirst)
    {
        var options = DbOptions();
        int loteId;
        var hooks = Substitute.For<IConnectWebhookDispatcher>();
        await using (var db = new NeoStpDbContext(options))
        {
            var (doc, evento) = await Seed(db);
            var lote = await AttachLote(db, doc, evento);
            loteId = lote.Id;
            lote.RawConsulta = "previous-batch-evidence";
            doc.Json!.RespuestaHacienda = "previous-document-evidence";
            await db.SaveChangesAsync();
            var rejected = Item(doc.CodigoGeneracion, "RECHAZADO", null);
            var processed = Item(doc.CodigoGeneracion, "PROCESADO", "ambiguous-seal");
            var query = Substitute.For<IHaciendaConsultaLoteClient>();
            query.ConsultarLoteAsync(Arg.Any<HaciendaConsultaLoteRequest>(), Arg.Any<CancellationToken>())
                .Returns(new HaciendaConsultaLoteResult {
                    Success = true, CodigoHttp = 200, Raw = "ambiguous-duplicate-response",
                    Items = rejectedFirst ? [rejected, processed] : [processed, rejected]
                });

            var result = await Service(db, query: query, hooks: hooks).ConsultarLoteAsync(lote.Id, 10);
            result.ErrorCode.Should().Be("DTE_LOTE_EVIDENCIA_CONFLICTIVA");
        }
        await using var verify = new NeoStpDbContext(options);
        var persisted = await verify.DteDocumentos.AsNoTracking().Include(d => d.Json).SingleAsync();
        persisted.EstadoCodigo.Should().Be(DteEstadoCodigos.Enviado);
        persisted.SelloRecibido.Should().BeNull();
        persisted.ProcesadoAt.Should().BeNull();
        persisted.Json!.RespuestaHacienda.Should().Be("previous-document-evidence");
        var oldBatch = await verify.DteContingenciaLotes.AsNoTracking().Include(l => l.Detalles).SingleAsync(l => l.Id == loteId);
        oldBatch.EstadoCodigo.Should().Be(DteContingenciaLoteEstados.Enviado);
        oldBatch.RawConsulta.Should().Be("previous-batch-evidence");
        oldBatch.UltimaConsultaAt.Should().BeNull();
        oldBatch.Detalles.Single().EstadoCodigo.Should().Be(DteContingenciaLoteEstados.Pendiente);
        oldBatch.Detalles.Single().SelloRecibido.Should().BeNull();
        hooks.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task LostTransportResponseWithoutRawPersistsCauseAndBlocksReplayAfterNewContext()
    {
        var options = DbOptions();
        var send = Substitute.For<IHaciendaLoteClient>();
        send.EnviarLoteAsync(Arg.Any<HaciendaLoteRequest>(), Arg.Any<CancellationToken>())
            .Returns(new HaciendaLoteResult {
                Success = false, CodigoHttp = 0, CodigoMsg = "NETWORK_ERROR", Estado = "ENVIADO",
                DescripcionMsg = "Synthetic lost transport response", Raw = null
            });
        int eventId, loteId;
        await using (var db = new NeoStpDbContext(options))
        {
            var (_, evento) = await Seed(db);
            eventId = evento.Id;
            var sent = await Service(db, send: send).CrearYEnviarLoteAsync(eventId, 10, "synthetic-audit");
            sent.ErrorCode.Should().Be("DTE_RESULTADO_INCIERTO");
            loteId = sent.Value!.LoteId;
        }
        await using var verify = new NeoStpDbContext(options);
        var lote = await verify.DteContingenciaLotes.AsNoTracking().SingleAsync();
        var document = await verify.DteDocumentos.AsNoTracking().SingleAsync();
        lote.EstadoCodigo.Should().Be(DteContingenciaLoteEstados.Enviado);
        lote.CodigoLote.Should().BeNull();
        lote.Intentos.Should().Be(1);
        document.EstadoCodigo.Should().Be(DteEstadoCodigos.Enviado);
        document.EnviadoAt.Should().Be(lote.EnviadoAt).And.NotBeNull();
        using var raw = JsonDocument.Parse(lote.RawEnvio!);
        raw.RootElement.GetProperty("codigoHttp").GetInt32().Should().Be(0);
        raw.RootElement.GetProperty("codigoMsg").GetString().Should().Be("NETWORK_ERROR");
        raw.RootElement.GetProperty("descripcionMsg").GetString().Should().Be("Synthetic lost transport response");
        raw.RootElement.GetProperty("resultadoIncierto").GetBoolean().Should().BeTrue();
        var replay = await Service(verify, send: send).CrearYEnviarLoteAsync(eventId, 10, "synthetic-replay");
        replay.ErrorCode.Should().Be("DTE_RESULTADO_INCIERTO");
        replay.Value!.LoteId.Should().Be(loteId);
        await send.Received(1).EnviarLoteAsync(Arg.Any<HaciendaLoteRequest>(), Arg.Any<CancellationToken>());
        (await verify.DteContingenciaLotes.CountAsync()).Should().Be(1);
    }

    private static DbContextOptions<NeoStpDbContext> DbOptions() => new DbContextOptionsBuilder<NeoStpDbContext>()
        .UseInMemoryDatabase("lote-attempt-audit-" + Guid.NewGuid()).Options;

    private static async Task<(DteDocumento Doc, DteEvento Evento)> Seed(NeoStpDbContext db)
    {
        db.Empresas.Add(new Empresa {
            Id = 10, Nit = "00000000000000", Nrc = "1234567", RazonSocial = "SYNTHETIC AUDIT",
            CodigoActividad = "62010", ActividadEconomica = "Programacion informatica",
            Departamento = "06", Municipio = "23", Distrito = "01",
            Direccion = "Direccion sintetica sin cliente real", Correo = "audit@example.invalid", Telefono = "22222222"
        });
        db.DteConfiguracion.Add(new() {
            EmpresaId = 10, AmbienteCodigo = DteAmbientes.Pruebas, UsuarioMh = "synthetic", PasswordMhCifrado = "synthetic",
            TipoEstablecimientoCodigo = "02", CodigoEstablecimientoMh = "M001", CodigoPuntoVentaMh = "P001"
        });
        var doc = DteFiscalIsolationTests.Documento();
        doc.EstadoCodigo = DteEstadoCodigos.Firmado;
        doc.GeneradoAt = DateTime.UtcNow.AddMinutes(-10);
        doc.Detalles.Add(new DteDocumentoDetalle {
            NumeroLinea = 1, TipoItem = 2, Codigo = "SYNTHETIC-01", Descripcion = "Servicio de prueba de recuperacion",
            Cantidad = 1, PrecioUnitario = 100, VentaGravada = 100, UnidadMedidaCodigo = "99"
        });
        doc.Json = new() { JsonDte = DteFiscalIsolationTests.Payload(doc), JsonFirmado = DteFiscalIsolationTests.Jws(DteFiscalIsolationTests.Payload(doc)) };
        var evento = new DteEvento {
            EmpresaId = 10, AmbienteCodigo = DteAmbientes.Pruebas, TipoEventoCodigo = TipoEventoCodigos.Contingencia,
            EstadoCodigo = DteEventoEstadoCodigos.Procesado, SelloRecibido = "synthetic-event", CodigoGeneracion = Guid.NewGuid().ToString()
        };
        db.AddRange(doc, evento);
        await db.SaveChangesAsync();
        db.DteEventoDocumentosRelacionados.Add(new() { EventoId = evento.Id, DocumentoId = doc.Id, RolCodigo = DteEventoRolCodigos.LoteContingencia });
        await db.SaveChangesAsync();
        return (doc, evento);
    }

    private static async Task<DteContingenciaLote> AttachLote(NeoStpDbContext db, DteDocumento doc, DteEvento evento)
    {
        doc.EstadoCodigo = DteEstadoCodigos.Enviado;
        doc.EnviadoAt = DateTime.UtcNow.AddMinutes(-5);
        var lote = new DteContingenciaLote {
            EmpresaId = 10, EventoContingenciaId = evento.Id, AmbienteCodigo = DteAmbientes.Pruebas,
            EstadoCodigo = DteContingenciaLoteEstados.Enviado, CodigoLote = "synthetic-batch", EnviadoAt = doc.EnviadoAt, Intentos = 1,
            Detalles = [new() { DteDocumentoId = doc.Id, CodigoGeneracion = doc.CodigoGeneracion, TipoDteCodigo = doc.TipoDteCodigo }]
        };
        db.Add(lote);
        await db.SaveChangesAsync();
        return lote;
    }

    private static HaciendaConsultaLoteItemResult Item(string uuid, string state, string? seal) => new() {
        CodigoGeneracion = uuid, Estado = state, SelloRecibido = seal,
        CodigoMsg = state == "RECHAZADO" ? "096" : "001", DescripcionMsg = "Synthetic batch result"
    };

    private static IHaciendaConsultaLoteClient Query(string uuid, string state, string? seal)
    {
        var query = Substitute.For<IHaciendaConsultaLoteClient>();
        query.ConsultarLoteAsync(Arg.Any<HaciendaConsultaLoteRequest>(), Arg.Any<CancellationToken>())
            .Returns(new HaciendaConsultaLoteResult { Success = true, CodigoHttp = 200, Raw = "synthetic-query", Items = [Item(uuid, state, seal)] });
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
        IHaciendaConsultaLoteClient? query = null, IConnectWebhookDispatcher? hooks = null)
        => new(db, send ?? Substitute.For<IHaciendaLoteClient>(), query ?? Substitute.For<IHaciendaConsultaLoteClient>(),
            Auth(), DteFiscalIsolationTests.Protector(), NullLogger<ContingenciaLoteService>.Instance, hooks);
}
