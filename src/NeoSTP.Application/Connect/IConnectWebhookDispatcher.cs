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
/// Payload de un evento de negocio (E6): cobros, compras, inventario, agenda.
/// A diferencia del DTE, aquí la forma varía por evento, así que los datos propios
/// van en <see cref="Datos"/> y el consumidor los lee por nombre.
/// </summary>
public sealed record ConnectEventoNegocioPayload
{
    public string Evento { get; init; } = null!;
    public int EmpresaId { get; init; }

    /// <summary>Entidad de origen: "PagoCliente", "OrdenCompra", "Producto", "Cita".</summary>
    public string EntidadTipo { get; init; } = null!;
    public int EntidadId { get; init; }

    /// <summary>Resumen legible, para logs y para el humano que depura la integración.</summary>
    public string? Descripcion { get; init; }

    /// <summary>Datos propios del evento (monto, saldo, cantidad…).</summary>
    public IReadOnlyDictionary<string, object?> Datos { get; init; }
        = new Dictionary<string, object?>();

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
    /// Igual que <see cref="DispatchAsync(ConnectDteEventoPayload, CancellationToken)"/> pero
    /// para eventos de negocio (E6): cobros, compras, inventario, agenda. Mismo transporte,
    /// misma firma HMAC y mismos reintentos. Best-effort: nunca rompe la operación que lo emite.
    /// </summary>
    Task DispatchNegocioAsync(ConnectEventoNegocioPayload payload, CancellationToken ct = default);

    /// <summary>
    /// Procesa las entregas pendientes con <c>ProximoIntento &lt;= now</c>.
    /// Devuelve el número de entregas procesadas en este ciclo.
    /// </summary>
    Task<int> ProcesarPendientesAsync(CancellationToken ct = default);
}
