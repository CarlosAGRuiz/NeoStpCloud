using System.ComponentModel.DataAnnotations;

namespace NeoSTP.Web.Models;

public class CreateCatalogoViewModel
{
    [Required, StringLength(50), RegularExpression("^[A-Za-z0-9_]+$", ErrorMessage = "Solo letras, números y guion bajo."), Display(Name = "Código")]
    public string Codigo { get; set; } = string.Empty;

    [Required, StringLength(150), Display(Name = "Nombre")]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(500), Display(Name = "Descripción")]
    public string? Descripcion { get; set; }
}

public class EditCatalogoViewModel
{
    public string Codigo { get; set; } = string.Empty;

    [Required, StringLength(150), Display(Name = "Nombre")]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(500), Display(Name = "Descripción")]
    public string? Descripcion { get; set; }

    [Display(Name = "Activo")]
    public bool Activo { get; set; } = true;
}

public class CatalogoItemFormViewModel
{
    public int? ItemId { get; set; }

    [Required, StringLength(30), Display(Name = "Código")]
    public string Codigo { get; set; } = string.Empty;

    [Required, StringLength(250), Display(Name = "Valor")]
    public string Valor { get; set; } = string.Empty;

    [StringLength(500), Display(Name = "Descripción")]
    public string? Descripcion { get; set; }

    [Display(Name = "Orden")]
    public int Orden { get; set; }

    [StringLength(30), Display(Name = "Código padre")]
    public string? ParentCodigo { get; set; }

    [Display(Name = "Activo")]
    public bool Activo { get; set; } = true;
}
