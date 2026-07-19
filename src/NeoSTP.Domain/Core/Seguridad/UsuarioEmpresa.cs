using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Domain.Core.Seguridad;

/// <summary>
/// Membresía de un usuario en una empresa ADICIONAL a su empresa principal
/// (<see cref="Usuario.EmpresaId"/>). Habilita el plan Contador (un profesional
/// atiende varios clientes con un solo login) y los grupos empresariales.
/// El rol define los permisos del usuario cuando opera en esa empresa.
/// Las membresías no consumen el límite de usuarios del plan de la empresa.
/// </summary>
public class UsuarioEmpresa : AuditableEntity
{
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    /// <summary>Rol que rige los permisos del usuario dentro de esta empresa.</summary>
    public int RolId { get; set; }
    public Rol Rol { get; set; } = null!;

    /// <summary>ACTIVO | INACTIVO (revocada sin borrar el historial).</summary>
    public string EstadoCodigo { get; set; } = "ACTIVO";
}
