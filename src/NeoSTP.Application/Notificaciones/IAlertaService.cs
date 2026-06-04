using NeoSTP.Application.Common;
using NeoSTP.Application.Notificaciones.Dtos;

namespace NeoSTP.Application.Notificaciones;

/// <summary>
/// Centro de alertas y notificaciones: gestiona alertas por empresa/usuario, dispositivos
/// (FCM) y preferencias. Crear una alerta dispara el push (best-effort). Aislado por EmpresaId.
/// </summary>
public interface IAlertaService
{
    // ── Alertas (centro de notificaciones) ──
    Task<Result<PagedResult<AlertaDto>>> ListarAsync(int empresaId, int usuarioId, AlertaQuery query, CancellationToken ct = default);
    Task<AlertaResumenDto> ResumenAsync(int empresaId, int usuarioId, CancellationToken ct = default);
    Task<Result> MarcarLeidaAsync(int empresaId, int usuarioId, int alertaId, CancellationToken ct = default);
    Task<Result> ResolverAsync(int empresaId, int usuarioId, int alertaId, CancellationToken ct = default);
    Task<Result> MarcarTodasLeidasAsync(int empresaId, int usuarioId, CancellationToken ct = default);

    /// <summary>Crea (o actualiza por <c>Clave</c>) una alerta y envía push best-effort.</summary>
    Task<Result<AlertaDto>> CrearAsync(CrearAlertaRequest request, CancellationToken ct = default);

    // ── Dispositivos (push) ──
    Task<Result> RegistrarDispositivoAsync(int empresaId, int usuarioId, RegistrarDispositivoRequest request, CancellationToken ct = default);
    Task<Result> EliminarDispositivoAsync(int empresaId, int usuarioId, string token, CancellationToken ct = default);

    // ── Preferencias ──
    Task<PreferenciaNotificacionDto> GetPreferenciasAsync(int empresaId, int usuarioId, CancellationToken ct = default);
    Task<Result> GuardarPreferenciasAsync(int empresaId, int usuarioId, PreferenciaNotificacionDto request, CancellationToken ct = default);
}

/// <summary>Genera alertas a partir de datos reales (DTE rechazado, certificado por vencer, facturas vencidas).</summary>
public interface IAlertaGeneracionService
{
    /// <summary>Recalcula y persiste (upsert) las alertas de la empresa. Devuelve cuántas se crearon nuevas.</summary>
    Task<int> GenerarAsync(int empresaId, CancellationToken ct = default);
}
