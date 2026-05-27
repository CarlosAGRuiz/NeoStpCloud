namespace NeoSTP.Application.Empresas;

/// <summary>
/// Contexto de empresa "actual" para el usuario en sesión.
///   - Si es usuario de empresa: siempre devuelve su EmpresaId
///   - Si es SuperAdmin: devuelve la empresa que tenga seleccionada en modo soporte
///     (cookie/session); null si no ha seleccionado ninguna
/// Los controllers per-tenant (Clientes, Productos, DteConfiguracion, etc.)
/// deben preguntar SIEMPRE por CurrentEmpresaId aquí en vez de ICurrentUser.EmpresaId.
/// </summary>
public interface IEmpresaContext
{
    int? CurrentEmpresaId { get; }
    bool IsSupportMode { get; }
    string? SupportEmpresaNombre { get; }
    void SetSupportEmpresa(int empresaId, string empresaNombre);
    void ClearSupportEmpresa();
}
