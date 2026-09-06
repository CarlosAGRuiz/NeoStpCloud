using Microsoft.Extensions.Options;
using NeoSTP.Application.Billing;
using NeoSTP.Application.Workers;

namespace NeoSTP.Worker.Jobs;

/// <summary>
/// Procesa operaciones Billing previamente persistidas. El processor conserva la
/// responsabilidad de reclamar, ejecutar y finalizar cada operación de forma durable.
/// </summary>
public sealed class BillingProviderOperationWorker : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(10);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BillingProviderOperationWorker> _logger;
    private readonly BillingProviderOperationOptions _options;

    public BillingProviderOperationWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<WorkerOptions> options,
        ILogger<BillingProviderOperationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value.BillingProviderOperations;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation(
                "BillingProviderOperationWorker deshabilitado (Worker:BillingProviderOperations:Enabled=false).");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.IntervaloSegundos));
        _logger.LogInformation(
            "BillingProviderOperationWorker iniciado. Intervalo: {Intervalo}s", interval.TotalSeconds);

        await Task.Delay(InitialDelay, stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IBillingProviderOperationProcessor>();
                var processed = await processor.ProcessPendingAsync(stoppingToken);
                if (processed > 0)
                {
                    _logger.LogInformation(
                        "BillingProviderOperationWorker: {Count} operaciones procesadas", processed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BillingProviderOperationWorker: error inesperado");
            }

            await Task.Delay(interval, stoppingToken);
        }

        _logger.LogInformation("BillingProviderOperationWorker detenido");
    }
}
