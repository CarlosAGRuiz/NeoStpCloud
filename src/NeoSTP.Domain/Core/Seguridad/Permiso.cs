using NeoSTP.Domain.Common;

namespace NeoSTP.Domain.Core.Seguridad;

public class Permiso : AuditableEntity
{
    public string Codigo { get; set; } = null!;
    public string Modulo { get; set; } = null!;
    public string? Descripcion { get; set; }

    public ICollection<RolPermiso> Roles { get; set; } = new List<RolPermiso>();
}
