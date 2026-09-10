using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Auth;
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
    public Guid? SessionId => Guid.TryParse(Principal?.FindFirstValue(SessionClaims.Id), out var id) ? id : null;

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

    public string? TipoUsuarioCodigo
    {
        get
        {
            var tipo = Principal?.FindFirstValue(JwtTokenService.ClaimTipoUsuario);
            return tipo == "SUPERADMIN" && (Principal is null || !SessionClaims.IsPlatformAdministrator(Principal))
                ? "OPERADOR" : tipo;
        }
    }

    public IReadOnlyList<string> Roles => Principal?
        .FindAll(ClaimTypes.Role)
        .Select(c => c.Value)
        .ToList() ?? new List<string>();

    public IReadOnlyList<string> Permisos => Principal?
        .FindAll(JwtTokenService.ClaimPermiso)
        .Select(c => c.Value)
        .ToList() ?? new List<string>();

    public bool HasPermiso(string codigo)
        => Principal is not null && (SessionClaims.IsPlatformPermission(codigo)
            ? SessionClaims.IsPlatformAdministrator(Principal)
            : Principal.HasClaim(JwtTokenService.ClaimPermiso, codigo));

    public bool IsInRole(string codigo)
        => Principal is not null && (codigo == "SUPERADMIN"
            ? SessionClaims.IsPlatformAdministrator(Principal) : Principal.IsInRole(codigo));
}
