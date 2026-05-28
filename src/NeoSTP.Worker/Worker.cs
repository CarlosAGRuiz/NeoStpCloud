namespace NeoSTP.Worker;

/// <summary>
/// Worker de arranque: registra el inicio del host y termina.
/// Los jobs reales están en <see cref="Jobs.RetransmisionContingenciaWorker"/>
/// y <see cref="Jobs.LimpiezaTokensWorker"/>.
/// </summary>
public class Worker(ILogger<Worker> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "NeoSTP.Worker arrancado a las {Time}. Jobs activos: RetransmisionContingencia, LimpiezaTokens",
            DateTimeOffset.Now);
        return Task.CompletedTask;
    }
}
