using NeoSTP.Application.Common;

namespace NeoSTP.Application.Licenciamiento;

public enum RecursoLimitado
{
    Usuarios,
    Sucursales,
    PuntosVenta,
    DteMensual,
}

/// <summary>
/// Enforcement comercial de la licencia (Entrega 7):
/// - Límites del plan activo (usuarios, sucursales, puntos de venta, DTE por mes).
///   Empresa sin plan o límite null = ilimitado (no bloquear el alta inicial).
/// - Estado operativo de la empresa: solo ACTIVA opera; SUSPENDIDA/VENCIDA/etc. se bloquean.
/// </summary>
public interface ILicenciaGuardService
{
    /// <summary>Fail con código LIMITE_PLAN si el recurso alcanzó el límite del plan.</summary>
    Task<Result> ValidarLimiteAsync(int empresaId, RecursoLimitado recurso, CancellationToken ct = default);

    /// <summary>True si la empresa está ACTIVA. Cacheado ~60s (la suspensión tarda ≤1 min en aplicar).</summary>
    Task<bool> EmpresaOperativaAsync(int empresaId, CancellationToken ct = default);
}
