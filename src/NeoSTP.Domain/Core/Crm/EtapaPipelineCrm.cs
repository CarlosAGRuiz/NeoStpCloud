using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Domain.Core.Crm;

public class EtapaPipelineCrm : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public int Orden { get; set; }
    public decimal ProbabilidadDefault { get; set; }
    public bool Activa { get; set; } = true;
    public bool EsCierreGanado { get; set; }
    public bool EsCierrePerdido { get; set; }

    public ICollection<OportunidadCrm> Oportunidades { get; set; } = new List<OportunidadCrm>();
}
