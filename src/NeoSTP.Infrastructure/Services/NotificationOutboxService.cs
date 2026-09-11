using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Notificaciones;
using NeoSTP.Domain.Core.Notificaciones;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

public sealed class NotificationOutboxService : INotificationOutbox
{
    private const int MaxPayloadLength = 262_144;
    private readonly NeoStpDbContext _db;

    public NotificationOutboxService(NeoStpDbContext db) => _db = db;

    public async Task<int> EnqueueAsync(NotificationOutboxRequest request, CancellationToken ct = default)
    {
        Validate(request);

        var existingId = await _db.NotificationOutbox
            .Where(x => x.EmpresaId == request.EmpresaId
                && x.ClaveIdempotencia == request.ClaveIdempotencia)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(ct);
        if (existingId is int id)
            return id;

        var message = new NotificationOutboxMessage
        {
            EmpresaId = request.EmpresaId,
            Tipo = request.Tipo.Trim().ToUpperInvariant(),
            Canal = request.Canal.Trim().ToUpperInvariant(),
            Destinatario = string.IsNullOrWhiteSpace(request.Destinatario)
                ? null : request.Destinatario.Trim(),
            Payload = request.Payload,
            ClaveIdempotencia = request.ClaveIdempotencia.Trim(),
            Estado = NotificationOutboxEstados.Pending,
            MaxIntentos = Math.Clamp(request.MaxIntentos, 1, 20),
            DisponibleDesde = DateTime.UtcNow,
        };

        _db.NotificationOutbox.Add(message);
        try
        {
            await _db.SaveChangesAsync(ct);
            return message.Id;
        }
        catch (DbUpdateException)
        {
            // Otra instancia pudo confirmar la misma clave después de nuestra lectura.
            // La restricción única es el árbitro; sólo absorbemos el error si la fila
            // canónica realmente existe.
            _db.Entry(message).State = EntityState.Detached;
            var racedId = await _db.NotificationOutbox.AsNoTracking()
                .Where(x => x.EmpresaId == request.EmpresaId
                    && x.ClaveIdempotencia == request.ClaveIdempotencia)
                .Select(x => (int?)x.Id)
                .FirstOrDefaultAsync(ct);
            if (racedId is int canonicalId)
                return canonicalId;
            throw;
        }
    }

    private static void Validate(NotificationOutboxRequest request)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.EmpresaId);
        if (string.IsNullOrWhiteSpace(request.Tipo) || request.Tipo.Length > 60)
            throw new ArgumentException("El tipo de notificación es obligatorio y admite hasta 60 caracteres.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Canal) || request.Canal.Length > 20)
            throw new ArgumentException("El canal de notificación es obligatorio y admite hasta 20 caracteres.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.ClaveIdempotencia) || request.ClaveIdempotencia.Length > 200)
            throw new ArgumentException("La clave idempotente es obligatoria y admite hasta 200 caracteres.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Payload) || request.Payload.Length > MaxPayloadLength)
            throw new ArgumentException("El payload es obligatorio y admite hasta 256 KiB.", nameof(request));
        if (request.Destinatario?.Length > 320)
            throw new ArgumentException("El destinatario admite hasta 320 caracteres.", nameof(request));
    }
}
