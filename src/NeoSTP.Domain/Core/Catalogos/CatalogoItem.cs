using NeoSTP.Domain.Common;

namespace NeoSTP.Domain.Core.Catalogos;

public class CatalogoItem : AuditableEntity
{
    public int CatalogoId { get; set; }
    public Catalogo Catalogo { get; set; } = null!;

    public string Codigo { get; set; } = null!;
    public string Valor { get; set; } = null!;
    public string? Descripcion { get; set; }
    public int Orden { get; set; }
    public bool EsSistema { get; set; }
    public bool Activo { get; set; } = true;

    /// <summary>
    /// Código del ítem padre dentro del mismo catálogo (cascadas). Por ejemplo,
    /// un municipio referencia el código del departamento, y un distrito el del
    /// municipio. Null para ítems de primer nivel.
    /// </summary>
    public string? ParentCodigo { get; set; }

    /// <summary>JSON libre con atributos extra (símbolo, color, tasa, etc.).</summary>
    public string? MetadataJson { get; set; }
}
