using System.Threading.Channels;
using NeoSTP.Application.Common;

namespace NeoSTP.Infrastructure.BackgroundTasks;

/// <summary>
/// Cola de trabajo en proceso respaldada por un <see cref="Channel{T}"/> acotado (M4.4).
/// Acotada para aplicar backpressure: si se llena, el productor espera al encolar.
/// </summary>
public sealed class BackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly Channel<Func<IServiceProvider, CancellationToken, ValueTask>> _channel;

    public BackgroundTaskQueue(int capacity = 200)
    {
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
        };
        _channel = Channel.CreateBounded<Func<IServiceProvider, CancellationToken, ValueTask>>(options);
    }

    public int Count => _channel.Reader.Count;

    public async ValueTask EnqueueAsync(Func<IServiceProvider, CancellationToken, ValueTask> workItem, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        await _channel.Writer.WriteAsync(workItem, ct);
    }

    public async ValueTask<Func<IServiceProvider, CancellationToken, ValueTask>> DequeueAsync(CancellationToken ct)
        => await _channel.Reader.ReadAsync(ct);
}
