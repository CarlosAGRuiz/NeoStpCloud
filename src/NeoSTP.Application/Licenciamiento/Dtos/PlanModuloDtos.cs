namespace NeoSTP.Application.Licenciamiento.Dtos;

public class ModuloDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string? Descripcion { get; set; }
    public string? Icono { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; }
}

public class PlanDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string? Descripcion { get; set; }
    public decimal PrecioMensual { get; set; }
    public string MonedaCodigo { get; set; } = "USD";
    public int? LimiteUsuarios { get; set; }
    public int? LimiteSucursales { get; set; }
    public int? LimitePuntosVenta { get; set; }
    public int? LimiteDteMensual { get; set; }
    public bool Activo { get; set; }
    public IReadOnlyList<ModuloDto> Modulos { get; set; } = Array.Empty<ModuloDto>();
}
