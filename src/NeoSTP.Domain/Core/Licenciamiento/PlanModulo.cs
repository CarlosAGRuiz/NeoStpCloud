namespace NeoSTP.Domain.Core.Licenciamiento;

public class PlanModulo
{
    public int PlanId { get; set; }
    public Plan Plan { get; set; } = null!;

    public int ModuloId { get; set; }
    public Modulo Modulo { get; set; } = null!;

    public bool Activo { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
