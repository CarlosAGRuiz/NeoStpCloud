using NeoSTP.Domain.Common;

namespace NeoSTP.Domain.Core.Licenciamiento;

public class Plan : AuditableEntity
{
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string? Descripcion { get; set; }
    public decimal PrecioMensual { get; set; }
    public string MonedaCodigo { get; set; } = "USD";
    public int? LimiteUsuarios { get; set; }
    public int? LimiteSucursales { get; set; }
    public int? LimitePuntosVenta { get; set; }
    public int? LimiteDteMensual { get; set; }
    public bool Activo { get; set; } = true;

    public ICollection<PlanModulo> Modulos { get; set; } = new List<PlanModulo>();
}
