using Microsoft.Extensions.Options;
using NeoSTP.Application.Ops;

namespace NeoSTP.Worker.Jobs;

/// <summary>
/// Job periódico de respaldo del sistema. Genera un respaldo lógico (manifiesto)
/// cada <see cref="BackupOptions.IntervaloHoras"/> horas y lo sube al storage
/// configurado. Solo se activa si <see cref="BackupOptions.WorkerEnabled"/> es true.
/// </summary>
public class BackupWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackupWorker> _logger;
    private readonly BackupOptions _options;

    public BackupWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<BackupOptions> options,
        ILogger<BackupWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.WorkerEnabled)
        {
            _logger.LogInformation("BackupWorker deshabilitado (Hardening:Backup:WorkerEnabled=false).");
            return;
        }

        var intervalo = TimeSpan.FromHours(Math.Max(1, _options.IntervaloHoras));
        _logger.LogInformation("BackupWorker iniciado. Intervalo: {Intervalo}", intervalo);

        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var backup = scope.ServiceProvider.GetRequiredService<IBackupService>();
                var result = await backup.EjecutarBackupAsync(null, "PROGRAMADO", "worker", stoppingToken);
                if (result.IsSuccess)
                    _logger.LogInformation("BackupWorker: respaldo {Id} completado ({Bytes} bytes)",
                        result.Value!.Id, result.Value.TamanoBytes);
                else
                    _logger.LogWarning("BackupWorker: respaldo falló. {Err}", result.Error);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BackupWorker: error inesperado");
            }

            await Task.Delay(intervalo, stoppingToken);
        }

        _logger.LogInformation("BackupWorker detenido");
    }
}
