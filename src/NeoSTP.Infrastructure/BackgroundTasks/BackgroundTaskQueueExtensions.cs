using Microsoft.Extensions.DependencyInjection;
using NeoSTP.Application.Common;

namespace NeoSTP.Infrastructure.BackgroundTasks;

public static class BackgroundTaskQueueExtensions
{
    /// <summary>
    /// Registra la cola de trabajo en proceso (M4.4) y su consumidor hospedado. Encolar y
    /// consumir ocurren en el mismo proceso, así que el host que registra esto procesa las tareas.
    /// </summary>
    public static IServiceCollection AddBackgroundTaskQueue(this IServiceCollection services, int capacity = 200)
    {
        services.AddSingleton<IBackgroundTaskQueue>(_ => new BackgroundTaskQueue(capacity));
        services.AddHostedService<QueuedHostedService>();
        return services;
    }
}
