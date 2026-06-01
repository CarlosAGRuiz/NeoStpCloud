using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Ops;
using NeoSTP.Domain.Core.Ops;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

/// <summary>
/// Implementación de cuotas por ventana deslizante apoyada en <c>Core_ApiQuotas</c>
/// (reglas) y <c>Core_ApiUsageLog</c> (conteo). Ver <see cref="IApiQuotaService"/>.
/// </summary>
public class ApiQuotaService : IApiQuotaService
{
    private const string AuditModule = "HARDENING";

    private readonly NeoStpDbContext _db;
    private readonly IAuditoriaService _auditoria;
    private readonly ILogger<ApiQuotaService> _logger;

    public ApiQuotaService(NeoStpDbContext db, IAuditoriaService auditoria, ILogger<ApiQuotaService> logger)
    {
        _db = db;
        _auditoria = auditoria;
        _logger = logger;
    }

    public async Task<QuotaDecision> EvaluarAsync(QuotaContext ctx, CancellationToken ct = default)
    {
        // SuperAdmin opera en modo soporte sobre múltiples empresas: exento de cuotas.
        if (ctx.IsSuperAdmin)
            return QuotaDecision.Permitir();

        // Reglas de la empresa + reglas de sistema (EmpresaId null).
        var quotas = await _db.ApiQuotas.AsNoTracking()
            .Where(q => q.Activo && (q.EmpresaId == ctx.EmpresaId || q.EmpresaId == null))
            .ToListAsync(ct);

        if (quotas.Count == 0)
            return QuotaDecision.Permitir();

        // Resolver plan activo solo si hay cuotas por plan.
        int? planId = null;
        if (quotas.Any(q => q.Ambito == ApiQuotaAmbito.Plan) && ctx.EmpresaId is int empPlan)
        {
            planId = await _db.EmpresaPlanes.AsNoTracking()
                .Where(p => p.EmpresaId == empPlan && p.EstadoCodigo == "ACTIVO")
                .Select(p => (int?)p.PlanId)
                .FirstOrDefaultAsync(ct);
        }

        var now = DateTime.UtcNow;
        int? minRemaining = null;
        int? minLimit = null;

        foreach (var q in quotas)
        {
            if (!Aplica(q, ctx, planId))
                continue;

            var desde = now.AddSeconds(-q.VentanaSegundos);
            var count = await ContarUsoAsync(q, ctx, desde, ct);
            var remaining = q.LimitePeticiones - count;

            if (count >= q.LimitePeticiones)
            {
                _logger.LogWarning(
                    "Rate limit excedido. Ambito={Ambito} Ref={Ref} Empresa={Empresa} Usuario={Usuario} count={Count}/{Limit}",
                    q.Ambito, q.AmbitoRef, ctx.EmpresaId, ctx.UsuarioId, count, q.LimitePeticiones);
                return QuotaDecision.Rechazar(q.Ambito, q.LimitePeticiones, q.VentanaSegundos);
            }

            if (minRemaining is null || remaining < minRemaining)
            {
                minRemaining = remaining;
                minLimit = q.LimitePeticiones;
            }
        }

        return QuotaDecision.Permitir(minLimit, minRemaining);
    }

    public async Task RegistrarUsoAsync(ApiUsageEntry entry, CancellationToken ct = default)
    {
        try
        {
            _db.ApiUsageLogs.Add(new ApiUsageLog
            {
                EmpresaId = entry.EmpresaId,
                UsuarioId = entry.UsuarioId,
                ApiKeyId = entry.ApiKeyId,
                Metodo = entry.Metodo,
                Ruta = entry.Ruta.Length > 500 ? entry.Ruta[..500] : entry.Ruta,
                Modulo = entry.Modulo,
                StatusCode = entry.StatusCode,
                DuracionMs = entry.DuracionMs,
                IpOrigen = entry.IpOrigen,
                OcurrioAt = DateTime.UtcNow,
            });
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Best-effort: el registro de uso nunca debe romper la petición.
            _logger.LogError(ex, "No se pudo registrar el uso de API (best-effort).");
        }
    }

    // -- Administración ---------------------------------------------------

    public async Task<IReadOnlyList<ApiQuotaDto>> ListarAsync(CancellationToken ct = default)
        => await _db.ApiQuotas.AsNoTracking()
            .OrderByDescending(q => q.Id)
            .Select(q => new ApiQuotaDto
            {
                Id = q.Id, EmpresaId = q.EmpresaId, Ambito = q.Ambito, AmbitoRef = q.AmbitoRef,
                VentanaSegundos = q.VentanaSegundos, LimitePeticiones = q.LimitePeticiones,
                Activo = q.Activo, Descripcion = q.Descripcion,
            })
            .ToListAsync(ct);

