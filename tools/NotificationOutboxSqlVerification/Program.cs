using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Notificaciones;
using NeoSTP.Application.Workers;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Notificaciones;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;

const string connectionVariable = "NEOSTP_SQLSERVER_TEST_CONNECTION";
const string localDbServer = @"(localdb)\NeoStpAuthAudit_20260904";
var configuredRoot = Environment.GetEnvironmentVariable(connectionVariable);
var rootBuilder = string.IsNullOrWhiteSpace(configuredRoot)
    ? new SqlConnectionStringBuilder { DataSource = localDbServer, IntegratedSecurity = true }
    : new SqlConnectionStringBuilder(configuredRoot);
var suffix = Guid.NewGuid().ToString("N");
var database = "NotificationOutboxAudit_" + suffix;
rootBuilder.InitialCatalog = database;
rootBuilder.TrustServerCertificate = true;
rootBuilder.ConnectTimeout = 10;
var connection = rootBuilder.ConnectionString;

NeoStpDbContext Db() => new(new DbContextOptionsBuilder<NeoStpDbContext>()
    .UseSqlServer(connection, sql => sql.EnableRetryOnFailure(3)).Options);

var checks = new List<string>();
void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + name);
    checks.Add(name);
    Console.WriteLine("PASS: " + name);
}

