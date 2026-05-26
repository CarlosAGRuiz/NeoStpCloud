using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Infrastructure.Auth;

namespace NeoSTP.Api.Auth;

public class CurrentUserAccessor : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUserAccessor(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public int? UserId
    {
        get
        {
            var raw = Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(raw, out var id) ? id : null;
        }
    }

    public int? EmpresaId
    {
        get
        {
            var raw = Principal?.FindFirstValue(JwtTokenService.ClaimEmpresaId);
            return int.TryParse(raw, out var id) ? id : null;
        }
    }

    public string? Username => Principal?.FindFirstValue(ClaimTypes.Name)
        ?? Principal?.FindFirstValue(JwtRegisteredClaimNames.UniqueName);

    public string? Email => Principal?.FindFirstValue(JwtRegisteredClaimNames.Email)
        ?? Principal?.FindFirstValue(ClaimTypes.Email);

    public string? TipoUsuarioCodigo => Principal?.FindFirstValue(JwtTokenService.ClaimTipoUsuario);

    public IReadOnlyList<string> Roles => Principal?
        .FindAll(ClaimTypes.Role)
        .Select(c => c.Value)
        .ToList() ?? new List<string>();

    public IReadOnlyList<string> Permisos => Principal?
        .FindAll(JwtTokenService.ClaimPermiso)
        .Select(c => c.Value)
        .ToList() ?? new List<string>();

    public bool HasPermiso(string codigo)
        => Principal?.HasClaim(JwtTokenService.ClaimPermiso, codigo) ?? false;

    public bool IsInRole(string codigo)
        => Principal?.IsInRole(codigo) ?? false;
}