    public async Task<Result<ApiQuotaDto>> CrearAsync(CrearApiQuotaRequest request, string? actor, CancellationToken ct = default)
    {
        var ambitos = new[] { ApiQuotaAmbito.Global, ApiQuotaAmbito.Empresa, ApiQuotaAmbito.Plan,
                              ApiQuotaAmbito.Usuario, ApiQuotaAmbito.ApiKey, ApiQuotaAmbito.Modulo };
        if (!ambitos.Contains(request.Ambito))
            return Result<ApiQuotaDto>.Fail("Ámbito inválido.", "VALIDATION");
        if (request.LimitePeticiones <= 0 || request.VentanaSegundos <= 0)
            return Result<ApiQuotaDto>.Fail("Límite y ventana deben ser positivos.", "VALIDATION");

        var q = new ApiQuota
        {
            EmpresaId = request.EmpresaId,
            Ambito = request.Ambito,
            AmbitoRef = request.AmbitoRef,
            VentanaSegundos = request.VentanaSegundos,
            LimitePeticiones = request.LimitePeticiones,
            Descripcion = request.Descripcion,
            Activo = true,
            CreatedBy = actor,
        };
        _db.ApiQuotas.Add(q);
        await _db.SaveChangesAsync(ct);
        await Audit(actor, "QUOTA_CREATE", q.Id.ToString(), $"{q.Ambito}:{q.AmbitoRef} {q.LimitePeticiones}/{q.VentanaSegundos}s");

        return Result<ApiQuotaDto>.Ok(new ApiQuotaDto
        {
            Id = q.Id, EmpresaId = q.EmpresaId, Ambito = q.Ambito, AmbitoRef = q.AmbitoRef,
            VentanaSegundos = q.VentanaSegundos, LimitePeticiones = q.LimitePeticiones,
            Activo = q.Activo, Descripcion = q.Descripcion,
        });
    }

    public async Task<Result> EliminarAsync(int id, string? actor, CancellationToken ct = default)
    {
        var q = await _db.ApiQuotas.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (q is null)
            return Result.Fail("Cuota no encontrada.", "QUOTA_NOT_FOUND");

        _db.ApiQuotas.Remove(q);
        await _db.SaveChangesAsync(ct);
        await Audit(actor, "QUOTA_DELETE", id.ToString(), $"{q.Ambito}:{q.AmbitoRef}");
        return Result.Ok();
    }

    private Task Audit(string? actor, string accion, string entidadId, string detalle)
        => _auditoria.RegistrarAsync(new AuditoriaEvent
        {
            Username = actor,
            Modulo = AuditModule,
            Accion = accion,
            Entidad = "ApiQuota",
            EntidadId = entidadId,
            Resultado = "OK",
            Detalle = detalle,
        });

    private static bool Aplica(ApiQuota q, QuotaContext ctx, int? planId) => q.Ambito switch
    {
        ApiQuotaAmbito.Global => true,
        ApiQuotaAmbito.Empresa => ctx.EmpresaId is not null,
        ApiQuotaAmbito.Usuario => ctx.UsuarioId is not null
            && (q.AmbitoRef is null || q.AmbitoRef == ctx.UsuarioId.ToString()),
        ApiQuotaAmbito.ApiKey => ctx.ApiKeyId is not null
            && (q.AmbitoRef is null || q.AmbitoRef == ctx.ApiKeyId.ToString()),
        ApiQuotaAmbito.Modulo => ctx.Modulo is not null
            && (q.AmbitoRef is null || string.Equals(q.AmbitoRef, ctx.Modulo, StringComparison.OrdinalIgnoreCase)),
        ApiQuotaAmbito.Plan => planId is not null
            && (q.AmbitoRef is null || q.AmbitoRef == planId.ToString()),
        _ => false,
    };

    private Task<int> ContarUsoAsync(ApiQuota q, QuotaContext ctx, DateTime desde, CancellationToken ct)
    {
        var query = _db.ApiUsageLogs.AsNoTracking().Where(l => l.OcurrioAt >= desde);

        query = q.Ambito switch
        {
            ApiQuotaAmbito.Global => q.EmpresaId is null ? query : query.Where(l => l.EmpresaId == q.EmpresaId),
            ApiQuotaAmbito.Empresa => query.Where(l => l.EmpresaId == ctx.EmpresaId),
            ApiQuotaAmbito.Usuario => query.Where(l => l.UsuarioId == ctx.UsuarioId),
            ApiQuotaAmbito.ApiKey => query.Where(l => l.ApiKeyId == ctx.ApiKeyId),
            ApiQuotaAmbito.Modulo => ctx.EmpresaId is null
                ? query.Where(l => l.Modulo == ctx.Modulo)
                : query.Where(l => l.Modulo == ctx.Modulo && l.EmpresaId == ctx.EmpresaId),
            ApiQuotaAmbito.Plan => query.Where(l => l.EmpresaId == ctx.EmpresaId),
            _ => query,
        };

        return query.CountAsync(ct);
    }
}
