using NeoSTP.Application.Auth.Dtos;
using NeoSTP.Application.Common;

namespace NeoSTP.Application.Auth.Abstractions;

public class AuthContext
{
    // Derived by the host from authenticated claims, never from request JSON.
    public Guid? SessionId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? TraceId { get; set; }
}

public interface IAuthService
{
    Task<Result<LoginResponse>> LoginAsync(LoginRequest request, AuthContext context, CancellationToken ct = default);
    Task<Result<LoginResponse>> RefreshAsync(string refreshToken, AuthContext context, CancellationToken ct = default);
    Task<Result> LogoutAsync(string? refreshToken, AuthContext context, CancellationToken ct = default);
    Task<Result<UserInfo>> GetCurrentUserInfoAsync(int userId, CancellationToken ct = default);

    /// <summary>Empresas donde el usuario puede operar: la principal + membresías activas (E1).</summary>
    Task<Result<IReadOnlyList<Dtos.EmpresaDisponibleDto>>> ListarEmpresasDisponiblesAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// Cambia la empresa activa del usuario: valida la membresía y emite credenciales
    /// nuevas (token/claims) con los permisos del rol de esa empresa.
    /// </summary>
    Task<Result<LoginResponse>> CambiarEmpresaAsync(int userId, int empresaId, AuthContext context, CancellationToken ct = default);

    /// <summary>
    /// Inicia sesión con una identidad federada (SSO OIDC, E3). Vincula por sujeto
    /// estable + issuer validado, o auto-aprovisiona según la
    /// configuración SSO de la empresa dueña del dominio. No usa contraseña.
    /// </summary>
    Task<Result<LoginResponse>> LoginExternoAsync(ExternalLoginInfo info, AuthContext context, CancellationToken ct = default);

    /// <summary>Vinculación explícita: identidad del cookie OIDC protegido + contraseña/MFA local.</summary>
    Task<Result<LoginResponse>> VincularExternoAsync(ExternalLoginInfo info, LoginRequest proof, AuthContext context, CancellationToken ct = default);

    Task<Result<LoginResponse>> VerifyMfaChallengeAsync(string code, AuthContext context, CancellationToken ct = default);
}
