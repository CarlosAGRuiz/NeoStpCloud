using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Domain.Core.Seguridad;

/// <summary>
/// Configuración de inicio de sesión federado (SSO OIDC) de una empresa (E3).
/// El SaaS registra una sola app multi-tenant de Microsoft Entra y un cliente de
/// Google; cada empresa declara aquí el <see cref="DominioCorreo"/> con el que sus
/// usuarios llegan por SSO, y opcionalmente el <see cref="TenantIdExterno"/> (tid de
/// Entra) para restringir la validación a su directorio corporativo.
/// </summary>
public class EmpresaSso : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa? Empresa { get; set; }

    /// <summary>Código del proveedor OIDC: ENTRA | GOOGLE (ver <see cref="SsoProveedores"/>).</summary>
    public string ProveedorCodigo { get; set; } = SsoProveedores.Entra;

    /// <summary>Si está deshabilitado, el SSO de esta empresa no resuelve ni auto-aprovisiona.</summary>
    public bool Habilitado { get; set; }

    /// <summary>Dominio de correo corporativo (p. ej. "contoso.com") que mapea a esta empresa.</summary>
    public string DominioCorreo { get; set; } = null!;

    /// <summary>Tenant id externo (Entra "tid") para validar que el usuario viene del directorio de la empresa. Opcional.</summary>
    public string? TenantIdExterno { get; set; }

    /// <summary>Si es true, un usuario federado sin cuenta local se crea automáticamente con <see cref="RolPorDefectoId"/>.</summary>
    public bool AutoProvisionar { get; set; }

    /// <summary>Rol asignado al auto-aprovisionar. Requerido cuando <see cref="AutoProvisionar"/> es true.</summary>
    public int? RolPorDefectoId { get; set; }
    public Rol? RolPorDefecto { get; set; }

    public string? Notas { get; set; }
}

/// <summary>Proveedores de identidad OIDC soportados por el SSO (E3).</summary>
public static class SsoProveedores
{
    public const string Entra = "ENTRA";
    public const string Google = "GOOGLE";

    public static readonly string[] All = [Entra, Google];

    public static bool EsValido(string? codigo) =>
        codigo is not null && All.Contains(codigo, StringComparer.Ordinal);
}
