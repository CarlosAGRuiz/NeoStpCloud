using Microsoft.Extensions.Options;
using NeoSTP.Application.Notificaciones;
using NeoSTP.Application.Workers;

namespace NeoSTP.Worker.Jobs;

public sealed class NotificationOutboxWorker : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(5);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly NotificationOutboxOptions _options;
    private readonly ILogger<NotificationOutboxWorker> _logger;

    public NotificationOutboxWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<WorkerOptions> options,
        ILogger<NotificationOutboxWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value.NotificationOutbox;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation(
                "NotificationOutboxWorker deshabilitado (Worker:NotificationOutbox:Enabled=false).");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.IntervaloSegundos));
        var nextPurgeAt = DateTime.UtcNow;
        await Task.Delay(InitialDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<INotificationOutboxProcessor>();
                var processed = await processor.ProcessPendingAsync(stoppingToken);
                if (processed > 0)
                    _logger.LogInformation("NotificationOutboxWorker procesó {Count} mensaje(s).", processed);

                if (DateTime.UtcNow >= nextPurgeAt)
                {
                    var purged = await processor.PurgeSentAsync(_options.RetencionDias, stoppingToken);
                    if (purged > 0)
                        _logger.LogInformation("NotificationOutboxWorker purgó {Count} mensaje(s) enviados.", purged);
                    nextPurgeAt = DateTime.UtcNow.AddDays(1);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procesando notification outbox.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
