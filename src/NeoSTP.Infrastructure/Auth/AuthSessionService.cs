using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Auth.Dtos;
using NeoSTP.Application.Common;
using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Seguridad;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Auth;

public sealed class AuthSessionService(NeoStpDbContext db) : IAuthSessionService
{
    internal static AuthSession Create(Usuario user, UserInfo info, string purpose, DateTime expires)
    {
        var session = new AuthSession
        {
            UsuarioId = user.Id, EmpresaId = info.EmpresaId, Purpose = purpose,
            CredentialFingerprint = Credentials(user, purpose),
            AuthorizationFingerprint = Authorization(info),
            ExpiresAt = expires
        };
        Stamp(info, session);
        return session;
    }

    internal async Task<Result<UserInfo>> ValidateAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await db.AuthSessions.AsNoTracking()
            .Include(s => s.Usuario).ThenInclude(u => u.Roles).ThenInclude(r => r.Rol)
                .ThenInclude(r => r.Permisos).ThenInclude(p => p.Permiso)
            .SingleOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null || session.RevokedAt is not null || session.ExpiresAt <= DateTime.UtcNow)
            return Invalid();
        var user = session.Usuario;
        if (user.EstadoCodigo != EstadoCodes.Activo || user.BloqueadoHasta > DateTime.UtcNow
            || session.CredentialFingerprint != Credentials(user, session.Purpose))
            return Invalid();
        if (session.Purpose switch
            {
                SessionClaims.Full => RbacSecurity.IsPlatformUser(user) && !user.MfaHabilitado,
                SessionClaims.MfaEnroll => user.MfaHabilitado || !RbacSecurity.IsPlatformUser(user),
                SessionClaims.MfaVerify => !user.MfaHabilitado,
                _ => true
            })
            return Invalid();

        var resolved = await SessionUserInfoFactory.ResolveAsync(db, user, session.EmpresaId, ct);
        if (resolved.IsFailure) return resolved;
        if (session.AuthorizationFingerprint != Authorization(resolved.Value!))
            return Invalid();
        Stamp(resolved.Value!, session);
        return resolved;
    }

    public async Task<Result<UserInfo>> ValidatePrincipalAsync(ClaimsPrincipal principal, CancellationToken ct = default)
    {
        if (principal.Identity?.IsAuthenticated != true
            || !Guid.TryParse(principal.FindFirst(SessionClaims.Id)?.Value, out var id))
            return Invalid();
        var result = await ValidateAsync(id, ct);
        if (result.IsFailure) return result;
        var info = result.Value!;
        if (principal.FindFirst(ClaimTypes.NameIdentifier)?.Value != info.Id.ToString(CultureInfo.InvariantCulture)
            || principal.FindFirst(SessionClaims.Purpose)?.Value != info.SessionPurpose
            || principal.FindFirst("empresa_id")?.Value != info.EmpresaId?.ToString(CultureInfo.InvariantCulture)
            || principal.FindFirst("tipo_usuario")?.Value != info.TipoUsuarioCodigo
            || !SameClaims(principal, ClaimTypes.Role, info.Roles)
            || !SameClaims(principal, "permiso", info.Permisos))
            return Invalid();
        return result;
    }

    private static bool SameClaims(ClaimsPrincipal user, string claim, IReadOnlyList<string> expected) =>
        user.FindAll(claim).Select(c => c.Value).ToHashSet(StringComparer.Ordinal).SetEquals(expected);

    private static void Stamp(UserInfo info, AuthSession session)
    {
        info.SessionId = session.Id;
        info.SessionExpiresAt = session.ExpiresAt;
        info.SessionPurpose = session.Purpose;
        if (session.Purpose == SessionClaims.Full) return;
        info.EmpresaId = null;
        info.TipoUsuarioCodigo = "OPERADOR";
        info.Roles = Array.Empty<string>();
        info.Permisos = Array.Empty<string>();
    }

    // These fingerprints stay server-side. JSON encoding avoids delimiter ambiguities.
    private static string Credentials(Usuario user, string purpose) => Hash(new
    {
        user.SecurityStamp, user.PasswordHash, user.MfaHabilitado,
        Secret = purpose == SessionClaims.MfaEnroll ? null : user.MfaSecretoCifrado,
        user.SsoProveedor, user.SsoIssuer, user.SsoSubject
    });
    private static string Authorization(UserInfo user) => Hash(new
    {
        user.Id, user.EmpresaId, user.TipoUsuarioCodigo,
        Roles = user.Roles.OrderBy(x => x, StringComparer.Ordinal),
        Permissions = user.Permisos.OrderBy(x => x, StringComparer.Ordinal)
    });
    private static string Hash(object value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    private static Result<UserInfo> Invalid() => Result<UserInfo>.Fail(
        "Tu sesión venció o cambió su autorización. Inicia sesión nuevamente.", "AUTH_SESSION_INVALID");
}
