using System.Net.Mail;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Notificaciones;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Notificaciones;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

public sealed class DteCorreoEntregaService : IDteCorreoEntregaService
{
    internal const string EntidadDte = "DTE";
    private readonly NeoStpDbContext _db;
    private readonly IAuditoriaService? _audit;

    public DteCorreoEntregaService(NeoStpDbContext db, IAuditoriaService? audit = null)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<Result<DteCorreoEstadoDto>> GetEstadoAsync(
        int empresaId,
        int dteDocumentoId,
        CancellationToken ct = default)
    {
        var contexto = await CargarContextoAsync(empresaId, dteDocumentoId, ct);
        if (contexto is null)
            return Result<DteCorreoEstadoDto>.Fail("Documento no encontrado.", "DTE_NOT_FOUND");

        return Result<DteCorreoEstadoDto>.Ok(
            await ConstruirEstadoAsync(contexto, ct));
    }

    public async Task<Result<DteCorreoEstadoDto>> ReencolarAsync(
        int empresaId,
        int dteDocumentoId,
        string finalidad,
        string idempotencyKey,
        string? actor,
        CancellationToken ct = default)
    {
        var contexto = await CargarContextoAsync(empresaId, dteDocumentoId, ct);
        if (contexto is null)
            return Result<DteCorreoEstadoDto>.Fail("Documento no encontrado.", "DTE_NOT_FOUND");
        if (contexto.EstadoCodigo != DteEstadoCodigos.Procesado || string.IsNullOrWhiteSpace(contexto.SelloRecibido))
            return Result<DteCorreoEstadoDto>.Fail(
                "Solo se puede reenviar el correo de un DTE procesado y sellado.", "INVALID_STATE");

        var destinos = NormalizarFinalidades(finalidad);
        if (destinos is null)
            return Result<DteCorreoEstadoDto>.Fail(
                "Finalidad inválida. Use RECEPTOR, EMISOR o AMBOS.", "VALIDATION");

        var claveCliente = NormalizarClaveCliente(idempotencyKey);
        if (claveCliente is null)
            return Result<DteCorreoEstadoDto>.Fail(
                "IdempotencyKey es obligatoria y debe tener entre 8 y 100 caracteres seguros.", "VALIDATION");

        foreach (var destino in destinos)
        {
            var correo = destino == DteCorreoFinalidades.Receptor
                ? contexto.ReceptorCorreo
                : contexto.EmisorCorreo;
            if (string.IsNullOrWhiteSpace(correo))
                return Result<DteCorreoEstadoDto>.Fail(
                    $"No hay correo configurado para {destino.ToLowerInvariant()}.", "EMAIL_DESTINATION_MISSING");
            if (!EsDestinoValido(correo))
                return Result<DteCorreoEstadoDto>.Fail(
                    $"El correo configurado para {destino.ToLowerInvariant()} no es válido.", "EMAIL_DESTINATION_INVALID");
        }

        var expectedKeys = destinos
            .Select(destino => $"DTE:{dteDocumentoId}:MANUAL:{destino}:{claveCliente}")
            .ToHashSet(StringComparer.Ordinal);
        for (var attempt = 0; ; attempt++)
        {
            foreach (var destino in destinos)
            {
                var correo = destino == DteCorreoFinalidades.Receptor
                    ? contexto.ReceptorCorreo
                    : contexto.EmisorCorreo;
                await StagePendingAsync(
                    _db,
                    empresaId,
                    dteDocumentoId,
                    destino,
                    correo!,
                    automatico: false,
                    $"DTE:{dteDocumentoId}:MANUAL:{destino}:{claveCliente}",
                    actor,
                    ct);
            }

            try
            {
                await _db.SaveChangesAsync(ct);
                break;
            }
            catch (DbUpdateException) when (attempt < 2)
            {
                var raced = _db.ChangeTracker.Entries<NotificationOutboxMessage>()
                    .Where(x => x.State == EntityState.Added
                        && x.Entity.EmpresaId == empresaId
                        && expectedKeys.Contains(x.Entity.ClaveIdempotencia))
                    .ToList();
                foreach (var entry in raced)
                    entry.State = EntityState.Detached;

                var existingKeys = await _db.NotificationOutbox.AsNoTracking()
                    .Where(x => x.EmpresaId == empresaId
                        && expectedKeys.Contains(x.ClaveIdempotencia))
                    .Select(x => x.ClaveIdempotencia)
                    .ToListAsync(ct);
                if (expectedKeys.All(existingKeys.Contains))
                    break;
            }
        }
        if (_audit is not null)
        {
            await _audit.RegistrarAsync(new AuditoriaEvent
            {
                EmpresaId = empresaId,
                Username = actor,
                Modulo = "DTE",
                Accion = "REENVIAR_CORREO",
                Entidad = "DteDocumento",
                EntidadId = dteDocumentoId.ToString(),
                Resultado = "OK",
                Detalle = $"Correo DTE encolado para {string.Join(',', destinos)}.",
            }, ct);
        }
        return Result<DteCorreoEstadoDto>.Ok(await ConstruirEstadoAsync(contexto, ct));
    }

