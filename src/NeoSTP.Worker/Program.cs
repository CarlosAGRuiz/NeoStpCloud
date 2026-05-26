using NeoSTP.Worker;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Services.AddSerilog((services, configuration) => configuration
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

try
{
    Log.Information("Starting NeoSTP.Worker host");
    host.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "NeoSTP.Worker terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
