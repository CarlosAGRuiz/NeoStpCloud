namespace NeoSTP.Application.Productos.Dtos;

public class ProductoDto
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public string CodigoInterno { get; set; } = null!;
    public string? CodigoBarra { get; set; }
    public string Nombre { get; set; } = null!;
    public string? Descripcion { get; set; }
    public string TipoItem { get; set; } = "BIEN";
    public bool EsServicio { get; set; }
    /// <summary>Categoría del catálogo por empresa CATEGORIA_PRODUCTO.</summary>
    public string? CategoriaCodigo { get; set; }
    /// <summary>True si el inventario se maneja por lotes con vencimiento (FEFO).</summary>
    public bool ControlaLote { get; set; }
    public string UnidadMedidaCodigo { get; set; } = "59";
    public decimal PrecioUnitario { get; set; }
    public decimal? CostoUnitario { get; set; }
    public bool AplicaIva { get; set; }
    public string? TributoCodigo { get; set; }
    public string EstadoCodigo { get; set; } = "ACTIVO";
    public DateTime CreatedAt { get; set; }
}

public class CreateProductoRequest
{
    public string CodigoInterno { get; set; } = null!;
    public string? CodigoBarra { get; set; }
    public string Nombre { get; set; } = null!;
    public string? Descripcion { get; set; }
    public string TipoItem { get; set; } = "BIEN";
    /// <summary>Categoría (catálogo por empresa CATEGORIA_PRODUCTO). Se crea sola si no existe.</summary>
    public string? CategoriaCodigo { get; set; }
    /// <summary>True si el inventario se maneja por lotes con vencimiento (FEFO).</summary>
    public bool ControlaLote { get; set; }
    public string UnidadMedidaCodigo { get; set; } = "59";
    public decimal PrecioUnitario { get; set; }
    public decimal? CostoUnitario { get; set; }
    public bool AplicaIva { get; set; } = true;
    public string? TributoCodigo { get; set; }
}

public class UpdateProductoRequest : CreateProductoRequest
{
    public string EstadoCodigo { get; set; } = "ACTIVO";
}

// ─── Precios por volumen y unidades alternativas (Entrega 5) ─────────────────

public class PrecioEscalaDto
{
    public decimal CantidadMinima { get; set; }
    public decimal PrecioUnitario { get; set; }
}

public class UnidadAlternativaDto
{
    public string UnidadMedidaCodigo { get; set; } = "59";
    public string Nombre { get; set; } = null!;
    public decimal Factor { get; set; }
    /// <summary>Precio de la unidad alternativa; null = precio base × factor.</summary>
    public decimal? PrecioUnitario { get; set; }
}

public class ProductoPreciosDto
{
    public int ProductoId { get; set; }
    public decimal PrecioBase { get; set; }
    public List<PrecioEscalaDto> Escalas { get; set; } = [];
    public List<UnidadAlternativaDto> Unidades { get; set; } = [];
}

/// <summary>Reemplaza el juego completo de escalas y unidades del producto.</summary>
public class SetProductoPreciosRequest
{
    public List<PrecioEscalaDto> Escalas { get; set; } = [];
    public List<UnidadAlternativaDto> Unidades { get; set; } = [];
}
