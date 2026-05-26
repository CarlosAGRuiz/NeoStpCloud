using NeoSTP.Domain.Common;

namespace NeoSTP.Domain.Core.Licenciamiento;

public class Modulo : AuditableEntity
{
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string? Descripcion { get; set; }
    public string? Icono { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;

    public ICollection<PlanModulo> Planes { get; set; } = new List<PlanModulo>();
}
