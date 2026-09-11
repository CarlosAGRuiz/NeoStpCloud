using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;
using System.Text.Json;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Inventario;
using NeoSTP.Application.Pos;
using NeoSTP.Application.Pos.Dtos;
using NeoSTP.Domain.Core.Pos;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Comunicaciones;
using NeoSTP.Application.Connect;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Application.Dte.Dtos;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Dte.Contingencia;
using NeoSTP.Domain.Core.Dte.Eventos;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Connect;
using NeoSTP.Infrastructure.Dte;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;

// No app startup, customer connection strings, credentials or real MH calls.
// --migration-chain applies migrations exclusively to the random synthetic database below.
const string connectionVariable = "NEOSTP_SQLSERVER_TEST_CONNECTION";
const string localDbServer = @"(localdb)\NeoStpAuthAudit_20260904";
var configuredRoot = Environment.GetEnvironmentVariable(connectionVariable);
var rootBuilder = string.IsNullOrWhiteSpace(configuredRoot)
    ? new SqlConnectionStringBuilder { DataSource = localDbServer, IntegratedSecurity = true }
    : new SqlConnectionStringBuilder(configuredRoot);
var server = rootBuilder.DataSource;
var suffix = Guid.NewGuid().ToString("N");
var database = "NeoStpDteAudit_" + suffix;
rootBuilder.InitialCatalog = database;
rootBuilder.TrustServerCertificate = true;
rootBuilder.ConnectTimeout = 10;
var connection = rootBuilder.ConnectionString;
NeoStpDbContext Db(params IInterceptor[] interceptors) => new(new DbContextOptionsBuilder<NeoStpDbContext>()
    .UseSqlServer(connection, sql => sql.EnableRetryOnFailure()).AddInterceptors(interceptors).Options);
var checks = new List<string>();
void Check(bool value, string name)
{
    if (!value) throw new InvalidOperationException("FAIL: " + name);
    checks.Add(name);
    Console.WriteLine("PASS: " + name);
}
var protector = Substitute.For<ISecretProtector>();
protector.Protect(Arg.Any<string>()).Returns(x => (string)x[0]);
protector.Unprotect(Arg.Any<string>()).Returns(x => (string)x[0]);
DteDocumentosService Service(NeoStpDbContext db) => new(db, new DteCalculator(),
    Substitute.For<IDteGeneratorService>(), Substitute.For<IDteSignerService>(),
    Substitute.For<IHaciendaReceptionClient>(), Substitute.For<IHaciendaContingenciaClient>(),
    Substitute.For<IHaciendaEventoClient>(), Substitute.For<IHaciendaAuthClient>(), protector,
    Substitute.For<IDtePdfService>(), Substitute.For<ITenantEmailSender>(),
    Substitute.For<IAuditoriaService>(), Substitute.For<IConnectWebhookDispatcher>());

