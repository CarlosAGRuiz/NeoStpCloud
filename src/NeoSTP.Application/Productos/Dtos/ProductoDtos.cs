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
