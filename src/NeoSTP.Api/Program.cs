using NeoSTP.Infrastructure;
using NeoSTP.Shared;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowAnyOrigin());
});

var app = builder.Build();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", () => ApiResponse<object>.Ok(new
{
    status = "ok",
    service = "NeoSTP.Api",
    timestamp = DateTime.UtcNow
}));

try
{
    Log.Information("Starting NeoSTP.Api host");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "NeoSTP.Api terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
