using NeoSTP.Application.Workers;
using NeoSTP.Infrastructure;
using NeoSTP.Worker;
using NeoSTP.Worker.Jobs;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// ── Logging ───────────────────────────────────────────────────────
builder.Services.AddSerilog((services, configuration) => configuration
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// ── Infraestructura (EF Core, servicios DTE, etc.) ────────────────
builder.Services.AddInfrastructure(builder.Configuration);

// ── Configuración del Worker ──────────────────────────────────────
builder.Services.Configure<WorkerOptions>(
    builder.Configuration.GetSection(WorkerOptions.SectionName));

// ── Hosted services ───────────────────────────────────────────────
builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<RetransmisionContingenciaWorker>();
builder.Services.AddHostedService<LimpiezaTokensWorker>();

var host = builder.Build();

try
{
    Log.Information("Iniciando NeoSTP.Worker host");
    host.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "NeoSTP.Worker terminó inesperadamente");
}
finally
{
    Log.CloseAndFlush();
}
