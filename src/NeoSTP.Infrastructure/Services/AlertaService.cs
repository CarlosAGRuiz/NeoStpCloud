using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NeoSTP.Application.Common;
using NeoSTP.Application.Notificaciones;
using NeoSTP.Application.Notificaciones.Dtos;
using NeoSTP.Domain.Core.Notificaciones;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

/// <summary>
/// Centro de alertas + dispositivos (FCM) + preferencias. Crear una alerta (upsert por Clave)
/// dispara push best-effort a los dispositivos activos del destinatario. Aislado por EmpresaId.
/// </summary>
public class AlertaService : IAlertaService
{
    private readonly NeoStpDbContext _db;
    private readonly IPushSender _push;
    private readonly ILogger<AlertaService> _logger;

    public AlertaService(NeoStpDbContext db, IPushSender push, ILogger<AlertaService> logger)
    {
        _db = db;
        _push = push;
        _logger = logger;
    }

    // ─── Alertas ────────────────────────────────────────────────────────────────

    public async Task<Result<PagedResult<AlertaDto>>> ListarAsync(int empresaId, int usuarioId, AlertaQuery query, CancellationToken ct = default)
    {
        var q = _db.Alertas.AsNoTracking()
            .Where(a => a.EmpresaId == empresaId && (a.UsuarioId == usuarioId || a.UsuarioId == null));

        if (!string.IsNullOrWhiteSpace(query.EstadoCodigo))
            q = q.Where(a => a.EstadoCodigo == query.EstadoCodigo);
        else
            q = q.Where(a => a.EstadoCodigo != AlertaEstados.Resuelta);

        if (!string.IsNullOrWhiteSpace(query.TipoCodigo))
            q = q.Where(a => a.TipoCodigo == query.TipoCodigo);

        var total = await q.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var items = await q
            .OrderByDescending(a => a.Severidad == AlertaSeveridades.Critica)
            .ThenByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => ToDto(a)).ToListAsync(ct);

