namespace NeoSTP.Application.Connect;

/// <summary>
/// Payload normalizado de un evento DTE que se envía a los webhooks suscritos.
/// </summary>
public sealed record ConnectDteEventoPayload
{
    public string Evento { get; init; } = null!;
    public int EmpresaId { get; init; }
    public int DteId { get; init; }
    public string CodigoGeneracion { get; init; } = null!;
    public string TipoDte { get; init; } = null!;
    public string Estado { get; init; } = null!;
    public DateTime OcurrioAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Dispatcher de webhooks NeoConnect.
/// - <see cref="DispatchAsync"/>: llamado por <c>DteDocumentosService</c> al cambiar
///   el estado de un DTE; crea registros <c>ConnectWebhookDelivery</c> en PENDIENTE
///   para cada webhook activo suscrito al evento.
/// - <see cref="ProcesarPendientesAsync"/>: llamado por <c>ConnectWebhookDeliveryWorker</c>;
///   envía las entregas pendientes con firma HMAC-SHA256 y aplica reintentos con backoff.
/// </summary>
public interface IConnectWebhookDispatcher
{
    /// <summary>
    /// Crea registros de entrega PENDIENTE para todos los webhooks de la empresa
    /// suscritos al evento indicado. Best-effort: nunca lanza excepción.
    /// </summary>
    Task DispatchAsync(ConnectDteEventoPayload payload, CancellationToken ct = default);

    /// <summary>
    /// Procesa las entregas pendientes con <c>ProximoIntento &lt;= now</c>.
    /// Devuelve el número de entregas procesadas en este ciclo.
    /// </summary>
    Task<int> ProcesarPendientesAsync(CancellationToken ct = default);
}
