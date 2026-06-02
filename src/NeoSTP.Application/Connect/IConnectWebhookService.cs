using NeoSTP.Application.Common;

namespace NeoSTP.Application.Connect;

// ─── DTOs ────────────────────────────────────────────────────────────────────

public sealed record ConnectWebhookDto
{
    public int Id { get; init; }
    public int EmpresaId { get; init; }
    public int? ApiKeyId { get; init; }
    public string Url { get; init; } = null!;
    /// <summary>Secreto HMAC truncado para display (primeros 8 chars + …).</summary>
    public string SecretoHmacDisplay { get; init; } = null!;
    public string[] Eventos { get; init; } = [];
    public bool Activo { get; init; }
    public DateTime? UltimaEntregaAt { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed record CrearWebhookRequest
{
    public int EmpresaId { get; init; }
    public int? ApiKeyId { get; init; }
    public string Url { get; init; } = null!;
    public string[] Eventos { get; init; } = [];
}

public sealed record ConnectWebhookDeliveryDto
{
    public int Id { get; init; }
    public int WebhookId { get; init; }
    public string Evento { get; init; } = null!;
    public string Estado { get; init; } = null!;
    public int? HttpStatus { get; init; }
    public int Intentos { get; init; }
    public DateTime? EntregadoAt { get; init; }
    public string? Error { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed record ConnectUsageDto
{
    public int? ApiKeyId { get; init; }
    public string? ApiKeyNombre { get; init; }
    public long TotalPeticiones { get; init; }
    public long PeticionesHoy { get; init; }
    public long PeticionesUltimos7Dias { get; init; }
    public long WebhooksEntregados { get; init; }
    public long WebhooksFallidos { get; init; }
}

// ─── Interfaz ────────────────────────────────────────────────────────────────

public interface IConnectWebhookService
{
    Task<IReadOnlyList<ConnectWebhookDto>> ListarAsync(int empresaId, CancellationToken ct = default);
    Task<Result<ConnectWebhookDto>> ObtenerAsync(int id, int empresaId, CancellationToken ct = default);
    Task<Result<ConnectWebhookDto>> CrearAsync(CrearWebhookRequest request, string? actor, CancellationToken ct = default);
    Task<Result> EliminarAsync(int id, int empresaId, string? actor, CancellationToken ct = default);
    Task<Result<ConnectWebhookDeliveryDto>> TestAsync(int webhookId, int empresaId, string? actor, CancellationToken ct = default);

    Task<IReadOnlyList<ConnectWebhookDeliveryDto>> ListarDeliveriesAsync(int empresaId, int limit, CancellationToken ct = default);
    Task<IReadOnlyList<ConnectUsageDto>> ObtenerUsageAsync(int empresaId, CancellationToken ct = default);
}
