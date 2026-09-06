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

    /// <summary>Complemento administrativo por empresa, independiente del catálogo del plan.</summary>
    public bool ComplementoAutorizado { get; set; }
    public DateTime? ComplementoAutorizadoAt { get; set; }
    public string? ComplementoAutorizadoBy { get; set; }
    public string? ComplementoMotivo { get; set; }
}