        return Result<PagedResult<AlertaDto>>.Ok(PagedResult<AlertaDto>.Create(items, total, page, pageSize));
    }

    public async Task<AlertaResumenDto> ResumenAsync(int empresaId, int usuarioId, CancellationToken ct = default)
    {
        var pend = await _db.Alertas.AsNoTracking()
            .Where(a => a.EmpresaId == empresaId && (a.UsuarioId == usuarioId || a.UsuarioId == null)
                     && a.EstadoCodigo == AlertaEstados.Pendiente)
            .Select(a => a.Severidad).ToListAsync(ct);

        return new AlertaResumenDto
        {
            Pendientes = pend.Count,
            Criticas = pend.Count(s => s == AlertaSeveridades.Critica),
            Advertencias = pend.Count(s => s == AlertaSeveridades.Advertencia),
        };
    }

    public Task<Result> MarcarLeidaAsync(int empresaId, int usuarioId, int alertaId, CancellationToken ct = default)
        => CambiarEstadoAsync(empresaId, usuarioId, alertaId, AlertaEstados.Leida, ct);

    public Task<Result> ResolverAsync(int empresaId, int usuarioId, int alertaId, CancellationToken ct = default)
        => CambiarEstadoAsync(empresaId, usuarioId, alertaId, AlertaEstados.Resuelta, ct);

    private async Task<Result> CambiarEstadoAsync(int empresaId, int usuarioId, int alertaId, string estado, CancellationToken ct)
    {
        var a = await _db.Alertas.FirstOrDefaultAsync(x => x.Id == alertaId && x.EmpresaId == empresaId
            && (x.UsuarioId == usuarioId || x.UsuarioId == null), ct);
        if (a is null) return Result.Fail("Alerta no encontrada.", "ALERTA_NOT_FOUND");
        a.EstadoCodigo = estado;
        if (estado == AlertaEstados.Leida) a.LeidaAt = DateTime.UtcNow;
        if (estado == AlertaEstados.Resuelta) a.ResueltaAt = DateTime.UtcNow;
        a.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> MarcarTodasLeidasAsync(int empresaId, int usuarioId, CancellationToken ct = default)
    {
        var pendientes = await _db.Alertas
            .Where(a => a.EmpresaId == empresaId && (a.UsuarioId == usuarioId || a.UsuarioId == null)
                     && a.EstadoCodigo == AlertaEstados.Pendiente)
            .ToListAsync(ct);
        foreach (var a in pendientes) { a.EstadoCodigo = AlertaEstados.Leida; a.LeidaAt = DateTime.UtcNow; }
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result<AlertaDto>> CrearAsync(CrearAlertaRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.TipoCodigo) || string.IsNullOrWhiteSpace(request.Titulo))
            return Result<AlertaDto>.Fail("Tipo y título son obligatorios.", "VALIDATION");

        var clave = string.IsNullOrWhiteSpace(request.Clave)
            ? $"{request.TipoCodigo}:{request.EntidadId?.ToString() ?? "-"}"
            : request.Clave.Trim();

        // Dedupe: si existe una alerta NO resuelta con la misma clave, devolverla sin duplicar ni re-notificar.
        var existente = await _db.Alertas
            .FirstOrDefaultAsync(a => a.EmpresaId == request.EmpresaId && a.Clave == clave
                && a.EstadoCodigo != AlertaEstados.Resuelta, ct);
        if (existente is not null)
            return Result<AlertaDto>.Ok(ToDto(existente));

        var alerta = new Alerta
        {
            EmpresaId = request.EmpresaId,
            UsuarioId = request.UsuarioId,
            TipoCodigo = request.TipoCodigo,
            Severidad = request.Severidad,
            Titulo = request.Titulo,
            Mensaje = request.Mensaje,
            EntidadTipo = request.EntidadTipo,
            EntidadId = request.EntidadId,
            EstadoCodigo = AlertaEstados.Pendiente,
            Clave = clave,
        };
        _db.Alertas.Add(alerta);
        await _db.SaveChangesAsync(ct);

        await EnviarPushAsync(alerta, ct);
        return Result<AlertaDto>.Ok(ToDto(alerta));
    }

    private async Task EnviarPushAsync(Alerta alerta, CancellationToken ct)
    {
        try
        {
            var tokensQuery = _db.DispositivosNotificacion.AsNoTracking()
                .Where(d => d.EmpresaId == alerta.EmpresaId && d.Activo);
            if (alerta.UsuarioId is int uid)
                tokensQuery = tokensQuery.Where(d => d.UsuarioId == uid);

            var tokens = await tokensQuery.Select(d => d.Token).ToListAsync(ct);
            if (tokens.Count == 0) return;

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
                },
            }, ct);

            await DesactivarTokensInvalidosAsync(result.InvalidTokens, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AlertaService: error enviando push para alerta {Id}", alerta.Id);
        }
    }

    /// <summary>Desactiva los tokens que el proveedor reportó como inválidos/no registrados.</summary>
    private async Task DesactivarTokensInvalidosAsync(IReadOnlyList<string> invalidos, CancellationToken ct)
    {
        if (invalidos is null || invalidos.Count == 0) return;
        var dispositivos = await _db.DispositivosNotificacion
            .Where(d => invalidos.Contains(d.Token) && d.Activo)
            .ToListAsync(ct);
        if (dispositivos.Count == 0) return;
        foreach (var d in dispositivos)
        {
            d.Activo = false;
            d.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("AlertaService: {N} token(s) push desactivados por inválidos.", dispositivos.Count);
    }

    // ─── Dispositivos ───────────────────────────────────────────────────────────

    public async Task<Result> RegistrarDispositivoAsync(int empresaId, int usuarioId, RegistrarDispositivoRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return Result.Fail("El token es obligatorio.", "VALIDATION");
        var token = request.Token.Trim();

        var existente = await _db.DispositivosNotificacion.FirstOrDefaultAsync(d => d.Token == token, ct);
        if (existente is not null)
        {
            existente.EmpresaId = empresaId;
            existente.UsuarioId = usuarioId;
            existente.Plataforma = NormPlataforma(request.Plataforma);
            existente.Activo = true;
            existente.UltimoUsoAt = DateTime.UtcNow;
            existente.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _db.DispositivosNotificacion.Add(new DispositivoNotificacion
            {
                EmpresaId = empresaId, UsuarioId = usuarioId, Token = token,
                Plataforma = NormPlataforma(request.Plataforma), Activo = true, UltimoUsoAt = DateTime.UtcNow,
            });
        }
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> EliminarDispositivoAsync(int empresaId, int usuarioId, string token, CancellationToken ct = default)
    {
        var d = await _db.DispositivosNotificacion.FirstOrDefaultAsync(x => x.Token == token && x.EmpresaId == empresaId, ct);
        if (d is null) return Result.Fail("Dispositivo no encontrado.", "DISPOSITIVO_NOT_FOUND");
        d.Activo = false;
        d.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    // ─── Preferencias ───────────────────────────────────────────────────────────

    public async Task<PreferenciaNotificacionDto> GetPreferenciasAsync(int empresaId, int usuarioId, CancellationToken ct = default)
    {
        var p = await _db.PreferenciasNotificacion.AsNoTracking()
            .FirstOrDefaultAsync(x => x.EmpresaId == empresaId && x.UsuarioId == usuarioId, ct);
        return p is null
            ? new PreferenciaNotificacionDto()
            : new PreferenciaNotificacionDto { Canal = p.Canal, NoMolestar = p.NoMolestar, HoraInicio = p.HoraInicio, HoraFin = p.HoraFin };
    }

    public async Task<Result> GuardarPreferenciasAsync(int empresaId, int usuarioId, PreferenciaNotificacionDto request, CancellationToken ct = default)
    {
        var p = await _db.PreferenciasNotificacion.FirstOrDefaultAsync(x => x.EmpresaId == empresaId && x.UsuarioId == usuarioId, ct);
        if (p is null)
        {
            p = new PreferenciaNotificacion { EmpresaId = empresaId, UsuarioId = usuarioId };
            _db.PreferenciasNotificacion.Add(p);
        }
        p.Canal = string.IsNullOrWhiteSpace(request.Canal) ? NotifCanales.Push : request.Canal.Trim().ToUpperInvariant();
        p.NoMolestar = request.NoMolestar;
        p.HoraInicio = request.HoraInicio;
        p.HoraFin = request.HoraFin;
        p.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    private static string NormPlataforma(string? p)
        => string.IsNullOrWhiteSpace(p) ? "ANDROID" : p.Trim().ToUpperInvariant();

    private static AlertaDto ToDto(Alerta a) => new()
    {
        Id = a.Id, TipoCodigo = a.TipoCodigo, Severidad = a.Severidad, Titulo = a.Titulo, Mensaje = a.Mensaje,
        EntidadTipo = a.EntidadTipo, EntidadId = a.EntidadId, EstadoCodigo = a.EstadoCodigo,
        CreatedAt = a.CreatedAt, ResueltaAt = a.ResueltaAt,
    };
}
