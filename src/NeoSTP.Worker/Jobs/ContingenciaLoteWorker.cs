using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Dte.Contingencia;
using NeoSTP.Application.Workers;

namespace NeoSTP.Worker.Jobs;

/// <summary>
/// Job periódico que implementa el MOMENTO 3 del ciclo de contingencia:
///
///   1. Detecta eventos de contingencia PROCESADOS sin lote aún y los envía como lote.
///   2. Consulta los lotes que están en estado ENVIADO para obtener sellos individuales.
///
/// Se ejecuta para todas las empresas activas con configuración DTE.
/// Intervalo configurable vía <see cref="ContingenciaLoteOptions.IntervaloMinutos"/>.
/// </summary>
public class ContingenciaLoteWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ContingenciaLoteWorker> _logger;
    private readonly TimeSpan _intervalo;

    public ContingenciaLoteWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<WorkerOptions> options,
        ILogger<ContingenciaLoteWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _intervalo = TimeSpan.FromMinutes(options.Value.ContingenciaLote.IntervaloMinutos);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "ContingenciaLoteWorker iniciado. Intervalo: {Intervalo}", _intervalo);

        // Espera breve al arranque
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

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
                _logger.LogError(ex, "ContingenciaLoteWorker: error inesperado");
            }

            await Task.Delay(_intervalo, stoppingToken);
        }

        _logger.LogInformation("ContingenciaLoteWorker detenido");
    }

    private async Task EjecutarAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IContingenciaLoteService>();
        var db = scope.ServiceProvider.GetRequiredService<NeoSTP.Infrastructure.Persistence.NeoStpDbContext>();

        // Obtener empresas con configuración DTE activa
        var empresaIds = await db.DteConfiguracion
            .Select(c => c.EmpresaId)
            .Distinct()
            .ToListAsync(ct);

        if (empresaIds.Count == 0) return;

        _logger.LogDebug("ContingenciaLoteWorker: procesando {Count} empresas", empresaIds.Count);

        foreach (var empresaId in empresaIds)
        {
            // PASO 1: Crear y enviar lotes de eventos sin lote aún
            var creados = await service.ProcesarEventosSinLoteAsync(empresaId, ct);
            if (creados > 0)
                _logger.LogInformation(
                    "ContingenciaLoteWorker: empresa {EmpresaId} — {Count} lote(s) nuevos enviados",
                    empresaId, creados);

            // PASO 2: Consultar lotes enviados pendientes de sello individual
            var lotesPendientes = await db.DteContingenciaLotes
                .Where(l => l.EmpresaId == empresaId
                         && l.EstadoCodigo == NeoSTP.Domain.Core.Dte.Contingencia.DteContingenciaLoteEstados.Enviado
                         && !string.IsNullOrEmpty(l.CodigoLote))
                .Select(l => l.Id)
                .ToListAsync(ct);

            foreach (var loteId in lotesPendientes)
            {
                var result = await service.ConsultarLoteAsync(loteId, empresaId, ct);
                if (result.IsSuccess)
                    _logger.LogInformation(
                        "ContingenciaLoteWorker: lote {LoteId} consultado — {Proc} DTE procesados",
                        loteId, result.Value?.DteProcesados);
                else
                    _logger.LogWarning(
                        "ContingenciaLoteWorker: lote {LoteId} falló consulta. {Err}",
                        loteId, result.Error);
            }
        }
    }
}