    internal static async Task<bool> StagePendingAsync(
        NeoStpDbContext db,
        int empresaId,
        int dteDocumentoId,
        string finalidad,
        string destinatario,
        bool automatico,
        string claveIdempotencia,
        string? actor,
        CancellationToken ct)
    {
        var key = claveIdempotencia.Trim();
        if (db.NotificationOutbox.Local.Any(x =>
                x.EmpresaId == empresaId && x.ClaveIdempotencia == key)
            || await db.NotificationOutbox.AsNoTracking().AnyAsync(x =>
                x.EmpresaId == empresaId && x.ClaveIdempotencia == key, ct))
            return false;

        db.NotificationOutbox.Add(new NotificationOutboxMessage
        {
            EmpresaId = empresaId,
            Tipo = NotificationOutboxTipos.DteCorreo,
            Canal = NotificationOutboxCanales.Email,
            Destinatario = destinatario.Trim(),
            Payload = JsonSerializer.Serialize(new DteCorreoOutboxPayload(
                empresaId, dteDocumentoId, finalidad, automatico, Cortar(actor, 100))),
            ClaveIdempotencia = key,
            EntidadTipo = EntidadDte,
            EntidadId = dteDocumentoId,
            Finalidad = finalidad,
            Estado = NotificationOutboxEstados.Pending,
            MaxIntentos = 6,
            DisponibleDesde = DateTime.UtcNow,
            CreatedBy = Cortar(actor, 100),
        });
        return true;
    }

    private async Task<DteCorreoEstadoDto> ConstruirEstadoAsync(
        DteCorreoContexto contexto,
        CancellationToken ct)
    {
        var rows = await _db.NotificationOutbox.AsNoTracking()
            .Where(x => x.EmpresaId == contexto.EmpresaId
                && x.EntidadTipo == EntidadDte
                && x.EntidadId == contexto.DteDocumentoId
                && x.Tipo == NotificationOutboxTipos.DteCorreo)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync(ct);

        var historial = rows.Select(Map).ToList();
        var receptor = UltimaOEstadoBase(
            historial, DteCorreoFinalidades.Receptor, contexto.ReceptorCorreo);
        var emisor = UltimaOEstadoBase(
            historial, DteCorreoFinalidades.Emisor, contexto.EmisorCorreo);

        return new DteCorreoEstadoDto
        {
            DteDocumentoId = contexto.DteDocumentoId,
            Receptor = receptor,
            Emisor = emisor,
            EstadoGeneral = EstadoGeneral(receptor, emisor),
            Historial = historial,
        };
    }

    private async Task<DteCorreoContexto?> CargarContextoAsync(
        int empresaId,
        int dteDocumentoId,
        CancellationToken ct)
        => await _db.DteDocumentos.AsNoTracking()
            .Where(x => x.EmpresaId == empresaId && x.Id == dteDocumentoId)
            .Select(x => new DteCorreoContexto(
                x.EmpresaId,
                x.Id,
                x.EstadoCodigo,
                x.SelloRecibido,
                x.ReceptorCorreo,
                _db.Empresas.Where(e => e.Id == empresaId).Select(e => e.Correo).FirstOrDefault()))
            .FirstOrDefaultAsync(ct);

    private static DteCorreoEntregaDto UltimaOEstadoBase(
        IReadOnlyList<DteCorreoEntregaDto> historial,
        string finalidad,
        string? correo)
        => historial.FirstOrDefault(x => x.Finalidad == finalidad)
           ?? new DteCorreoEntregaDto
           {
               Finalidad = finalidad,
               Estado = !EsDestinoValido(correo)
                   ? DteCorreoEstados.NoAplica
                   : DteCorreoEstados.SinRegistro,
               Destinatario = Mascarar(correo),
               Reintentable = EsDestinoValido(correo),
           };

