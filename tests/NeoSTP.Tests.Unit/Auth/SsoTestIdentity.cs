using NeoSTP.Application.Auth.Dtos;
using NeoSTP.Domain.Core.Seguridad;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Tests.Unit.Auth;

internal static class SsoTestIdentity
{
    internal const string Tenant = "11111111-1111-1111-1111-111111111111";
    internal const string Issuer = "https://login.microsoftonline.com/" + Tenant + "/v2.0";
    internal static ExternalLoginInfo Info(string subject = "fixture-subject", string? email = null) => new()
    { Proveedor = SsoProveedores.Entra, Subject = subject, Email = email, TenantIdExterno = Tenant, Issuer = Issuer };
    internal static void Configure(NeoStpDbContext db, int empresaId, string domain = "example.test") => db.EmpresaSso.Add(new EmpresaSso
    { EmpresaId = empresaId, ProveedorCodigo = SsoProveedores.Entra, Habilitado = true, DominioCorreo = domain, TenantIdExterno = Tenant });
}
