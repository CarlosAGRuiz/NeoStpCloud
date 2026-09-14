using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Notificaciones;
using NeoSTP.Application.Workers;
using NeoSTP.Domain.Core.Notificaciones;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

public sealed class NotificationOutboxProcessor : INotificationOutboxProcessor
{
    private static readonly TimeSpan[] RetryBackoff =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(6),
    ];

    private readonly NeoStpDbContext _db;
    private readonly IPushSender _push;
    private readonly NotificationOutboxOptions _options;
    private readonly ILogger<NotificationOutboxProcessor> _logger;
    private readonly IDteCorreoOutboxDispatcher? _dteCorreo;

    public NotificationOutboxProcessor(
        NeoStpDbContext db,
        IPushSender push,
        IOptions<WorkerOptions> options,
        ILogger<NotificationOutboxProcessor> logger,
        IDteCorreoOutboxDispatcher? dteCorreo = null)
    {
        _db = db;
        _push = push;
        _options = options.Value.NotificationOutbox;
        _logger = logger;
        _dteCorreo = dteCorreo;
    }

    public async Task<int> ProcessPendingAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var candidates = await _db.NotificationOutbox.AsNoTracking()
            .Where(x => ((x.Estado == NotificationOutboxEstados.Pending
                          || x.Estado == NotificationOutboxEstados.Failed)
                         && x.DisponibleDesde <= now)
                     || (x.Estado == NotificationOutboxEstados.Processing
                         && (x.LeaseExpiresAt == null || x.LeaseExpiresAt <= now)))
            .OrderBy(x => x.DisponibleDesde)
            .ThenBy(x => x.Id)
            .Select(x => x.Id)
            .Take(Math.Clamp(_options.LoteMaximo, 1, 200))
            .ToListAsync(ct);

        var processed = 0;
        foreach (var id in candidates)
        {
            if (await ProcessOneAsync(id, ct))
                processed++;
        }

        return processed;
    }

    public async Task<int> PurgeSentAsync(int retentionDays, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-Math.Clamp(retentionDays, 1, 3650));
        if (_db.Database.IsRelational())
        {
            return await _db.NotificationOutbox
                .Where(x => x.Estado == NotificationOutboxEstados.Sent
                    && x.ProcesadoAt != null && x.ProcesadoAt < cutoff)
                .ExecuteDeleteAsync(ct);
        }

        var expired = await _db.NotificationOutbox
            .Where(x => x.Estado == NotificationOutboxEstados.Sent
                && x.ProcesadoAt != null && x.ProcesadoAt < cutoff)
            .ToListAsync(ct);
        _db.NotificationOutbox.RemoveRange(expired);
        await _db.SaveChangesAsync(ct);
        return expired.Count;
    }

    private async Task<bool> ProcessOneAsync(int id, CancellationToken ct)
    {
        var leaseId = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;
        if (!await TryClaimAsync(id, leaseId, now, ct))
            return false;

        _db.ChangeTracker.Clear();
        var message = await _db.NotificationOutbox.AsNoTracking()
            .FirstAsync(x => x.Id == id && x.LeaseId == leaseId, ct);

        DispatchResult dispatch;
        try
        {
            dispatch = await DispatchAsync(message, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Fallo no confirmado procesando notification outbox id={OutboxId} tipo={Tipo}",
                message.Id, message.Tipo);
            dispatch = DispatchResult.Retry("El proveedor no confirmó el resultado.");
        }

        await CompleteAsync(message, leaseId, dispatch, ct);
        return true;
    }

    private async Task<bool> TryClaimAsync(int id, string leaseId, DateTime now, CancellationToken ct)
    {
        var leaseUntil = now.AddSeconds(Math.Clamp(_options.LeaseSegundos, 30, 900));
        if (_db.Database.IsRelational())
        {
            var changed = await _db.NotificationOutbox
                .Where(x => x.Id == id
                    && (((x.Estado == NotificationOutboxEstados.Pending
                          || x.Estado == NotificationOutboxEstados.Failed)
                         && x.DisponibleDesde <= now)
                        || (x.Estado == NotificationOutboxEstados.Processing
                            && (x.LeaseExpiresAt == null || x.LeaseExpiresAt <= now))))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Estado, NotificationOutboxEstados.Processing)
                    .SetProperty(x => x.LeaseId, leaseId)
                    .SetProperty(x => x.LeaseExpiresAt, leaseUntil)
                    .SetProperty(x => x.Intentos, x => x.Intentos + 1)
                    .SetProperty(x => x.UpdatedAt, now), ct);
            return changed == 1;
        }

        var message = await _db.NotificationOutbox.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (message is null || !IsDue(message, now))
            return false;
        message.Estado = NotificationOutboxEstados.Processing;
        message.LeaseId = leaseId;
        message.LeaseExpiresAt = leaseUntil;
        message.Intentos++;
        message.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<DispatchResult> DispatchAsync(NotificationOutboxMessage message, CancellationToken ct)
    {
        if (message.Canal == NotificationOutboxCanales.Email
            && message.Tipo == NotificationOutboxTipos.DteCorreo)
        {
            DteCorreoOutboxPayload? emailPayload;
            try { emailPayload = JsonSerializer.Deserialize<DteCorreoOutboxPayload>(message.Payload); }
            catch (JsonException) { return DispatchResult.Dead("Payload inválido."); }
            if (emailPayload is null || emailPayload.EmpresaId != message.EmpresaId
                || emailPayload.DteDocumentoId != message.EntidadId
                || emailPayload.Finalidad != message.Finalidad)
                return DispatchResult.Dead("Payload inválido para la empresa o DTE del mensaje.");
            if (_dteCorreo is null)
                return DispatchResult.Dead("Dispatcher de correo DTE no configurado.");

            var emailResult = await _dteCorreo.EnviarAsync(message, emailPayload, ct);
            if (emailResult.Success)
                return DispatchResult.Ok([], emailResult.MessageId);
            return emailResult.Mensaje is "PAYLOAD_INVALIDO" or "DTE_NO_ENCONTRADO" or "DTE_NO_PROCESADO"
                ? DispatchResult.Dead("La entrega de correo ya no es válida.")
                : DispatchResult.Retry("El proveedor no confirmó el correo.");
        }

        if (message.Canal != NotificationOutboxCanales.Push
            || message.Tipo != NotificationOutboxTipos.AlertaCreada)
            return DispatchResult.Dead("Tipo o canal no soportado por el dispatcher.");

        AlertaPushOutboxPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<AlertaPushOutboxPayload>(message.Payload);
        }
        catch (JsonException)
        {
            return DispatchResult.Dead("Payload inválido.");
        }

        if (payload is null || payload.EmpresaId != message.EmpresaId)
            return DispatchResult.Dead("Payload inválido para la empresa del mensaje.");

        var alerta = await _db.Alertas.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == payload.AlertaId && x.EmpresaId == message.EmpresaId, ct);
        if (alerta is null)
            return DispatchResult.Dead("La alerta de origen no existe.");

        var devicesQuery = _db.DispositivosNotificacion.AsNoTracking()
            .Where(x => x.EmpresaId == message.EmpresaId && x.Activo);
        if (alerta.UsuarioId is int userId)
            devicesQuery = devicesQuery.Where(x => x.UsuarioId == userId);
        var tokens = await devicesQuery.Select(x => x.Token).ToListAsync(ct);
        if (tokens.Count == 0)
            return DispatchResult.Ok([]);

        var result = await _push.EnviarAsync(new PushMessage
        {
            Tokens = tokens,
            Titulo = alerta.Titulo,
            Cuerpo = alerta.Mensaje,
            Data = new Dictionary<string, string>
            {
                ["tipo"] = alerta.TipoCodigo,
                ["alertaId"] = alerta.Id.ToString(),
                ["entidadTipo"] = alerta.EntidadTipo ?? string.Empty,
                ["entidadId"] = alerta.EntidadId?.ToString() ?? string.Empty,
                ["outboxId"] = message.Id.ToString(),
                ["idempotencyKey"] = message.ClaveIdempotencia,
            },
        }, ct);

        return result.Success
            ? DispatchResult.Ok(result.InvalidTokens)
            : DispatchResult.Retry("El proveedor rechazó temporalmente el envío.");
    }

    private async Task CompleteAsync(
        NotificationOutboxMessage snapshot,
        string leaseId,
        DispatchResult result,
        CancellationToken ct)
    {
        _db.ChangeTracker.Clear();
        var owned = await _db.NotificationOutbox.FirstOrDefaultAsync(x => x.Id == snapshot.Id
            && x.Estado == NotificationOutboxEstados.Processing && x.LeaseId == leaseId, ct);
        if (owned is null)
        {
            _logger.LogWarning("Se perdió el lease de notification outbox id={OutboxId}", snapshot.Id);
            return;
        }

        var now = DateTime.UtcNow;
        if (result.Success)
        {
            if (result.InvalidTokens.Count > 0)
            {
                var invalidDevices = await _db.DispositivosNotificacion
                    .Where(x => x.EmpresaId == owned.EmpresaId && x.Activo
                        && result.InvalidTokens.Contains(x.Token))
                    .ToListAsync(ct);
                foreach (var device in invalidDevices)
                {
                    device.Activo = false;
                    device.UpdatedAt = now;
                }
            }

            owned.Estado = NotificationOutboxEstados.Sent;
            owned.ProcesadoAt = now;
            owned.ErrorUltimo = null;
            owned.ProveedorMessageId = result.MessageId;
        }
        else
        {
            var exhausted = owned.Intentos >= owned.MaxIntentos;
            owned.Estado = result.Permanent || exhausted
                ? NotificationOutboxEstados.Dead
                : NotificationOutboxEstados.Failed;
            owned.ErrorUltimo = result.Error;
            if (owned.Estado == NotificationOutboxEstados.Failed)
                owned.DisponibleDesde = now + RetryBackoff[Math.Min(owned.Intentos - 1, RetryBackoff.Length - 1)];
        }

        owned.LeaseId = null;
        owned.LeaseExpiresAt = null;
        owned.UpdatedAt = now;

        if (_dteCorreo is not null && owned.Tipo == NotificationOutboxTipos.DteCorreo)
        {
            try
            {
                var payload = JsonSerializer.Deserialize<DteCorreoOutboxPayload>(owned.Payload);
                if (payload is not null)
                    await _dteCorreo.ActualizarAlertaAsync(owned, payload, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo actualizar la alerta del correo DTE para outbox id={OutboxId}", owned.Id);
            }
        }
        await _db.SaveChangesAsync(ct);
    }

    private static bool IsDue(NotificationOutboxMessage message, DateTime now)
        => ((message.Estado == NotificationOutboxEstados.Pending
             || message.Estado == NotificationOutboxEstados.Failed)
            && message.DisponibleDesde <= now)
           || (message.Estado == NotificationOutboxEstados.Processing
               && (message.LeaseExpiresAt is null || message.LeaseExpiresAt <= now));

    private sealed record DispatchResult(
        bool Success,
        bool Permanent,
        string? Error,
        IReadOnlyList<string> InvalidTokens,
        string? MessageId)
    {
        public static DispatchResult Ok(IReadOnlyList<string> invalidTokens, string? messageId = null)
            => new(true, false, null, invalidTokens, messageId);

        public static DispatchResult Retry(string error)
            => new(false, false, error, [], null);

        public static DispatchResult Dead(string error)
            => new(false, true, error, [], null);
    }
}
