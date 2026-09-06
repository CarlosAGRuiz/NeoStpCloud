using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Common;
using NeoSTP.Application.Licenciamiento;
using NeoSTP.Infrastructure.Billing;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

public class LicenciaGuardService : ILicenciaGuardService
{
    // Caché de estado de empresa (por proceso). 60s de staleness es aceptable para
    // que una suspensión aplique; evita un query por request en el middleware.
    private static readonly ConcurrentDictionary<int, (bool Operativa, DateTime Expira)> EstadoCache = new();
    private static readonly TimeSpan EstadoTtl = TimeSpan.FromSeconds(60);

    private readonly NeoStpDbContext _db;

    public LicenciaGuardService(NeoStpDbContext db)
    {
        _db = db;
    }

    public async Task<Result> ValidarLimiteAsync(int empresaId, RecursoLimitado recurso, CancellationToken ct = default)
    {
        var ahora = DateTime.UtcNow;
        var licencias = await _db.EmpresaPlanes.AsNoTracking()
            .Where(ep => ep.EmpresaId == empresaId && ep.EstadoCodigo == "ACTIVO"
                      && ep.FechaInicio <= ahora
                      && (ep.FechaFin == null || ep.FechaFin > ahora))
            .Take(2).ToListAsync(ct);
        if (licencias.Count > 1)
            return Result.Fail("La empresa tiene más de una licencia vigente; requiere revisión.", "LICENSE_INVALID");
        if (licencias.Count == 0 && await _db.BillingPaymentApplications.AnyAsync(x => x.CheckoutIntent.EmpresaId == empresaId, ct))
            return Result.Fail("La empresa requiere una licencia pagada vigente.", "LICENSE_INVALID");
        if (licencias.Count == 0)
        {
            // El alta inicial puede crear recursos administrativos antes de asignar el plan,
            // pero nunca debe poder emitir facturas sin una suscripción vigente.
            return recurso == RecursoLimitado.DteMensual
                ? Result.Fail("La empresa no tiene un plan vigente para emitir DTE.", "LICENSE_INVALID")
                : Result.Ok();
        }

        var entitlement = await BillingEntitlementReader.ReadAsync(_db, licencias[0], ct);
        if (entitlement.IsFailure) return Result.Fail(entitlement.Error!, entitlement.ErrorCode);
        var plan = entitlement.Value!;

        var (limite, nombre) = recurso switch
        {
            RecursoLimitado.Usuarios => (plan.LimiteUsuarios, "usuarios"),
            RecursoLimitado.Sucursales => (plan.LimiteSucursales, "sucursales"),
            RecursoLimitado.PuntosVenta => (plan.LimitePuntosVenta, "puntos de venta"),
            RecursoLimitado.DteMensual => (plan.LimiteDteMensual, "documentos por mes"),
            _ => (null, "recurso"),
        };
        if (limite is not int max || max <= 0) return Result.Ok(); // null/0 = ilimitado

        var usados = recurso switch
        {
            RecursoLimitado.Usuarios => await _db.Usuarios.CountAsync(u => u.EmpresaId == empresaId, ct),
            RecursoLimitado.Sucursales => await _db.Sucursales.CountAsync(s => s.EmpresaId == empresaId, ct),
            RecursoLimitado.PuntosVenta => await _db.PuntosVenta.CountAsync(p => p.Sucursal.EmpresaId == empresaId, ct),
            RecursoLimitado.DteMensual => await ContarDtesDelMesAsync(empresaId, ct),
            _ => 0,
        };

        if (usados >= max)
        {
            // Mismo código que ya usan sucursales/puntos de venta (SucursalesService).
            return Result.Fail(
                $"Tu plan {plan.PlanName} permite {max} {nombre} y ya usas {usados}. " +
                "Mejora tu plan para continuar.", "LIMIT_EXCEEDED");
        }
        return Result.Ok();
    }

    private Task<int> ContarDtesDelMesAsync(int empresaId, CancellationToken ct)
    {
        var hoy = DateTime.UtcNow;
        var inicioMes = new DateTime(hoy.Year, hoy.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return NeoSTP.Infrastructure.Dte.Certificacion.CertificationCampaignAccess.CountCommercialDocumentsAsync(_db, empresaId, inicioMes, ct);
    }

    public async Task<bool> EmpresaOperativaAsync(int empresaId, CancellationToken ct = default)
    {
        if (EstadoCache.TryGetValue(empresaId, out var cached) && cached.Expira > DateTime.UtcNow)
            return cached.Operativa;

        var estado = await _db.Empresas.AsNoTracking()
            .Where(e => e.Id == empresaId)
            .Select(e => e.EstadoCodigo)
            .FirstOrDefaultAsync(ct);
        var operativa = estado == NeoSTP.Domain.Common.EmpresaEstados.Activa;
        EstadoCache[empresaId] = (operativa, DateTime.UtcNow.Add(EstadoTtl));
        return operativa;
    }

    /// <summary>Invalida el caché de estado (para tests y cambios de estado inmediatos).</summary>
    public static void InvalidarEstadoCache(int? empresaId = null)
    {
        if (empresaId is int id) EstadoCache.TryRemove(id, out _);
        else EstadoCache.Clear();
    }
}
