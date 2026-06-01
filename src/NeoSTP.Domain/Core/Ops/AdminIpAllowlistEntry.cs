using NeoSTP.Domain.Common;

namespace NeoSTP.Domain.Core.Ops;

/// <summary>
/// Entrada de la lista blanca de IPs autorizadas para acceder al panel
/// administrativo / SuperAdmin. Es configuración de sistema (sin EmpresaId).
/// Cuando hay al menos una entrada activa, el acceso a rutas protegidas
/// queda restringido a las IP/CIDR listadas.
/// </summary>
public class AdminIpAllowlistEntry : AuditableEntity
{
    /// <summary>Dirección IP o rango CIDR autorizado (ej. "203.0.113.4" o "10.0.0.0/24").</summary>
    public string IpCidr { get; set; } = null!;

    public string? Descripcion { get; set; }

    public bool Activo { get; set; } = true;
}