var completed = false;
string[] migrations = [];
await using var schema = Db();
try
{
    if (args.Contains("--migration-chain"))
    {
        await schema.Database.MigrateAsync();
        migrations = (await schema.Database.GetAppliedMigrationsAsync()).ToArray();
        Check(!(await schema.Database.GetPendingMigrationsAsync()).Any(), "Full migration chain on empty synthetic SQL database");
    }
    else await schema.Database.EnsureCreatedAsync();
    Console.WriteLine("Isolated SQL database: " + database);
    var empresa = new Empresa { Nit = "00000000000000", RazonSocial = "SYNTHETIC DTE AUDIT",
        Nrc = "1234567", CodigoActividad = "62010", ActividadEconomica = "Programacion informatica",
        Departamento = "06", Municipio = "23", Distrito = "01", Direccion = "Direccion sintetica de auditoria SQL",
        Correo = "audit@example.invalid", Telefono = "22222222" };
    schema.Empresas.Add(empresa); await schema.SaveChangesAsync();
    var config = new DteConfiguracion { EmpresaId = empresa.Id, UsuarioMh = "synthetic",
        PasswordMhCifrado = "test-password", AmbienteCodigo = DteAmbientes.Pruebas,
        TipoEstablecimientoCodigo = "02", CodigoEstablecimientoMh = "M001", CodigoPuntoVentaMh = "P001",
        CertificadoBlob = [1, 2, 3] };
    schema.DteConfiguracion.Add(config);
    schema.DteCorrelativos.Add(new DteCorrelativo { EmpresaId = empresa.Id, TipoDteCodigo = "01", UltimoCorrelativo = 5 });
    var historical = new DteDocumento { EmpresaId = empresa.Id, TipoDteCodigo = "01",
        NumeroControl = "DTE-01-M001P001-000000000000900", CodigoGeneracion = Guid.NewGuid().ToString().ToUpperInvariant(),
        AmbienteCodigo = DteAmbientes.Pruebas, EstadoCodigo = DteEstadoCodigos.Procesado,
        FechaEmision = new DateTime(2026, 9, 1), TotalGravada = 900, TotalPagar = 900 };
    schema.DteDocumentos.Add(historical);
    await schema.SaveChangesAsync();

    async Task<int> Reserve(bool rollback = false)
    {
        await using var db = Db();
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            var number = await DteCorrelativoAllocator.NextAsync(db, empresa.Id, "01", default);
            if (rollback) await transaction.RollbackAsync(); else await transaction.CommitAsync();
            return number;
        });
    }
    var numbers = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Reserve()));
    Check(numbers.Distinct().Count() == 12 && numbers.Min() == 901 && numbers.Max() == 912,
        "Concurrent reservations unique and above historical maximum despite lowered counter");
    Check(await Reserve(rollback: true) == 913 && await Reserve() == 913,
        "Rolled-back reservation does not advance persisted counter");

    // Service-level path: execution strategy + company applock, also with optional license guard absent.
    var created = await Task.WhenAll(Enumerable.Range(0, 6).Select(async _ =>
    {
        await using var db = Db();
        return await Service(db).CreateBorradorAsync(empresa.Id, new CreateDteDocumentoRequest
        {
            ReceptorManual = new ReceptorDto { Nombre = "SYNTHETIC" },
            Lineas = [new() { Codigo = "TEST", Descripcion = "SYNTHETIC", Cantidad = 1, PrecioUnitario = 100, TipoItem = 1,
                UnidadMedidaCodigo = "59", Clasificacion = "GRAVADA" }]
        }, "isolated-test");
    }));
    Check(created.All(x => x.IsSuccess), "Concurrent real CreateBorrador succeeds: " + string.Join("; ", created.Where(x => x.IsFailure).Select(x => x.Error)));
    Check(created.Select(x => x.Value!.NumeroControl).Distinct().Count() == 6,
        "Concurrent creation persists six different control numbers");
    Check(created.All(x => x.Value!.AmbienteCodigo == DteAmbientes.Pruebas), "Creation snapshots configured environment");

    // A settings change must not reset numbering or change the document already stored.
    await schema.DteConfiguracion.Where(c => c.Id == config.Id)
        .ExecuteUpdateAsync(s => s.SetProperty(c => c.AmbienteCodigo, DteAmbientes.Produccion));
    Check(await Reserve() == 920, "Environment switch preserves historical global sequence");
    await using (var db = Db())
    {
        var result = await Service(db).GenerarAsync(empresa.Id, created[0].Value!.Id, "test");
        Check(result.ErrorCode == "DTE_AMBIENTE_INCOMPATIBLE", "Real SQL document blocked after config environment switch");
    }

    // Late auth response must not overwrite credentials or cached token of new environment.
    var rejected = await HaciendaTokenCache.StoreAsync(schema, config, protector, "old-test-token", DateTime.UtcNow.AddHours(1), default);
    Check(!rejected, "SQL compare-and-set rejects late token from previous environment");
    await using (var db = Db())
    {
        var current = await db.DteConfiguracion.SingleAsync();
        Check(current.TokenMhCifrado is null, "Late token was not persisted");
        Check(await HaciendaTokenCache.StoreAsync(db, current, protector, "Bearer prod-token", DateTime.UtcNow.AddHours(1), default),
            "Matching context can persist token");
        current.UpdatedBy = "test";
        await db.SaveChangesAsync();
    }
    await using (var db = Db())
    {
        var current = await db.DteConfiguracion.SingleAsync();
        Check(HaciendaTokenCache.Read(current, protector) == "prod-token", "Bound token survives tracked save and reload");
    }
    var production = new DteDocumento { EmpresaId = empresa.Id, TipoDteCodigo = "01",
        NumeroControl = "DTE-01-M001P001-000000000000921", CodigoGeneracion = Guid.NewGuid().ToString().ToUpperInvariant(),
        AmbienteCodigo = DteAmbientes.Produccion, EstadoCodigo = DteEstadoCodigos.Procesado,
        FechaEmision = new DateTime(2026, 9, 1), TotalGravada = 113, TotalPagar = 113 };
    schema.DteDocumentos.Add(production);
    await schema.SaveChangesAsync();
    var report = await new ReporteFiscalService(schema).LibroVentasConsumidorAsync(empresa.Id, 2026, 9);
    Check(report.Value!.Filas.Sum(x => x.GravadasConIva) == 113, "Fiscal SQL report excludes processed test documents");
    Check((await schema.DteDocumentos.AsNoTracking().SingleAsync(d => d.Id == historical.Id)).NumeroControl.EndsWith("900"),
        "Historical document number unchanged");

    CreateDteDocumentoRequest Request(string? key) => new()
    {
        IdempotencyKey = key, ReceptorManual = new() { Nombre = "SYNTHETIC" },
        Lineas = [new() { Codigo = "TEST", Descripcion = "SYNTHETIC", Cantidad = 1, PrecioUnitario = 100 }]
    };
    var retries = await Task.WhenAll(Enumerable.Range(0, 12).Select(async _ =>
    {
        await using var db = Db();
        return await Service(db).CreateBorradorAsync(empresa.Id, Request("same-sale"), "test");
    }));
    Check(retries.All(r => r.IsSuccess) && retries.Select(r => r.Value!.Id).Distinct().Count() == 1
        && retries.Count(r => !r.Value!.IdempotencyReplayed) == 1,
        "12 concurrent requests with same key create exactly one DTE");
    var retryId = retries[0].Value!.Id;
    await using (var db = Db())
    {
        var before = await db.DteCorrelativos.AsNoTracking().SingleAsync();
        var replay = await new ConnectDteService(Service(db)).EmitirAsync(empresa.Id, Request("same-sale"), "retry");
        Check(replay.Value!.Id == retryId && replay.Value.IdempotencyReplayed,
            "New service/context returns existing document without running emission pipeline");
        var after = await db.DteCorrelativos.AsNoTracking().SingleAsync();
        Check(before.UltimoCorrelativo == after.UltimoCorrelativo, "Replay does not consume another control number");
        var changed = Request("same-sale"); changed.Lineas[0].PrecioUnitario = 101;
        var conflict = await Service(db).CreateBorradorAsync(empresa.Id, changed, "retry");
        Check(conflict.ErrorCode == "IDEMPOTENCY_CONFLICT", "Same key with changed amount is a conflict");
    }
    var another = new Empresa { Nit = "11111111111111", RazonSocial = "SYNTHETIC SECOND TENANT" };
    schema.Empresas.Add(another); await schema.SaveChangesAsync();
    schema.DteConfiguracion.Add(new DteConfiguracion { EmpresaId = another.Id });
    await schema.SaveChangesAsync();
    await using (var db = Db())
    {
        var isolated = await Service(db).CreateBorradorAsync(another.Id, Request("same-sale"), "test");
        Check(isolated.IsSuccess && isolated.Value!.Id != retryId, "Same key is isolated by tenant");
    }
    await schema.DteConfiguracion.Where(c => c.EmpresaId == empresa.Id)
        .ExecuteUpdateAsync(s => s.SetProperty(c => c.AmbienteCodigo, DteAmbientes.Pruebas));
    await using (var db = Db())
    {
        var changedEnvironment = await Service(db).CreateBorradorAsync(empresa.Id, Request("same-sale"), "test");
        Check(changedEnvironment.ErrorCode == "DTE_AMBIENTE_INCOMPATIBLE" && changedEnvironment.Value!.Id == retryId,
            "Same key after environment switch cannot create a second document");
    }

    // Fault injection: real SQL reservation; simulated fiscal stages/transport never contact MH.
    var sendCalls = 0;
    IDteDocumentosService FaultingPipeline(NeoStpDbContext db, bool failGeneration = false)
    {
        var core = Service(db);
        var pipeline = Substitute.For<IDteDocumentosService>();
        pipeline.CreateBorradorAsync(Arg.Any<int>(), Arg.Any<CreateDteDocumentoRequest>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(c => core.CreateBorradorAsync(c.ArgAt<int>(0), c.ArgAt<CreateDteDocumentoRequest>(1), c.ArgAt<string?>(2), c.ArgAt<CancellationToken>(3)));
        pipeline.GetByIdAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(c => core.GetByIdAsync(c.ArgAt<int>(0), c.ArgAt<int>(1), c.ArgAt<CancellationToken>(2)));
        async Task<Result<DteDocumentoDto>> Stage(int eid, int id, string state)
        {
            var d = await db.DteDocumentos.SingleAsync(x => x.Id == id && x.EmpresaId == eid);
            d.EstadoCodigo = state; await db.SaveChangesAsync();
            return await core.GetByIdAsync(eid, id);
        }
        pipeline.GenerarAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(c => failGeneration ? Task.FromResult(Result<DteDocumentoDto>.Fail("Synthetic failure after creation", "VALIDATION"))
                : Stage(c.ArgAt<int>(0), c.ArgAt<int>(1), DteEstadoCodigos.Generado));
        pipeline.ValidarAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(c => Stage(c.ArgAt<int>(0), c.ArgAt<int>(1), DteEstadoCodigos.Validado));
        pipeline.FirmarAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(c => Stage(c.ArgAt<int>(0), c.ArgAt<int>(1), DteEstadoCodigos.Firmado));
        pipeline.EnviarAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_ => { Interlocked.Increment(ref sendCalls); return Task.FromException<Result<DteDocumentoDto>>(new TimeoutException("Synthetic lost response")); });
        return pipeline;
    }
    await using (var db = Db())
    {
        try { await new ConnectDteService(FaultingPipeline(db)).EmitirAsync(empresa.Id, Request("lost-response"), "test"); }
        catch (TimeoutException) { }
    }
    await using (var db = Db())
    {
        var recovered = await new ConnectDteService(FaultingPipeline(db)).EmitirAsync(empresa.Id, Request("lost-response"), "retry");
        Check(recovered.IsSuccess && recovered.Value!.IdempotencyReplayed && recovered.Value.EstadoCodigo == DteEstadoCodigos.Firmado && sendCalls == 1,
            "Lost transport response: retry returns same signed DTE and does not transmit again");
    }
    var sale = new VentaPos { EmpresaId = empresa.Id, Numero = "POS-SYNTHETIC",
        Lineas = [new VentaPosLinea { Codigo = "TEST", Descripcion = "SYNTHETIC", Cantidad = 1, PrecioUnitario = 100, Total = 100 }] };
    schema.VentasPos.Add(sale); await schema.SaveChangesAsync();
    PosService Pos(NeoStpDbContext db) => new(db, Substitute.For<IAuditoriaService>(), Substitute.For<ITicketPdfService>(),
        Substitute.For<ITenantEmailSender>(), new ConnectDteService(FaultingPipeline(db, failGeneration: true)),
        Substitute.For<IInventarioService>(), Options.Create(new PosOptions()));
    int linkedId;
    await using (var db = Db())
    {
        var failed = await Pos(db).PromoverADteAsync(empresa.Id, sale.Id, new PromoverVentaRequest(), "test");
        Check(failed.IsFailure && failed.Value?.DteDocumentoId is not null, "POS failure after draft retains document reference in error response");
        linkedId = failed.Value!.DteDocumentoId!.Value;
    }
    await using (var db = Db())
    {
        var persisted = await db.VentasPos.AsNoTracking().SingleAsync(v => v.Id == sale.Id);
        Check(persisted.DteDocumentoId == linkedId && persisted.EstadoFacturacion == VentaPosFacturacion.NoFacturada,
            "POS link persists atomically despite generation failure; not marked invoiced");
        var retry = await Pos(db).PromoverADteAsync(empresa.Id, sale.Id, new(), "retry");
        Check(retry.IsSuccess && retry.Value!.DteDocumentoId == linkedId, "POS retry returns original DTE");
        Check((await Pos(db).AnularAsync(empresa.Id, sale.Id, "test")).IsFailure,
            "POS with uncertain/pending DTE cannot be annulled as an ordinary ticket");
    }
    await using (var stale = Db())
    {
        var oldSale = await stale.VentasPos.SingleAsync(v => v.Id == sale.Id);
        await using var fresh = Db();
        await fresh.VentasPos.Where(v => v.Id == sale.Id).ExecuteUpdateAsync(s => s.SetProperty(v => v.EstadoCodigo, VentaPosEstados.Anulada));
        oldSale.DteDocumentoId = retryId;
        var blocked = false;
        try { await stale.SaveChangesAsync(); } catch (DbUpdateConcurrencyException) { blocked = true; }
        Check(blocked, "SQL concurrency token prevents linking a sale annulled by another transaction");
    }
    var parallelSale = new VentaPos { EmpresaId = empresa.Id, Numero = "POS-PARALLEL",
        Lineas = [new VentaPosLinea { Codigo = "TEST", Descripcion = "SYNTHETIC", Cantidad = 1, PrecioUnitario = 100, Total = 100 }] };
    schema.VentasPos.Add(parallelSale); await schema.SaveChangesAsync();
    var promotions = await Task.WhenAll(Enumerable.Range(0, 12).Select(async _ =>
    {
        await using var db = Db();
        return await Pos(db).PromoverADteAsync(empresa.Id, parallelSale.Id, new(), "test");
    }));
    Check(promotions.All(p => p.Value?.DteDocumentoId is not null)
        && promotions.Select(p => p.Value!.DteDocumentoId).Distinct().Count() == 1,
        "12 concurrent POS promotions preserve exactly one DTE reference despite partial failure");
    await using (var db = Db())
    {
        var original = await db.DteDocumentos.AsNoTracking().SingleAsync(d => d.Id == retryId);
        db.DteDocumentos.Add(new DteDocumento { EmpresaId = empresa.Id, NumeroControl = "DTE-01-M001P001-000000000999999",
            CodigoGeneracion = Guid.NewGuid().ToString().ToUpperInvariant(), IdempotencyScope = original.IdempotencyScope,
            IdempotencyKeyHash = original.IdempotencyKeyHash, IdempotencyRequestHash = original.IdempotencyRequestHash });
        var blocked = false;
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 }) { blocked = true; }
        Check(blocked, "Database unique index rejects a second DTE with same tenant/scope/key");
    }
    // GL1C: real transmission persistence, with a synthetic transport and no MH traffic.
    var reception = Substitute.For<IHaciendaReceptionClient>();
    reception.EnviarAsync(Arg.Any<HaciendaReceptionRequest>(), Arg.Any<CancellationToken>())
        .Returns(new HaciendaReceptionResult { CodigoHttp = 0, Estado = "CONTINGENCIA", ClasificaMsg = "TIMEOUT", DescripcionMsg = "Synthetic timeout" });
    var auth = Substitute.For<IHaciendaAuthClient>();
    auth.AutenticarAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
        .Returns(new HaciendaAuthResult { Success = true, Token = "synthetic-token", ExpiresAt = DateTime.UtcNow.AddHours(1) });
    var legacySigner = Substitute.For<IDteSignerService>();
    legacySigner.FirmarAsync(Arg.Any<string>(), Arg.Any<byte[]?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
        .Returns(c => new DteSignResult { Success = true, JsonFirmado = SyntheticJws(c.ArgAt<string>(0)) });
    DteDocumentosService TransmissionService(NeoStpDbContext db) => new(db, new DteCalculator(),
        new DteGeneratorService(Options.Create(new TerritorialOptions()), new ConfigurationBuilder().Build()), legacySigner, reception,
        Substitute.For<IHaciendaContingenciaClient>(), Substitute.For<IHaciendaEventoClient>(), auth, protector,
        Substitute.For<IDtePdfService>(), Substitute.For<ITenantEmailSender>(),
        Substitute.For<IAuditoriaService>(), Substitute.For<IConnectWebhookDispatcher>());
    var uncertain = new DteDocumento { EmpresaId = empresa.Id, TipoDteCodigo = "01", EstadoCodigo = DteEstadoCodigos.Firmado,
        AmbienteCodigo = DteAmbientes.Pruebas, NumeroControl = "DTE-01-M001P001-000000000999998",
        CodigoGeneracion = Guid.NewGuid().ToString().ToUpperInvariant(), ReceptorNombre = "SYNTHETIC",
        Detalles = [new() { NumeroLinea = 1, Codigo = "TEST", Descripcion = "SYNTHETIC", Cantidad = 1, PrecioUnitario = 100, VentaGravada = 100 }] };
    var payload = System.Text.Json.JsonSerializer.Serialize(new { identificacion = new { ambiente = "00", tipoDte = "01",
        numeroControl = uncertain.NumeroControl, codigoGeneracion = uncertain.CodigoGeneracion } });
    var encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payload)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    uncertain.Json = new() { JsonDte = payload, JsonFirmado = "eyJhbGciOiJSUzUxMiJ9." + encoded + ".c3ludGhldGlj" };
    schema.DteDocumentos.Add(uncertain); await schema.SaveChangesAsync();
    await using (var db = Db())
    {
        var result = await TransmissionService(db).EnviarAsync(empresa.Id, uncertain.Id, "test");
        Check(result.ErrorCode == "DTE_RESULTADO_INCIERTO" && result.Value!.EstadoCodigo == "ENVIADO",
            "Real SQL send keeps timeout pending reconciliation, not automatic contingency: " + result.ErrorCode + ": " + result.Error);
    }
    await using (var db = Db())
    {
        var current = await TransmissionService(db).GetByIdAsync(empresa.Id, uncertain.Id);
        Check(current.Value!.EnviadoAt.HasValue && current.Value.Diagnostico!.RequiereConsultaHacienda
            && current.Value.RespuestaHacienda!.Contains("TIMEOUT"), "Transmission evidence and guidance survive new SQL context");
        var attempt = await db.DteErrorOcurrencias.SingleAsync(o => o.EmpresaId == empresa.Id && o.DteDocumentoId == uncertain.Id);
        Check(attempt.RespuestaMhJson == current.Value.RespuestaHacienda && attempt.JsonEnviado == current.Value.JsonFirmado,
            "SQL diagnosis history retains response and signed payload without marking resolved");
        var retry = await TransmissionService(db).EnviarAsync(empresa.Id, uncertain.Id, "retry");
        Check(retry.ErrorCode == "DTE_RESULTADO_INCIERTO" && reception.ReceivedCalls().Count() == 1,
            "New SQL context refuses a blind resend of uncertain document");
    }
    // GL1D: normal FIRMADO path, real generator + SQL service + real webhook queue.
    // The signer creates only an identity-valid synthetic JWS, NOT a cryptographic
    // or Hacienda schema/certification test. No delivery worker/HTTP factory is run.
    schema.ChangeTracker.Clear();
    var fiscalEmpresa = await schema.Empresas.SingleAsync(e => e.Id == empresa.Id);
    fiscalEmpresa.Nrc = "1234567";
    fiscalEmpresa.CodigoActividad = "62010";
    fiscalEmpresa.ActividadEconomica = "Programacion informatica";
    fiscalEmpresa.Departamento = "06";
    fiscalEmpresa.Municipio = "23";
    fiscalEmpresa.Distrito = "01";
    fiscalEmpresa.Direccion = "Direccion sintetica para auditoria SQL, sin cliente real";
    fiscalEmpresa.Correo = "audit@example.invalid";
    fiscalEmpresa.Telefono = "22222222";
    var fiscalConfig = await schema.DteConfiguracion.SingleAsync(c => c.EmpresaId == empresa.Id);
    fiscalConfig.AmbienteCodigo = DteAmbientes.Pruebas;
    fiscalConfig.TipoEstablecimientoCodigo = "02";
    fiscalConfig.CodigoEstablecimientoMh = "M001";
    fiscalConfig.CodigoPuntoVentaMh = "P001";
    fiscalConfig.CertificadoBlob = [1, 2, 3]; // Handled only by synthetic signer below.
    schema.ConnectWebhooks.Add(new ConnectWebhook
    {
        EmpresaId = empresa.Id, Url = "https://audit.example.invalid/webhook",
        SecretoHmac = "synthetic-not-used", Eventos = string.Join(',', ConnectEventos.All), Activo = true,
    });
    await schema.SaveChangesAsync();

    var generator = new DteGeneratorService(Options.Create(new TerritorialOptions()), new ConfigurationBuilder().Build());
    var signer = Substitute.For<IDteSignerService>();
    signer.FirmarAsync(Arg.Any<string>(), Arg.Any<byte[]?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
        .Returns(c => new DteSignResult { Success = true, JsonFirmado = SyntheticJws(c.ArgAt<string>(0)), Detalle = "SQL audit synthetic signer" });
    ConnectWebhookDispatcher Queue(NeoStpDbContext db) => new(db,
        Substitute.For<IHttpClientFactory>(), NullLogger<ConnectWebhookDispatcher>.Instance);
    DteDocumentosService FiscalService(NeoStpDbContext db, IHaciendaReceptionClient transport) => new(db,
        new DteCalculator(), generator, signer, transport,
        Substitute.For<IHaciendaContingenciaClient>(), Substitute.For<IHaciendaEventoClient>(), auth, protector,
        Substitute.For<IDtePdfService>(), Substitute.For<ITenantEmailSender>(), Substitute.For<IAuditoriaService>(), Queue(db));
    static string SyntheticJws(string json)
        => "eyJhbGciOiJSUzUxMiJ9." + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_') + ".c3ludGhldGlj";
    async Task<DteDocumentoDto> Signed(string name)
    {
        await using var db = Db();
        var svc = FiscalService(db, Substitute.For<IHaciendaReceptionClient>());
        var createdFixture = await svc.CreateBorradorAsync(empresa.Id, Request("GL1D-" + name), "sql-audit");
        if (createdFixture.IsFailure) throw new InvalidOperationException("Fixture create: " + createdFixture.Error);
        var fixtureId = createdFixture.Value!.Id;
        var generated = await svc.GenerarAsync(empresa.Id, fixtureId, "sql-audit");
        var validated = await svc.ValidarAsync(empresa.Id, fixtureId, "sql-audit");
        var signed = await svc.FirmarAsync(empresa.Id, fixtureId, "sql-audit");
        Check(generated.IsSuccess && validated.IsSuccess && signed.IsSuccess
            && signed.Value!.EstadoCodigo == DteEstadoCodigos.Firmado && !signed.Value.EnviadoAt.HasValue,
            "GL1D normal generated/validated/signed fixture: " + name
            + (signed.IsFailure ? ": " + signed.Error : string.Empty));
        return signed.Value!;
    }
    var received = new ConcurrentQueue<HaciendaReceptionRequest>();
    var processedAt = new DateTime(2026, 9, 4, 12, 0, 0, DateTimeKind.Utc);
    HaciendaReceptionResult Accepted() => new()
    {
        Success = true, CodigoHttp = 200, Estado = DteEstadoCodigos.Procesado,
        SelloRecibido = "SQL-AUDIT-SEAL", FhProcesamiento = processedAt,
        Raw = "{\"estado\":\"PROCESADO\",\"selloRecibido\":\"SQL-AUDIT-SEAL\"}",
    };
    var acceptedReception = Substitute.For<IHaciendaReceptionClient>();
    acceptedReception.EnviarAsync(Arg.Any<HaciendaReceptionRequest>(), Arg.Any<CancellationToken>())
        .Returns(c => { received.Enqueue(c.ArgAt<HaciendaReceptionRequest>(0)); return Accepted(); });
    async Task<List<ConnectWebhookDelivery>> Deliveries(int id)
    {
        await using var db = Db();
        var all = await db.ConnectWebhookDeliveries.AsNoTracking().Where(d => d.EmpresaId == empresa.Id).ToListAsync();
        return all.Where(d => { using var json = JsonDocument.Parse(d.Payload); return json.RootElement.GetProperty("dteId").GetInt32() == id; }).ToList();
    }
    async Task AssertProcessed(DteDocumentoDto original, string name, string seal = "SQL-AUDIT-SEAL")
    {
        await using var db = Db();
        var persisted = await db.DteDocumentos.AsNoTracking().Include(d => d.Json).SingleAsync(d => d.Id == original.Id);
        Check(persisted.EstadoCodigo == DteEstadoCodigos.Procesado && persisted.CodigoGeneracion == original.CodigoGeneracion
            && persisted.NumeroControl == original.NumeroControl && persisted.SelloRecibido == seal
            && persisted.ProcesadoAt == processedAt && persisted.EnviadoAt.HasValue,
            "GL1D terminal state, identity, seal and timestamp preserved: " + name);
        var deliveries = await Deliveries(original.Id);
        Check(deliveries.Count == 1 && deliveries[0].Evento == ConnectEventos.DteProcesado
            && deliveries[0].Estado == ConnectDeliveryEstados.Pendiente && deliveries[0].Intentos == 0,
            "GL1D exactly one persisted processed webhook, no delivery attempted: " + name);
    }

    // Both processes load FIRMADO and generate before either UPDATE is allowed.
    // Release stale writer only after winner persists PROCESADO and webhook.
    var parallelSigned = await Signed("parallel-send");
    var firstGate = new FiscalSaveGate(parallelSigned.Id, DteEstadoCodigos.Generado);
    var secondGate = new FiscalSaveGate(parallelSigned.Id, DteEstadoCodigos.Generado);
    await using (var first = Db(firstGate))
    await using (var second = Db(secondGate))
    {
        var firstSend = FiscalService(first, acceptedReception).EnviarAsync(empresa.Id, parallelSigned.Id, "sql-first");
        var secondSend = FiscalService(second, acceptedReception).EnviarAsync(empresa.Id, parallelSigned.Id, "sql-second");
        try
        {
            await Task.WhenAll(firstGate.Ready, secondGate.Ready).WaitAsync(TimeSpan.FromSeconds(30));
            firstGate.Release();
            var winner = await firstSend;
            secondGate.Release();
            var loser = await secondSend;
            Check(winner.IsSuccess && loser.ErrorCode == "DTE_CONCURRENCY_CONFLICT"
                && loser.Value!.EstadoCodigo == DteEstadoCodigos.Procesado,
                "GL1D two FIRMADO senders: winner succeeds, stale process returns persisted terminal document");
            var attempts = received.Where(x => x.IdEnvio == parallelSigned.Id).ToArray();
            Check(attempts.Length == 1 && attempts[0].CodigoGeneracion == parallelSigned.CodigoGeneracion
                && attempts[0].Ambiente == "00", "GL1D concurrent normal send performs exactly one transport call with original UUID");
            await using var persistedDb = Db();
            var persisted = await persistedDb.DteDocumentos.Include(d => d.Json).SingleAsync(d => d.Id == parallelSigned.Id);
            Check(persisted.Json!.JsonFirmado == attempts[0].Documento
                && DteFiscalContext.CoincideJws(attempts[0].Documento, DteAmbientes.Pruebas, persisted),
                "GL1D transmitted identity-valid JWS is exactly the persisted winner payload");
        }
        finally { firstGate.Release(); secondGate.Release(); await Task.WhenAll(firstSend, secondSend); }
    }
    await AssertProcessed(parallelSigned, "parallel-send");

    var lateInvalidation = await Signed("stale-invalidation");
    var invalidationGate = new FiscalSaveGate(lateInvalidation.Id, DteEstadoCodigos.Invalidado);
    await using (var stale = Db(invalidationGate))
    {
        var invalidation = FiscalService(stale, acceptedReception).InvalidarAsync(empresa.Id, lateInvalidation.Id, "synthetic", "sql-stale");
        try
        {
            await invalidationGate.Ready.WaitAsync(TimeSpan.FromSeconds(30));
            await using var sender = Db();
            var sent = await FiscalService(sender, acceptedReception).EnviarAsync(empresa.Id, lateInvalidation.Id, "sql-sender");
            invalidationGate.Release();
            Check(sent.IsSuccess && (await invalidation).ErrorCode == "DTE_CONCURRENCY_CONFLICT",
                "GL1D stale local invalidation cannot overwrite a concurrently processed DTE");
        }
        finally { invalidationGate.Release(); await invalidation; }
    }
    await AssertProcessed(lateInvalidation, "stale-invalidation");

    var invalidationWins = await Signed("invalidation-wins");
    var generationGate = new FiscalSaveGate(invalidationWins.Id, DteEstadoCodigos.Generado);
    await using (var stale = Db(generationGate))
    {
        var sending = FiscalService(stale, acceptedReception).EnviarAsync(empresa.Id, invalidationWins.Id, "sql-stale");
        try
        {
            await generationGate.Ready.WaitAsync(TimeSpan.FromSeconds(30));
            await using var invalidator = Db();
            var invalidation = await FiscalService(invalidator, acceptedReception).InvalidarAsync(empresa.Id, invalidationWins.Id, "synthetic", "sql-winner");
            generationGate.Release();
            Check(invalidation.IsSuccess && (await sending).ErrorCode == "DTE_CONCURRENCY_CONFLICT"
                && !received.Any(r => r.IdEnvio == invalidationWins.Id),
                "GL1D invalidation wins before transmission: stale sender sends nothing");
        }
        finally { generationGate.Release(); await sending; }
    }
    await using (var db = Db())
    {
        var persisted = await db.DteDocumentos.AsNoTracking().SingleAsync(d => d.Id == invalidationWins.Id);
        var deliveries = await Deliveries(invalidationWins.Id);
        Check(persisted.EstadoCodigo == DteEstadoCodigos.Invalidado && persisted.EnviadoAt is null
            && persisted.SelloRecibido is null && persisted.ProcesadoAt is null
            && deliveries.Count == 1 && deliveries[0].Evento == ConnectEventos.DteInvalidado,
            "GL1D winning local invalidation retains unsent state and exactly one invalidation webhook");
    }

    var inFlight = await Signed("in-flight-invalidation");
    var responseGate = new AsyncGate();
    var heldReception = Substitute.For<IHaciendaReceptionClient>();
    heldReception.EnviarAsync(Arg.Any<HaciendaReceptionRequest>(), Arg.Any<CancellationToken>())
        .Returns(async c => { received.Enqueue(c.ArgAt<HaciendaReceptionRequest>(0)); await responseGate.ArriveAsync(); return Accepted(); });
    await using (var sender = Db())
    {
        var sending = FiscalService(sender, heldReception).EnviarAsync(empresa.Id, inFlight.Id, "sql-sender");
        try
        {
            await responseGate.Ready.WaitAsync(TimeSpan.FromSeconds(30));
            await using var other = Db();
            var attempt = await other.DteDocumentos.AsNoTracking().SingleAsync(d => d.Id == inFlight.Id);
            var invalidation = await FiscalService(other, acceptedReception).InvalidarAsync(empresa.Id, inFlight.Id, "synthetic", "sql-other");
            Check(attempt.EstadoCodigo == DteEstadoCodigos.Enviado && attempt.EnviadoAt.HasValue
                && invalidation.ErrorCode == "DTE_RESULTADO_INCIERTO" && (await Deliveries(inFlight.Id)).Count == 0,
                "GL1D in-flight durable claim rejects invalidation before receiving success");
            var note = await FiscalService(other, acceptedReception).GuardarNotaInternaAsync(
                empresa.Id, inFlight.Id, "Synthetic note while waiting for ACK", "sql-note");
            Check(note.IsSuccess, "GL1D operational note can be saved during an in-flight transmission");
        }
        finally { responseGate.Release(); await sending; }
    }
    await AssertProcessed(inFlight, "in-flight-invalidation");
    await using (var db = Db())
    {
        var persisted = await db.DteDocumentos.AsNoTracking().SingleAsync(d => d.Id == inFlight.Id);
        Check(persisted.NotaInterna == "Synthetic note while waiting for ACK",
            "GL1D ACK and concurrent operational note are both preserved without a false fiscal conflict");
    }

    // Simulate a competing reconciler in another SQL context. The original sender has
    // crossed transport and must not overwrite the terminal DTE OR its JSON evidence.
    var lateResponse = await Signed("late-response");
    var lateResponseGate = new AsyncGate();
    var rejectionReception = Substitute.For<IHaciendaReceptionClient>();
    rejectionReception.EnviarAsync(Arg.Any<HaciendaReceptionRequest>(), Arg.Any<CancellationToken>())
        .Returns(async c =>
        {
            received.Enqueue(c.ArgAt<HaciendaReceptionRequest>(0));
            await lateResponseGate.ArriveAsync();
            return new HaciendaReceptionResult { CodigoHttp = 400, Estado = "ERROR", CodigoMsg = "008", DescripcionMsg = "Synthetic stale rejection", Raw = "{\"estado\":\"ERROR\",\"codigoMsg\":\"008\"}" };
        });
    const string winningResponse = "{\"estado\":\"PROCESADO\",\"selloRecibido\":\"SQL-RECONCILED-SEAL\"}";
    await using (var sender = Db())
    {
        var sending = FiscalService(sender, rejectionReception).EnviarAsync(empresa.Id, lateResponse.Id, "sql-stale");
        try
        {
            await lateResponseGate.Ready.WaitAsync(TimeSpan.FromSeconds(30));
            await using var reconciler = Db();
            var winner = await reconciler.DteDocumentos.Include(d => d.Json).SingleAsync(d => d.Id == lateResponse.Id);
            winner.EstadoCodigo = DteEstadoCodigos.Procesado;
            winner.SelloRecibido = "SQL-RECONCILED-SEAL";
            winner.ProcesadoAt = processedAt;
            winner.UpdatedAt = DateTime.UtcNow;
            winner.Json!.RespuestaHacienda = winningResponse;
            await reconciler.SaveChangesAsync();
            await Queue(reconciler).DispatchAsync(new ConnectDteEventoPayload
            {
                EmpresaId = empresa.Id, DteId = winner.Id, CodigoGeneracion = winner.CodigoGeneracion,
                TipoDte = winner.TipoDteCodigo, Estado = winner.EstadoCodigo, Evento = ConnectEventos.DteProcesado,
            });
            lateResponseGate.Release();
            Check((await sending).ErrorCode == "DTE_CONCURRENCY_CONFLICT",
                "GL1D late transport rejection returns concurrency conflict after another SQL writer processes the document");
        }
        finally { lateResponseGate.Release(); await sending; }
    }
    await AssertProcessed(lateResponse, "late-response", "SQL-RECONCILED-SEAL");
    await using (var db = Db())
    {
        var json = await db.Set<DteDocumentoJson>().AsNoTracking().SingleAsync(j => j.DocumentoId == lateResponse.Id);
        Check(json.RespuestaHacienda == winningResponse
            && !await db.DteErrorOcurrencias.AnyAsync(o => o.DteDocumentoId == lateResponse.Id),
            "GL1D rejected stale SaveChanges rolls back conflicting JSON/error history with terminal state intact");
    }

    var timeoutSigned = await Signed("signed-timeout");
    await using (var db = Db())
    {
        var result = await FiscalService(db, reception).EnviarAsync(empresa.Id, timeoutSigned.Id, "sql-timeout");
        Check(result.ErrorCode == "DTE_RESULTADO_INCIERTO" && result.Value!.EstadoCodigo == DteEstadoCodigos.Enviado,
            "GL1D normal FIRMADO timeout is pending reconciliation");
    }
    await using (var db = Db())
    {
        var svc = FiscalService(db, reception);
        var blocked = await svc.InvalidarAsync(empresa.Id, timeoutSigned.Id, "synthetic", "sql-invalid");
        var current = await svc.GetByIdAsync(empresa.Id, timeoutSigned.Id);
        Check(blocked.ErrorCode == "DTE_RESULTADO_INCIERTO" && current.Value!.EnviadoAt.HasValue
            && current.Value.EstadoCodigo == DteEstadoCodigos.Enviado && current.Value.Diagnostico!.RequiereConsultaHacienda
            && current.Value.RespuestaHacienda!.Contains("TIMEOUT") && (await Deliveries(timeoutSigned.Id)).Count == 0,
            "GL1D timeout cannot be locally invalidated or lose its reception evidence/guidance");
    }
    // GL1E: batch and individual transmission must compete for the SAME SQL rows.
    async Task<int> BatchEvent(params DteDocumentoDto[] documents)
    {
        await using var db = Db();
        var evt = new DteEvento { EmpresaId = empresa.Id, AmbienteCodigo = DteAmbientes.Pruebas,
            TipoEventoCodigo = TipoEventoCodigos.Contingencia, EstadoCodigo = DteEventoEstadoCodigos.Procesado,
            CodigoGeneracion = Guid.NewGuid().ToString().ToUpperInvariant(), SelloRecibido = "SYNTHETIC-EVENT-SEAL" };
        db.Add(evt); await db.SaveChangesAsync();
        foreach (var d in documents)
            db.DteEventoDocumentosRelacionados.Add(new() { EventoId = evt.Id, DocumentoId = d.Id, RolCodigo = DteEventoRolCodigos.LoteContingencia });
        await db.SaveChangesAsync(); return evt.Id;
    }
    var batchCalls = new ConcurrentQueue<HaciendaLoteRequest>();
    var batchTransport = Substitute.For<IHaciendaLoteClient>();
    batchTransport.EnviarLoteAsync(Arg.Any<HaciendaLoteRequest>(), Arg.Any<CancellationToken>()).Returns(c =>
    {
        batchCalls.Enqueue(c.ArgAt<HaciendaLoteRequest>(0));
        return new HaciendaLoteResult { Success = true, CodigoHttp = 200, Estado = "PROCESADO",
            CodigoLote = Guid.NewGuid().ToString("N"), SelloRecibido = "SYNTHETIC-BATCH-SEAL", Raw = "synthetic-batch-ack" };
    });
    ContingenciaLoteService BatchService(NeoStpDbContext db, IHaciendaLoteClient? send = null, IHaciendaConsultaLoteClient? query = null)
        => new(db, send ?? batchTransport, query ?? Substitute.For<IHaciendaConsultaLoteClient>(), auth, protector,
            NullLogger<ContingenciaLoteService>.Instance, Queue(db));

    var batchParallel = await Signed("GL1E-batch-parallel");
    var batchEvent = await BatchEvent(batchParallel);
    var firstBatchGate = new FiscalSaveGate(batchParallel.Id, DteEstadoCodigos.Enviado);
    var secondBatchGate = new FiscalSaveGate(batchParallel.Id, DteEstadoCodigos.Enviado);
    await using (var firstDb = Db(firstBatchGate))
    await using (var secondDb = Db(secondBatchGate))
    {
        var first = BatchService(firstDb).CrearYEnviarLoteAsync(batchEvent, empresa.Id, "sql-first");
        var second = BatchService(secondDb).CrearYEnviarLoteAsync(batchEvent, empresa.Id, "sql-second");
        try
        {
            await Task.WhenAll(firstBatchGate.Ready, secondBatchGate.Ready).WaitAsync(TimeSpan.FromSeconds(45));
            firstBatchGate.Release();
            Check((await first).IsSuccess, "GL1E first batch claims and transmits successfully");
            secondBatchGate.Release();
            Check((await second).ErrorCode == "DTE_CONCURRENCY_CONFLICT", "GL1E stale second batch returns conflict before transport");
        }
        finally { firstBatchGate.Release(); secondBatchGate.Release(); await Task.WhenAll(first, second); }
    }
    await using (var db = Db())
    {
        Check(await db.DteContingenciaLotes.CountAsync(l => l.EventoContingenciaId == batchEvent) == 1,
            "GL1E losing transaction does not leave a phantom batch");
        var claim = await db.DteDocumentos.SingleAsync(d => d.Id == batchParallel.Id);
        var lote = await db.DteContingenciaLotes.SingleAsync(l => l.EventoContingenciaId == batchEvent);
        Check(claim.EstadoCodigo == DteEstadoCodigos.Enviado && claim.EnviadoAt == lote.EnviadoAt && lote.Intentos == 1,
            "GL1E document and batch share one persisted attempt before acknowledgement");
        Check(batchCalls.Count(r => r.Items.Any(i => i.CodigoGeneracion == batchParallel.CodigoGeneracion)) == 1,
            "GL1E overlapping batches perform exactly one POST");
    }

    // Partial multi-document claim must roll back every row when an individual send wins.
    var shared = await Signed("GL1E-individual-wins");
    var companion = await Signed("GL1E-companion-rollback");
    var sharedEvent = await BatchEvent(shared, companion);
    var losingBatchGate = new FiscalSaveGate(shared.Id, DteEstadoCodigos.Enviado);
    await using (var batchDb = Db(losingBatchGate))
    {
        var pending = BatchService(batchDb).CrearYEnviarLoteAsync(sharedEvent, empresa.Id, "sql-losing-batch");
        try
        {
            await losingBatchGate.Ready.WaitAsync(TimeSpan.FromSeconds(45));
            await using var individualDb = Db();
            Check((await FiscalService(individualDb, acceptedReception).EnviarAsync(empresa.Id, shared.Id, "sql-individual")).IsSuccess,
                "GL1E individual sender can win before the batch claim commits");
            losingBatchGate.Release();
            Check((await pending).ErrorCode == "DTE_CONCURRENCY_CONFLICT", "GL1E individual winner rejects stale multi-document batch");
        }
        finally { losingBatchGate.Release(); await pending; }
    }
    await using (var db = Db())
    {
        var otherDoc = await db.DteDocumentos.SingleAsync(d => d.Id == companion.Id);
        Check(otherDoc.EstadoCodigo == DteEstadoCodigos.Firmado && !otherDoc.EnviadoAt.HasValue
            && !await db.DteContingenciaLotes.AnyAsync(l => l.EventoContingenciaId == sharedEvent),
            "GL1E SQL rollback leaves companion unclaimed and no phantom batch");
        Check(!batchCalls.Any(r => r.Items.Any(i => i.CodigoGeneracion == shared.CodigoGeneracion)),
            "GL1E losing multi-document batch transmits nothing");
    }
    await AssertProcessed(shared, "GL1E-individual-wins");

    // Opposite race plus lost acknowledgement: batch owns attempt; individual sends nothing.
    var batchWins = await Signed("GL1E-batch-wins-timeout");
    var winsEvent = await BatchEvent(batchWins);
    var individualGate = new FiscalSaveGate(batchWins.Id, DteEstadoCodigos.Generado);
    var timeoutBatch = Substitute.For<IHaciendaLoteClient>();
    var timeoutCalls = 0;
    timeoutBatch.EnviarLoteAsync(Arg.Any<HaciendaLoteRequest>(), Arg.Any<CancellationToken>()).Returns(_ =>
    {
        Interlocked.Increment(ref timeoutCalls);
        return new HaciendaLoteResult { CodigoHttp = 0, CodigoMsg = "TIMEOUT", DescripcionMsg = "Synthetic timeout" };
    });
    await using (var individualDb = Db(individualGate))
    {
        var stale = FiscalService(individualDb, acceptedReception).EnviarAsync(empresa.Id, batchWins.Id, "sql-stale-individual");
        try
        {
            await individualGate.Ready.WaitAsync(TimeSpan.FromSeconds(45));
            await using var batchDb = Db();
            var batchSvc = BatchService(batchDb, timeoutBatch);
            var result = await batchSvc.CrearYEnviarLoteAsync(winsEvent, empresa.Id, "sql-batch");
            Check(result.ErrorCode == "DTE_RESULTADO_INCIERTO" && result.Value!.LoteId > 0,
                "GL1E lost batch ACK reports uncertainty with persisted batch ID");
            Check((await batchSvc.CrearYEnviarLoteAsync(winsEvent, empresa.Id, "sql-replay")).ErrorCode == "DTE_RESULTADO_INCIERTO" && timeoutCalls == 1,
                "GL1E replay of uncertain batch never repeats POST");
            individualGate.Release();
            Check((await stale).ErrorCode == "DTE_CONCURRENCY_CONFLICT", "GL1E batch winner blocks stale individual regeneration/send");
        }
        finally { individualGate.Release(); await stale; }
    }
    await using (var db = Db())
    {
        var doc = await db.DteDocumentos.SingleAsync(d => d.Id == batchWins.Id);
        var lote = await db.DteContingenciaLotes.SingleAsync(l => l.EventoContingenciaId == winsEvent);
        Check(doc.EstadoCodigo == DteEstadoCodigos.Enviado && doc.EnviadoAt == lote.EnviadoAt
            && lote.CodigoLote is null && lote.RawEnvio!.Contains("TIMEOUT") && !received.Any(r => r.IdEnvio == doc.Id),
            "GL1E uncertain batch preserves claim and diagnostic; individual transport untouched");
        Check((await FiscalService(db, acceptedReception).InvalidarAsync(empresa.Id, doc.Id, "synthetic", "sql-invalid")).ErrorCode == "DTE_RESULTADO_INCIERTO",
            "GL1E batch in-flight document cannot be locally invalidated");
    }

    // Two query contexts: late rejection cannot overwrite a confirmed seal/JSON or queue another event.
    int queryBatchId;
    await using (var db = Db()) queryBatchId = await db.DteContingenciaLotes.Where(l => l.EventoContingenciaId == batchEvent).Select(l => l.Id).SingleAsync();
    var queryGate = new AsyncGate();
    var staleQuery = Substitute.For<IHaciendaConsultaLoteClient>();
    staleQuery.ConsultarLoteAsync(Arg.Any<HaciendaConsultaLoteRequest>(), Arg.Any<CancellationToken>()).Returns(async _ =>
    {
        await queryGate.ArriveAsync();
        return new HaciendaConsultaLoteResult { Success = true, Raw = "stale-rejection",
            Items = [new() { CodigoGeneracion = batchParallel.CodigoGeneracion, Estado = "RECHAZADO", CodigoMsg = "008" }] };
    });
    var acceptedQuery = Substitute.For<IHaciendaConsultaLoteClient>();
    acceptedQuery.ConsultarLoteAsync(Arg.Any<HaciendaConsultaLoteRequest>(), Arg.Any<CancellationToken>()).Returns(new HaciendaConsultaLoteResult
    {
        Success = true, Raw = "confirmed-batch-query", Items = [new() { CodigoGeneracion = batchParallel.CodigoGeneracion,
            Estado = "PROCESADO", SelloRecibido = "BATCH-DOCUMENT-SEAL" }],
    });
    await using (var staleDb = Db())
    {
        var stale = BatchService(staleDb, query: staleQuery).ConsultarLoteAsync(queryBatchId, empresa.Id);
        try
        {
            await queryGate.Ready.WaitAsync(TimeSpan.FromSeconds(45));
            await using var winningDb = Db();
            Check((await BatchService(winningDb, query: acceptedQuery).ConsultarLoteAsync(queryBatchId, empresa.Id)).IsSuccess,
                "GL1E query winner persists confirmed individual batch evidence");
            queryGate.Release();
            Check((await stale).ErrorCode == "DTE_CONCURRENCY_CONFLICT", "GL1E late query loses optimistic concurrency without overwriting success");
        }
        finally { queryGate.Release(); await stale; }
    }
    await using (var db = Db())
    {
        var doc = await db.DteDocumentos.Include(d => d.Json).SingleAsync(d => d.Id == batchParallel.Id);
        var lote = await db.DteContingenciaLotes.Include(l => l.Detalles).SingleAsync(l => l.Id == queryBatchId);
        Check(doc.EstadoCodigo == DteEstadoCodigos.Procesado && doc.SelloRecibido == "BATCH-DOCUMENT-SEAL"
            && doc.ProcesadoAt.HasValue && doc.Json!.RespuestaHacienda!.Contains("BATCH-DOCUMENT-SEAL")
            && lote.RawConsulta == "confirmed-batch-query" && lote.Detalles.Single().SelloRecibido == doc.SelloRecibido,
            "GL1E stale query transaction rolls back detail/raw/JSON together and preserves terminal evidence");
        Check((await Deliveries(doc.Id)).Count == 1, "GL1E only confirmed batch query queues one webhook; no delivery HTTP executed");
    }
    completed = true;
    Console.WriteLine($"DTE SQL verification completed: {checks.Count}/{checks.Count}; no real Hacienda calls.");
}
finally
{
    if (database == "NeoStpDteAudit_" + suffix && Guid.TryParseExact(suffix, "N", out _)
        && schema.Database.GetDbConnection().DataSource == server
        && schema.Database.GetDbConnection().Database == database)
    {
        await schema.Database.EnsureDeletedAsync();
        Console.WriteLine("Removed task-owned synthetic database: " + database);
    }
    var evidence = Environment.GetEnvironmentVariable("NEOSTP_DTE_SQL_EVIDENCE");
    if (completed && !string.IsNullOrWhiteSpace(evidence))
    {
        var absolute = Path.GetFullPath(evidence);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        await File.WriteAllTextAsync(absolute, JsonSerializer.Serialize(new {
            generatedAtUtc = DateTime.UtcNow, syntheticServer = server, syntheticDatabase = database,
            migrationsApplied = migrations.Length > 0, appliedMigrations = migrations, syntheticDatabaseDeleted = true,
            passed = checks.Count, failed = 0, checks,
            scope = "Real isolated SQL; synthetic DTE and provider substitutes only; no Hacienda calls or customer data"
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}

// Barriers are deterministic; timeout only bounds a broken fixture so CI cannot hang.
sealed class AsyncGate
{
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task Ready => _ready.Task;
    public void Release() => _release.TrySetResult();
    public async Task ArriveAsync()
    {
        _ready.TrySetResult();
        await _release.Task.WaitAsync(TimeSpan.FromSeconds(45));
    }
}

sealed class FiscalSaveGate(int documentId, string state) : SaveChangesInterceptor
{
    private readonly AsyncGate _gate = new();
    private int _entered;
    public Task Ready => _gate.Ready;
    public void Release() => _gate.Release();
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var db = eventData.Context!;
        db.ChangeTracker.DetectChanges();
        if (db.ChangeTracker.Entries<DteDocumento>().Any(e => e.Entity.Id == documentId
                && e.State == EntityState.Modified && e.Entity.EstadoCodigo == state)
            && Interlocked.Exchange(ref _entered, 1) == 0)
            await _gate.ArriveAsync();
        return result;
    }
}
