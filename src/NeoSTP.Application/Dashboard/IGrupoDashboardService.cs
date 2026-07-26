using NeoSTP.Application.Common;
using NeoSTP.Application.Dashboard.Dtos;

namespace NeoSTP.Application.Dashboard;

/// <summary>
/// Dashboard consolidado de grupo (E5). Agrega métricas de todas las empresas donde
/// el usuario puede operar (principal + membresías activas de E1). El alcance sale
/// de las membresías, así que es inherentemente seguro por tenant: nadie ve una
/// empresa a la que no pertenece.
/// </summary>
public interface IGrupoDashboardService
{
    /// <summary>
    /// Consolidado del período indicado (por defecto, el mes en curso).
    /// Devuelve una fila por empresa más los totales del grupo.
    /// </summary>
    Task<Result<GrupoDashboardDto>> GetAsync(int userId, int? anio = null, int? mes = null, CancellationToken ct = default);
}
