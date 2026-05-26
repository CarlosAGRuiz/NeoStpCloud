using System.Security.Claims;
using NeoSTP.Application.Auth.Abstractions;

namespace NeoSTP.Web.Auth;

public class CookieCurrentUser : ICurrentUser
{
    public const string ClaimEmpresaId = "empresa_id";
    public const string ClaimTipoUsuario = "tipo_usuario";
    public const string ClaimPermiso = "permiso";

    private readonly IHttpContextAccessor _accessor;

    public CookieCurrentUser(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public int? UserId
    {
        get
        {
            var raw = Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(raw, out var id) ? id : null;
        }
    }

    public int? EmpresaId
    {
        get
        {
            var raw = Principal?.FindFirstValue(ClaimEmpresaId);
            return int.TryParse(raw, out var id) ? id : null;
        }
    }

    public string? Username => Principal?.FindFirstValue(ClaimTypes.Name);
    public string? Email => Principal?.FindFirstValue(ClaimTypes.Email);
    public string? TipoUsuarioCodigo => Principal?.FindFirstValue(ClaimTipoUsuario);

    public IReadOnlyList<string> Roles => Principal?.FindAll(ClaimTypes.Role)
        .Select(c => c.Value).ToList() ?? new List<string>();

    public IReadOnlyList<string> Permisos => Principal?.FindAll(ClaimPermiso)
        .Select(c => c.Value).ToList() ?? new List<string>();

    public bool HasPermiso(string codigo) => Principal?.HasClaim(ClaimPermiso, codigo) ?? false;
    public bool IsInRole(string codigo) => Principal?.IsInRole(codigo) ?? false;
}
