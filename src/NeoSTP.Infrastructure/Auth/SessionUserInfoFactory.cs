using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Dtos;
using NeoSTP.Application.Common;
using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Seguridad;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Auth;

internal static class SessionUserInfoFactory
{
    internal static async Task<Result<UserInfo>> ResolveAsync(NeoStpDbContext db, Usuario usuario, int? empresaId, CancellationToken ct)
    {
        var userInfo = FromUser(usuario);
        if (empresaId == usuario.EmpresaId)
        {
            if (empresaId is not null && !await db.Empresas.AsNoTracking()
                .AnyAsync(e => e.Id == empresaId && e.EstadoCodigo == EmpresaEstados.Activa, ct))
                return Result<UserInfo>.Fail("La empresa está suspendida o inactiva.", "EMPRESA_SUSPENDIDA");
            return Result<UserInfo>.Ok(userInfo);
        }

        if (empresaId is null)
            return Result<UserInfo>.Fail("La sesión ya no corresponde a tu empresa. Inicia sesión nuevamente.", "AUTH_INVALID_CONTEXT");

        var membresia = await db.UsuarioEmpresas.AsNoTracking()
            .Include(m => m.Empresa)
            .Include(m => m.Rol).ThenInclude(r => r.Permisos).ThenInclude(rp => rp.Permiso)
            .FirstOrDefaultAsync(m => m.UsuarioId == usuario.Id && m.EmpresaId == empresaId
                && m.EstadoCodigo == EstadoCodes.Activo, ct);
        if (membresia is null || !RbacSecurity.CanAssignToTenant(membresia.Rol, empresaId.Value))
            return Result<UserInfo>.Fail("Ya no tienes un rol activo para operar en esa empresa.", "EMPRESA_NO_MEMBRESIA");
        if (membresia.Empresa.EstadoCodigo != EmpresaEstados.Activa)
            return Result<UserInfo>.Fail("La empresa está suspendida o inactiva.", "EMPRESA_SUSPENDIDA");

        userInfo.EmpresaId = empresaId;
        // El tipo de la cuenta principal no concede administración en una membresía.
        userInfo.TipoUsuarioCodigo = "OPERADOR";
        userInfo.Roles = new[] { membresia.Rol.Codigo };
        userInfo.Permisos = membresia.Rol.Permisos.Select(rp => rp.Permiso.Codigo).Distinct().ToList();
        return Result<UserInfo>.Ok(userInfo);
    }

    internal static UserInfo FromUser(Usuario u)
    {
        var platformAdmin = RbacSecurity.IsPlatformUser(u);
        var roles = u.Roles.Select(ur => ur.Rol).Where(r => r.Activo
            && (u.EmpresaId is int empresaId
                ? RbacSecurity.IsTenantRole(r, empresaId)
                : r.EmpresaId is null && (platformAdmin
                    || (!RbacSecurity.IsReservedRole(r.Codigo)
                        && !r.Permisos.Any(p => RbacSecurity.IsPlatformPermission(p.Permiso.Codigo)
                            || string.Equals(p.Permiso.Modulo, "ADMIN", StringComparison.OrdinalIgnoreCase))))))
            .ToList();
        return new UserInfo
        {
            Id = u.Id,
            EmpresaId = u.EmpresaId,
            Username = u.Username,
            Email = u.Email,
            NombreCompleto = u.NombreCompleto,
            TipoUsuarioCodigo = u.TipoUsuarioCodigo == "SUPERADMIN" && !platformAdmin ? "OPERADOR" : u.TipoUsuarioCodigo,
            UltimoLogin = u.UltimoLogin,
            Roles = roles.Select(r => r.Codigo).Distinct().ToList(),
            Permisos = roles.SelectMany(r => r.Permisos.Select(rp => rp.Permiso.Codigo)).Distinct().ToList(),
        };
    }

}
