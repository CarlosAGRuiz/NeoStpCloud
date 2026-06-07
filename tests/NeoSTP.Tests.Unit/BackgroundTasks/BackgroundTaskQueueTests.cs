using FluentAssertions;
using NeoSTP.Infrastructure.BackgroundTasks;
using Xunit;

namespace NeoSTP.Tests.Unit.BackgroundTasks;

/// <summary>M4.4 — cola de trabajo en proceso (Channel acotado): encolar/desencolar FIFO.</summary>
public class BackgroundTaskQueueTests
{
    [Fact]
    public async Task Encola_Desencola_EjecutaWorkItem()
    {
        var queue = new BackgroundTaskQueue(capacity: 8);
        var ejecutado = false;

        await queue.EnqueueAsync((_, _) => { ejecutado = true; return ValueTask.CompletedTask; });
        queue.Count.Should().Be(1);

        var work = await queue.DequeueAsync(CancellationToken.None);
        await work(null!, CancellationToken.None);

        ejecutado.Should().BeTrue();
        queue.Count.Should().Be(0);
    }

    [Fact]
    public async Task RespetaOrdenFifo()
    {
        var queue = new BackgroundTaskQueue();
        var orden = new List<int>();

        for (int i = 0; i < 5; i++)
        {
            var n = i;
            await queue.EnqueueAsync((_, _) => { orden.Add(n); return ValueTask.CompletedTask; });
        }

        for (int i = 0; i < 5; i++)
        {
            var work = await queue.DequeueAsync(CancellationToken.None);
            await work(null!, CancellationToken.None);
        }

        orden.Should().Equal(0, 1, 2, 3, 4);
    }

    [Fact]
    public async Task Enqueue_Null_Lanza()
    {
        var queue = new BackgroundTaskQueue();
        await FluentActions.Awaiting(() => queue.EnqueueAsync(null!).AsTask())
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Dequeue_Cancelado_Lanza()
    {
        var queue = new BackgroundTaskQueue();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await FluentActions.Awaiting(() => queue.DequeueAsync(cts.Token).AsTask())
            .Should().ThrowAsync<OperationCanceledException>();
    }
}