await using var schema = Db();
try
{
    await schema.Database.MigrateAsync();
    var migrations = (await schema.Database.GetAppliedMigrationsAsync()).ToArray();
    Check(migrations[^1] == "20260911171700_P1_NotificationOutbox",
        "Real migration chain reaches notification outbox migration");
    Check(!await schema.NotificationOutbox.AnyAsync(),
        "Migration creates an empty outbox without synthetic data leakage");

    var company = new Empresa
    {
        Nit = "OUTBOX-" + suffix[..8],
        RazonSocial = "SYNTHETIC OUTBOX",
        EstadoCodigo = "ACTIVA",
    };
    schema.Empresas.Add(company);
    await schema.SaveChangesAsync();

    var concurrentRequest = new NotificationOutboxRequest(
        company.Id,
        NotificationOutboxTipos.AlertaCreada,
        NotificationOutboxCanales.Push,
        null,
        "{}",
        "SQL-CONCURRENT-ENQUEUE");
    int[] enqueueIds;
    await using (var first = Db())
    await using (var second = Db())
    {
        enqueueIds = await Task.WhenAll(
            new NotificationOutboxService(first).EnqueueAsync(concurrentRequest),
            new NotificationOutboxService(second).EnqueueAsync(concurrentRequest));
    }
    Check(enqueueIds[0] == enqueueIds[1]
          && await schema.NotificationOutbox.CountAsync(x => x.EmpresaId == company.Id
              && x.ClaveIdempotencia == concurrentRequest.ClaveIdempotencia) == 1,
        "Concurrent SQL enqueue returns one canonical idempotent row");

    var directDuplicateRejected = false;
    await using (var duplicateDb = Db())
    {
        duplicateDb.NotificationOutbox.Add(new NotificationOutboxMessage
        {
            EmpresaId = company.Id,
            Tipo = NotificationOutboxTipos.AlertaCreada,
            Canal = NotificationOutboxCanales.Push,
            Payload = "{}",
            ClaveIdempotencia = concurrentRequest.ClaveIdempotencia,
        });
        try
        {
            await duplicateDb.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql
                                           && sql.Number is 2601 or 2627)
        {
            directDuplicateRejected = true;
        }
    }
    Check(directDuplicateRejected, "Unique SQL index rejects a direct duplicate key");
    await schema.NotificationOutbox
        .Where(x => x.EmpresaId == company.Id
            && x.ClaveIdempotencia == concurrentRequest.ClaveIdempotencia)
        .ExecuteUpdateAsync(setters => setters
            .SetProperty(x => x.Estado, NotificationOutboxEstados.Sent)
            .SetProperty(x => x.ProcesadoAt, DateTime.UtcNow)
            .SetProperty(x => x.UpdatedAt, DateTime.UtcNow));

    var alert = new Alerta
    {
        EmpresaId = company.Id,
        TipoCodigo = AlertaTipos.DteRechazado,
        Severidad = AlertaSeveridades.Critica,
        Titulo = "Synthetic alert",
        Mensaje = "Synthetic message",
        Clave = "SYNTHETIC:1",
    };
    schema.Alertas.Add(alert);
    schema.DispositivosNotificacion.Add(new DispositivoNotificacion
    {
        EmpresaId = company.Id,
        UsuarioId = 1,
        Token = "synthetic-token-" + suffix,
        Activo = true,
    });
    await schema.SaveChangesAsync();
    var deliveryKey = "SQL-CONCURRENT-DELIVERY";
    await new NotificationOutboxService(schema).EnqueueAsync(new NotificationOutboxRequest(
        company.Id,
        NotificationOutboxTipos.AlertaCreada,
        NotificationOutboxCanales.Push,
        null,
        JsonSerializer.Serialize(new AlertaPushOutboxPayload(company.Id, alert.Id)),
        deliveryKey));

    var sender = new CountingPushSender();
    var workerOptions = Options.Create(new WorkerOptions
    {
        NotificationOutbox = new NotificationOutboxOptions
        {
            Enabled = true,
            LoteMaximo = 50,
            LeaseSegundos = 120,
        },
    });
    int[] processed;
    await using (var first = Db())
    await using (var second = Db())
    {
        var firstProcessor = new NotificationOutboxProcessor(
            first, sender, workerOptions, NullLogger<NotificationOutboxProcessor>.Instance);
        var secondProcessor = new NotificationOutboxProcessor(
            second, sender, workerOptions, NullLogger<NotificationOutboxProcessor>.Instance);
        processed = await Task.WhenAll(
            firstProcessor.ProcessPendingAsync(),
            secondProcessor.ProcessPendingAsync()).WaitAsync(TimeSpan.FromSeconds(30));
    }

    var delivered = await schema.NotificationOutbox.AsNoTracking()
        .SingleAsync(x => x.EmpresaId == company.Id && x.ClaveIdempotencia == deliveryKey);
    Check(processed.Sum() == 1 && sender.Calls == 1
          && delivered.Estado == NotificationOutboxEstados.Sent
          && delivered.Intentos == 1,
        "Two SQL workers claim once and call the provider once");

    var rollbackCompany = new Empresa
    {
        Nit = "ROLLBACK-" + suffix[..8],
        RazonSocial = "SYNTHETIC ROLLBACK",
        EstadoCodigo = "ACTIVA",
    };
    schema.Empresas.Add(rollbackCompany);
    await schema.SaveChangesAsync();
    await using (var rollbackDb = Db())
    {
        var alerts = new AlertaService(
            rollbackDb,
            new ThrowingOutbox(),
            NullLogger<AlertaService>.Instance);
        try
        {
            await alerts.CrearAsync(new()
            {
                EmpresaId = rollbackCompany.Id,
                TipoCodigo = AlertaTipos.DteRechazado,
                Severidad = AlertaSeveridades.Critica,
                Titulo = "Must roll back",
                Mensaje = "Must roll back",
            });
        }
        catch (InvalidOperationException)
        {
            // Expected injected failure after the alert INSERT and before commit.
        }
    }
    Check(!await schema.Alertas.AsNoTracking().AnyAsync(x => x.EmpresaId == rollbackCompany.Id),
        "Outbox enqueue failure rolls back the business alert transaction");

    var indexColumns = await schema.Database.SqlQueryRaw<string>("""
        SELECT c.name AS [Value]
        FROM sys.indexes i
        JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
        JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
        WHERE i.object_id = OBJECT_ID(N'dbo.Notif_Outbox') AND i.is_unique = 1 AND i.is_primary_key = 0
        ORDER BY ic.key_ordinal
        """).ToArrayAsync();
    Check(indexColumns.SequenceEqual(["EmpresaId", "ClaveIdempotencia"]),
        "Outbox unique index is tenant scoped in the physical SQL schema");

    Console.WriteLine($"PASS: {checks.Count} SQL invariants verified.");
}
finally
{
    var current = new SqlConnectionStringBuilder(connection);
    if (current.InitialCatalog != database
        || database != "NotificationOutboxAudit_" + suffix
        || !Guid.TryParseExact(suffix, "N", out _))
        throw new InvalidOperationException("Refusing to delete an unexpected database.");
    await schema.Database.EnsureDeletedAsync();
    Console.WriteLine("Deleted owned synthetic database: " + database);
}

internal sealed class CountingPushSender : IPushSender
{
    private int _calls;
    public int Calls => _calls;

    public async Task<PushResult> EnviarAsync(PushMessage message, CancellationToken ct = default)
    {
        Interlocked.Increment(ref _calls);
        await Task.Delay(200, ct);
        return new PushResult { Success = true, Enviados = message.Tokens.Count };
    }
}

internal sealed class ThrowingOutbox : INotificationOutbox
{
    public Task<int> EnqueueAsync(NotificationOutboxRequest request, CancellationToken ct = default)
        => throw new InvalidOperationException("Injected outbox failure");
}
