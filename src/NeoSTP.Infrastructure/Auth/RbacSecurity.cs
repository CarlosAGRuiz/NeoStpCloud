using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Domain.Core.Seguridad;

namespace NeoSTP.Infrastructure.Auth;

internal static class RbacSecurity
{
    public static bool IsPlatformAdministrator(ICurrentUser? actor) =>
        actor?.IsAuthenticated == true && actor.UserId is > 0
        && actor.EmpresaId is null && actor.TipoUsuarioCodigo == "SUPERADMIN"
        && actor.IsInRole("SUPERADMIN");

    public static bool IsPlatformUser(Usuario user) =>
        user.EmpresaId is null && user.TipoUsuarioCodigo == "SUPERADMIN"
        && user.Roles.Any(r => r.Rol.Activo && r.Rol.EmpresaId is null
            && r.Rol.EsSistema && IsReservedRole(r.Rol.Codigo));

    public static bool IsMfaRequiredUser(Usuario user) =>
        IsPlatformUser(user) || IsTenantAdministrator(user);

    private static bool IsTenantAdministrator(Usuario user) =>
        user.EmpresaId is not null
        && (string.Equals(user.TipoUsuarioCodigo, "ADMIN", StringComparison.OrdinalIgnoreCase)
            || user.Roles.Any(r => r.Rol.Activo
                && (r.Rol.EmpresaId is null || r.Rol.EmpresaId == user.EmpresaId)
                && (string.Equals(r.Rol.Codigo, "ADMIN", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(r.Rol.Codigo, "ADMIN_EMPRESA", StringComparison.OrdinalIgnoreCase))));

    public static bool IsReservedRole(string? code) =>
        string.Equals(code?.Trim(), "SUPERADMIN", StringComparison.OrdinalIgnoreCase);

    public static bool IsPlatformPermission(string code) =>
        code.StartsWith("SuperAdmin.", StringComparison.OrdinalIgnoreCase)
        || code.StartsWith("Ops.Hardening.", StringComparison.OrdinalIgnoreCase);

    public static bool CanAssignToTenant(Rol role, int empresaId) =>
        role.Activo && IsTenantRole(role, empresaId);

    public static bool IsTenantRole(Rol role, int empresaId) =>
        (role.EmpresaId is null || role.EmpresaId == empresaId)
        && !IsReservedRole(role.Codigo)
        && !role.Permisos.Any(p => IsPlatformPermission(p.Permiso.Codigo)
            || string.Equals(p.Permiso.Modulo, "ADMIN", StringComparison.OrdinalIgnoreCase));
}