    private static DteCorreoEntregaDto Map(NotificationOutboxMessage row)
        => new()
        {
            OutboxId = row.Id,
            Finalidad = row.Finalidad ?? string.Empty,
            Estado = row.Estado switch
            {
                NotificationOutboxEstados.Pending => DteCorreoEstados.Pendiente,
                NotificationOutboxEstados.Processing => DteCorreoEstados.Enviando,
                NotificationOutboxEstados.Sent => DteCorreoEstados.Enviado,
                NotificationOutboxEstados.Failed => DteCorreoEstados.Reintentando,
                NotificationOutboxEstados.Dead => DteCorreoEstados.Fallido,
                _ => DteCorreoEstados.Fallido,
            },
            Destinatario = Mascarar(row.Destinatario),
            Automatico = row.ClaveIdempotencia.Contains(":AUTO:", StringComparison.Ordinal),
            Intentos = row.Intentos,
            MaxIntentos = row.MaxIntentos,
            ProximoIntentoAt = row.Estado == NotificationOutboxEstados.Failed
                ? row.DisponibleDesde : null,
            EnviadoAt = row.ProcesadoAt,
            Error = row.ErrorUltimo,
            MessageId = row.ProveedorMessageId,
            Reintentable = true,
        };

    private static string EstadoGeneral(params DteCorreoEntregaDto[] entregas)
    {
        var aplicables = entregas.Where(x => x.Estado != DteCorreoEstados.NoAplica).ToList();
        if (aplicables.Count == 0) return DteCorreoEstados.NoAplica;
        if (aplicables.Any(x => x.Estado == DteCorreoEstados.Fallido)) return DteCorreoEstados.Fallido;
        if (aplicables.Any(x => x.Estado == DteCorreoEstados.Reintentando)) return DteCorreoEstados.Reintentando;
        if (aplicables.Any(x => x.Estado == DteCorreoEstados.Enviando)) return DteCorreoEstados.Enviando;
        if (aplicables.Any(x => x.Estado == DteCorreoEstados.Pendiente)) return DteCorreoEstados.Pendiente;
        if (aplicables.Any(x => x.Estado == DteCorreoEstados.SinRegistro)) return DteCorreoEstados.SinRegistro;
        return DteCorreoEstados.Enviado;
    }

    private static IReadOnlyList<string>? NormalizarFinalidades(string? finalidad)
        => finalidad?.Trim().ToUpperInvariant() switch
        {
            DteCorreoFinalidades.Receptor => [DteCorreoFinalidades.Receptor],
            DteCorreoFinalidades.Emisor => [DteCorreoFinalidades.Emisor],
            DteCorreoFinalidades.Ambos => [DteCorreoFinalidades.Receptor, DteCorreoFinalidades.Emisor],
            _ => null,
        };

    private static string? NormalizarClaveCliente(string? value)
    {
        var key = value?.Trim();
        if (key is null || key.Length is < 8 or > 100)
            return null;
        return key.All(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.' or ':')
            ? key
            : null;
    }

    internal static bool EsDestinoValido(string? correo)
    {
        if (string.IsNullOrWhiteSpace(correo)) return false;
        var partes = correo.Split([',', ';'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return partes.Length > 0 && partes.All(x => MailAddress.TryCreate(x, out _));
    }

    private static string? Mascarar(string? correo)
    {
        if (string.IsNullOrWhiteSpace(correo)) return null;
        var first = correo.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        if (!MailAddress.TryCreate(first, out var parsed)) return "***";
        var local = parsed.User;
        var visible = local.Length <= 2 ? local[..1] : local[..2];
        return $"{visible}***@{parsed.Host}";
    }

    private static string? Cortar(string? value, int max)
        => string.IsNullOrWhiteSpace(value) ? null
            : value.Trim().Length <= max ? value.Trim() : value.Trim()[..max];

    private sealed record DteCorreoContexto(
        int EmpresaId,
        int DteDocumentoId,
        string EstadoCodigo,
        string? SelloRecibido,
        string? ReceptorCorreo,
        string? EmisorCorreo);
}
