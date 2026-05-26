using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Domain.Core.Seguridad;

public class Rol : AuditableEntity
{
    public int? EmpresaId { get; set; }
    public Empresa? Empresa { get; set; }

    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string? Descripcion { get; set; }
    public bool EsSistema { get; set; }
    public bool Activo { get; set; } = true;

    public ICollection<RolPermiso> Permisos { get; set; } = new List<RolPermiso>();
    public ICollection<UsuarioRol> Usuarios { get; set; } = new List<UsuarioRol>();
}
