using Microsoft.Extensions.Hosting.WindowsServices;
using NeoSTP.Application.Workers;
using NeoSTP.Infrastructure;
using NeoSTP.Infrastructure.BackgroundTasks;
using NeoSTP.Infrastructure.Diagnostics;
using NeoSTP.Worker;
using NeoSTP.Worker.Jobs;
using Serilog;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : null
});
builder.Services.AddWindowsService(options => options.ServiceName = "NeoSTP.Worker");

HostConfiguration.AddLocalDevelopmentSettings(builder.Configuration, builder.Environment);
HostConfiguration.AddExternalDeploymentSettings(builder.Configuration, builder.Environment);

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
// Production workers require explicit activation after operational acceptance.
WorkerStartupPolicy.ConfigureJobs(builder.Services, builder.Configuration, builder.Environment, services =>
{
    services.AddHostedService<Worker>();
    services.AddHostedService<RetransmisionContingenciaWorker>();
    services.AddHostedService<LimpiezaTokensWorker>();
    services.AddHostedService<ContingenciaLoteWorker>();
    services.AddHostedService<BackupWorker>();
    services.AddHostedService<ConnectWebhookDeliveryWorker>();
    services.AddHostedService<BillingProviderOperationWorker>();
    services.AddHostedService<NotificationOutboxWorker>();
    services.AddHostedService<AlertaGeneracionWorker>();
    services.AddHostedService<RecordatorioCobroWorker>();
    services.AddHostedService<LimpiezaAuditoriaWorker>();

    // ── Cola de trabajo en proceso (M4.4): descarga de tareas pesadas ─
    services.AddBackgroundTaskQueue();
});

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
