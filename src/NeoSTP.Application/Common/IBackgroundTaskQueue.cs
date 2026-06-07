namespace NeoSTP.Application.Common;

/// <summary>
/// Cola de trabajo en proceso (M4.4) para descargar tareas pesadas (OCR, push masivo,
/// generación de reportes) fuera del request. El work item recibe un IServiceProvider
/// (de un scope nuevo creado por el consumidor) para resolver servicios scoped.
/// </summary>
public interface IBackgroundTaskQueue
{
    /// <summary>Encola una unidad de trabajo. Bloquea si la cola está llena (backpressure).</summary>
    ValueTask EnqueueAsync(Func<IServiceProvider, CancellationToken, ValueTask> workItem, CancellationToken ct = default);

    /// <summary>Saca la siguiente unidad de trabajo (espera si la cola está vacía).</summary>
    ValueTask<Func<IServiceProvider, CancellationToken, ValueTask>> DequeueAsync(CancellationToken ct);

    /// <summary>Cantidad aproximada de items pendientes.</summary>
    int Count { get; }
}
