using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Empresas;

namespace NeoSTP.Web.Auth;

/// <summary>
/// Resuelve el EmpresaId efectivo del request actual.
///   - Para usuario de empresa: ICurrentUser.EmpresaId
///   - Para SuperAdmin: lee cookie "neostp-soporte-empresa" (id + nombre)
/// </summary>
public class WebEmpresaContext : IEmpresaContext
{
    public const string CookieId = "neostp.soporte.empresaId";
    public const string CookieNombre = "neostp.soporte.empresaNombre";

    private readonly IHttpContextAccessor _accessor;
    private readonly ICurrentUser _currentUser;

    public WebEmpresaContext(IHttpContextAccessor accessor, ICurrentUser currentUser)
    {
        _accessor = accessor;
        _currentUser = currentUser;
    }

    public int? CurrentEmpresaId
    {
        get
        {
            if (_currentUser.EmpresaId is int empresaId) return empresaId;
            if (_currentUser.TipoUsuarioCodigo != "SUPERADMIN") return null;
            var ctx = _accessor.HttpContext;
            if (ctx is null) return null;
            return int.TryParse(ctx.Request.Cookies[CookieId], out var id) ? id : null;
        }
    }

    public bool IsSupportMode
        => _currentUser.TipoUsuarioCodigo == "SUPERADMIN" && _currentUser.EmpresaId is null && CurrentEmpresaId is not null;

    public string? SupportEmpresaNombre
        => _accessor.HttpContext?.Request.Cookies[CookieNombre];

    public void SetSupportEmpresa(int empresaId, string empresaNombre)
    {
        var ctx = _accessor.HttpContext ?? throw new InvalidOperationException("No HttpContext.");
        var opts = new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = ctx.Request.IsHttps,
            Expires = DateTimeOffset.UtcNow.AddDays(7),
        };
        ctx.Response.Cookies.Append(CookieId, empresaId.ToString(), opts);
        ctx.Response.Cookies.Append(CookieNombre, empresaNombre, opts);
    }

    public void ClearSupportEmpresa()
    {
        var ctx = _accessor.HttpContext;
        if (ctx is null) return;
        ctx.Response.Cookies.Delete(CookieId);
        ctx.Response.Cookies.Delete(CookieNombre);
    }
}
