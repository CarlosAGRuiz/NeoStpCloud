using System.Security.Claims;

namespace NeoSTP.Application.Auth;

public static class SessionClaims
{
    public const string Id = "session_id";
    public const string Purpose = "session_purpose";
    public const string Full = "FULL";
    public const string MfaEnroll = "MFA_ENROLL";
    public const string MfaVerify = "MFA_VERIFY";

    public static bool IsRestricted(ClaimsPrincipal user) =>
        user.FindFirst(Purpose)?.Value is MfaEnroll or MfaVerify;

    public static bool IsPlatformAdministrator(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true
        && user.FindFirst("tipo_usuario")?.Value == "SUPERADMIN"
        && !user.HasClaim(c => c.Type == "empresa_id")
        && user.IsInRole("SUPERADMIN");

    public static bool IsPlatformPermission(string code) =>
        code.StartsWith("SuperAdmin.", StringComparison.OrdinalIgnoreCase)
        || code.StartsWith("Ops.Hardening.", StringComparison.OrdinalIgnoreCase);
}
