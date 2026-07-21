using NeoSTP.Application.Auth.Dtos;
using NeoSTP.Application.Common;

namespace NeoSTP.Application.Auth.Abstractions;

/// <summary>
/// CRUD de la configuración de SSO federado por empresa (E3). No maneja el flujo de
/// login (eso es <see cref="IAuthService.LoginExternoAsync"/>); solo el mapeo
/// dominio→empresa, el auto-aprovisionamiento y el rol por defecto.
/// </summary>
public interface ISsoConfigService
{
    Task<Result<EmpresaSsoDto>> GetAsync(int empresaId, CancellationToken ct = default);
    Task<Result<EmpresaSsoDto>> GuardarAsync(int empresaId, GuardarEmpresaSsoRequest request, string? actor, CancellationToken ct = default);
}
