using System.ComponentModel.DataAnnotations;

namespace NeoSTP.Web.Models;

public class CreateProductoViewModel
{
    [Required, StringLength(50), Display(Name = "Código interno")]
    public string CodigoInterno { get; set; } = string.Empty;

    [StringLength(50), Display(Name = "Código de barra")]
    public string? CodigoBarra { get; set; }

    [Required, StringLength(250), Display(Name = "Nombre")]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(1000), Display(Name = "Descripción")]
    public string? Descripcion { get; set; }

    [Required, Display(Name = "Tipo")]
    public string TipoItem { get; set; } = "BIEN";

    [Required, Display(Name = "Unidad de medida")]
    public string UnidadMedidaCodigo { get; set; } = "UNIDAD";

    [Required, Range(0, double.MaxValue), Display(Name = "Precio unitario")]
    public decimal PrecioUnitario { get; set; }

    [Range(0, double.MaxValue), Display(Name = "Costo unitario")]
    public decimal? CostoUnitario { get; set; }

    [Display(Name = "Aplica IVA")]
    public bool AplicaIva { get; set; } = true;

    [StringLength(20), Display(Name = "Código tributo")]
    public string? TributoCodigo { get; set; }
}

public class EditProductoViewModel : CreateProductoViewModel
{
    public int Id { get; set; }

    [Required, Display(Name = "Estado")]
    public string EstadoCodigo { get; set; } = "ACTIVO";
}
