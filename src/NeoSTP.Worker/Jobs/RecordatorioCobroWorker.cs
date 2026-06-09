using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Cobranza;
using NeoSTP.Application.Cobranza.Dtos;
using NeoSTP.Application.Workers;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Worker.Jobs;

/// <summary>
/// Job periodico de recordatorios de cobranza vencida. Deshabilitado por defecto para evitar
/// envios accidentales; se activa con Worker:RecordatoriosCobro:Enabled=true.
/// </summary>
public class RecordatorioCobroWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RecordatorioCobroWorker> _logger;
    private readonly RecordatoriosCobroOptions _options;

    public RecordatorioCobroWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<WorkerOptions> options,
        ILogger<RecordatorioCobroWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value.RecordatoriosCobro;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("RecordatorioCobroWorker deshabilitado (Worker:RecordatoriosCobro:Enabled=false).");
            return;
        }

        var intervalo = TimeSpan.FromHours(Math.Max(1, _options.IntervaloHoras));
        _logger.LogInformation("RecordatorioCobroWorker iniciado. Intervalo: {Intervalo}h", intervalo.TotalHours);
        await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EjecutarAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RecordatorioCobroWorker: error inesperado");
            }

            await Task.Delay(intervalo, stoppingToken);
        }

        _logger.LogInformation("RecordatorioCobroWorker detenido");
    }

    private async Task EjecutarAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NeoStpDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IRecordatorioCobroService>();

        var empresas = await db.Empresas.AsNoTracking()
            .Where(e => e.EstadoCodigo == "ACTIVA")
            .Select(e => e.Id)
            .ToListAsync(ct);

        var totalEmail = 0;
        var totalWhatsapp = 0;
        var totalFallidos = 0;
        foreach (var empresaId in empresas)
        {
            var result = await service.EjecutarAsync(empresaId, new EjecutarRecordatoriosCobroRequest
            {
                DiasVencidoMinimo = _options.DiasVencidoMinimo,
                Maximo = _options.MaximoPorEmpresa,
                EnviarEmail = _options.EnviarEmail,
                EnviarWhatsApp = _options.EnviarWhatsApp,
            }, "worker:recordatorios-cobro", ct);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("RecordatorioCobroWorker: empresa {EmpresaId} fallo. {Error}", empresaId, result.Error);
                continue;
            }

            totalEmail += result.Value!.EnviadosEmail;
            totalWhatsapp += result.Value.EnviadosWhatsApp;
            totalFallidos += result.Value.Fallidos;
        }

        _logger.LogInformation(
            "RecordatorioCobroWorker: email={Email}, whatsapp={WhatsApp}, fallidos={Fallidos}, empresas={Empresas}",
            totalEmail, totalWhatsapp, totalFallidos, empresas.Count);
    }
}
