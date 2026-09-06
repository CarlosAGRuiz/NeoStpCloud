using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Dtos;
using NeoSTP.Application.Common;
using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Seguridad;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Auth;

/// <summary>Authorization after OIDC signature/audience/nonce/issuer validation by the host.</summary>
public static class SsoIdentityPolicy
{
    public static Result ValidateIdentity(ExternalLoginInfo info)
    {
        if (!SsoProveedores.EsValido(info.Proveedor))
            return Result.Fail("Proveedor de SSO no soportado.", "SSO_PROVIDER_INVALID");
        if (string.IsNullOrWhiteSpace(info.Subject) || info.Subject.Length > 200)
            return Result.Fail("Identidad de SSO incompleta.", "SSO_BAD_INPUT");
        if (info.Proveedor == SsoProveedores.Entra)
        {
            if (!Guid.TryParse(info.TenantIdExterno, out var tenant) || tenant == Guid.Empty
                || info.Issuer != $"https://login.microsoftonline.com/{tenant:D}/v2.0")
                return Result.Fail("El emisor de la identidad no corresponde al directorio corporativo.", "SSO_ISSUER_INVALID");
        }
        else if (info.Issuer != "https://accounts.google.com")
            return Result.Fail("Emisor de identidad no válido.", "SSO_ISSUER_INVALID");
        return Result.Ok();
    }

    public static async Task<Result<EmpresaSso>> ResolveAsync(NeoStpDbContext db, ExternalLoginInfo info,
        int? empresaId = null, CancellationToken ct = default)
    {
        var identity = ValidateIdentity(info);
        if (identity.IsFailure) return Result<EmpresaSso>.Fail(identity.Error!, identity.ErrorCode);
        var email = info.Email?.Trim().ToLowerInvariant();
        var domain = email?.Split('@');
        if (empresaId is null && (domain?.Length != 2 || string.IsNullOrEmpty(domain[0])))
            return Result<EmpresaSso>.Fail("El proveedor no entregó un correo corporativo válido.", "SSO_SIN_CORREO");
        var emailDomain = domain?.LastOrDefault();
        var config = await db.EmpresaSso.Include(c => c.Empresa).FirstOrDefaultAsync(c =>
            empresaId != null ? c.EmpresaId == empresaId : c.DominioCorreo == emailDomain, ct);
        if (config is null || !config.Habilitado)
            return Result<EmpresaSso>.Fail("SSO no está habilitado para esta empresa. Usa tu acceso local o contacta al administrador.", "SSO_SIN_CUENTA");
        if (config.ProveedorCodigo != info.Proveedor)
            return Result<EmpresaSso>.Fail("Proveedor no autorizado para esta empresa.", "SSO_PROVEEDOR_NO_COINCIDE");
        if (info.Proveedor == SsoProveedores.Entra)
        {
            if (!Guid.TryParse(config.TenantIdExterno, out var expected)
                || !Guid.TryParse(info.TenantIdExterno, out var actual) || expected != actual)
                return Result<EmpresaSso>.Fail("Tu directorio corporativo no está autorizado para esta empresa.", "SSO_TENANT_NO_COINCIDE");
        }
        else if (!info.EmailVerified || !string.Equals(info.HostedDomain, config.DominioCorreo, StringComparison.OrdinalIgnoreCase))
            return Result<EmpresaSso>.Fail("Se requiere una cuenta verificada del Google Workspace de la empresa.", "SSO_DOMAIN_UNVERIFIED");
        if (config.Empresa is null || config.Empresa.EstadoCodigo != EmpresaEstados.Activa)
            return Result<EmpresaSso>.Fail("La empresa está suspendida o inactiva.", "EMPRESA_SUSPENDIDA");
        return Result<EmpresaSso>.Ok(config);
    }
}
