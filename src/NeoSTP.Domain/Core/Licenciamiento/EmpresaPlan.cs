using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Domain.Core.Licenciamiento;

public class EmpresaPlan : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    public int PlanId { get; set; }
    public Plan Plan { get; set; } = null!;

    public DateTime FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
    public string EstadoCodigo { get; set; } = "ACTIVO";
}
