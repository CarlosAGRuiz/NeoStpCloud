using NeoSTP.Application.Common;

namespace NeoSTP.Application.Onboarding;

public sealed class VerticalTemplateDto
{
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string Descripcion { get; set; } = null!;
    public string Icono { get; set; } = "storefront";
    public IReadOnlyList<string> Categorias { get; set; } = [];
}

public sealed class AplicarPlantillaResultDto
{
    public string Codigo { get; set; } = null!;
    public int CategoriasCreadas { get; set; }
    public int CategoriasExistentes { get; set; }
}

/// <summary>
/// Plantillas de vertical (farmacia, ferretería, salón, tienda): siembran las categorías
/// de producto típicas del rubro en el catálogo por empresa CATEGORIA_PRODUCTO.
/// Idempotente: aplicar dos veces no duplica.
/// </summary>
public interface IVerticalTemplateService
{
    IReadOnlyList<VerticalTemplateDto> Listar();
    Task<Result<AplicarPlantillaResultDto>> AplicarAsync(int empresaId, string codigo, string? actor, CancellationToken ct = default);
}
