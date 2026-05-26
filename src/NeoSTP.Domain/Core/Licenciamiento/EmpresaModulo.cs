using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Domain.Core.Licenciamiento;

public class EmpresaModulo
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    public int ModuloId { get; set; }
    public Modulo Modulo { get; set; } = null!;

    public bool Activo { get; set; } = true;
    public DateTime FechaActivacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaInactivacion { get; set; }
}
