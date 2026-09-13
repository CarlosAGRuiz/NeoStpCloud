using Microsoft.Extensions.Hosting.WindowsServices;
using NeoSTP.Application.Workers;
using NeoSTP.Infrastructure;
using NeoSTP.Infrastructure.BackgroundTasks;
using NeoSTP.Infrastructure.Diagnostics;
using NeoSTP.Worker;
using NeoSTP.Worker.Jobs;
using Serilog;

var externalDeploymentPath = HostConfiguration.GetExternalDeploymentPath(args);
var useDeploymentContentRoot = WindowsServiceHelpers.IsWindowsService()
    || HostConfiguration.IsDeployedEnvironment(
        args,
        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
        Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT"));
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = useDeploymentContentRoot ? AppContext.BaseDirectory : null
});
builder.Services.AddWindowsService(options => options.ServiceName = "NeoSTP.Worker");

HostConfiguration.AddLocalDevelopmentSettings(builder.Configuration, builder.Environment);
HostConfiguration.AddExternalDeploymentSettings(
    builder.Configuration, builder.Environment, externalDeploymentPath);

// ── Logging ───────────────────────────────────────────────────────
builder.Services.AddSerilog((services, configuration) => configuration
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// ── Infraestructura (EF Core, servicios DTE, etc.) ────────────────
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddNeoStpObservability(builder.Configuration, "neostp-worker");

// ── Configuración del Worker ──────────────────────────────────────
builder.Services.Configure<WorkerOptions>(
    builder.Configuration.GetSection(WorkerOptions.SectionName));
builder.Services.Configure<NeoSTP.Application.Ops.BackupOptions>(
    builder.Configuration.GetSection(NeoSTP.Application.Ops.BackupOptions.SectionName));

// ── Hosted services ───────────────────────────────────────────────
// Staging/Production require Worker:Enabled=true plus each Worker:<Job>:Enabled=true.
WorkerStartupPolicy.ConfigureJob(builder.Services, builder.Configuration, builder.Environment,
    "RetransmisionContingencia", services => services.AddHostedService<RetransmisionContingenciaWorker>());
WorkerStartupPolicy.ConfigureJob(builder.Services, builder.Configuration, builder.Environment,
    "LimpiezaTokens", services => services.AddHostedService<LimpiezaTokensWorker>());
WorkerStartupPolicy.ConfigureJob(builder.Services, builder.Configuration, builder.Environment,
    "ContingenciaLote", services => services.AddHostedService<ContingenciaLoteWorker>());
WorkerStartupPolicy.ConfigureJob(builder.Services, builder.Configuration, builder.Environment,
    "WebhookDelivery", services => services.AddHostedService<ConnectWebhookDeliveryWorker>());
WorkerStartupPolicy.ConfigureJob(builder.Services, builder.Configuration, builder.Environment,
    "BillingProviderOperations", services => services.AddHostedService<BillingProviderOperationWorker>());
WorkerStartupPolicy.ConfigureJob(builder.Services, builder.Configuration, builder.Environment,
    "NotificationOutbox", services => services.AddHostedService<NotificationOutboxWorker>());
WorkerStartupPolicy.ConfigureJob(builder.Services, builder.Configuration, builder.Environment,
    "GeneracionAlertas", services => services.AddHostedService<AlertaGeneracionWorker>());
WorkerStartupPolicy.ConfigureJob(builder.Services, builder.Configuration, builder.Environment,
    "RecordatoriosCobro", services => services.AddHostedService<RecordatorioCobroWorker>());
WorkerStartupPolicy.ConfigureJob(builder.Services, builder.Configuration, builder.Environment,
    "LimpiezaAuditoria", services => services.AddHostedService<LimpiezaAuditoriaWorker>());

// Backup retains its dedicated hardening switch and also requires the master switch.
if (WorkerStartupPolicy.IsJobEnabled(builder.Configuration, builder.Environment, "Backup")
    && builder.Configuration.GetValue<bool>("Hardening:Backup:WorkerEnabled"))
{
    builder.Services.AddHostedService<BackupWorker>();
}

// In-process task execution is independently activated in deployed environments.
WorkerStartupPolicy.ConfigureJob(builder.Services, builder.Configuration, builder.Environment,
    "BackgroundTasks", services => services.AddBackgroundTaskQueue());

var host = builder.Build();

// Fail-fast: en Producción no se arranca con providers Mock (whatsapp, push, etc.).
NeoSTP.Infrastructure.Diagnostics.ProductionGuards.ValidarProvidersDeProduccion(
    builder.Configuration, builder.Environment);

// Workers have never owned schema deployment, including in Development.
var databaseConfiguration = WorkerStartupPolicy.CreateDatabaseConfiguration(builder.Configuration);
await DatabaseStartup.InitializeAsync(host.Services, databaseConfiguration, builder.Environment);
try
{
    Log.Information("Iniciando NeoSTP.Worker host");
    host.Run();
}
catch (Exception ex)
{
    Environment.ExitCode = 1;
    Log.Fatal(ex, "NeoSTP.Worker terminó inesperadamente");
}
finally
{
    Log.CloseAndFlush();
}
