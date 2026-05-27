using System.ComponentModel.DataAnnotations;

namespace NeoSTP.Web.Models;

public class CreateClienteViewModel
{
    [Required, Display(Name = "Tipo de documento")]
    public string TipoDocumentoCodigo { get; set; } = "DUI";

    [Required, StringLength(50), Display(Name = "Número de documento")]
    public string NumeroDocumento { get; set; } = string.Empty;

    [StringLength(20), Display(Name = "NRC")]
    public string? Nrc { get; set; }

    [Required, StringLength(250), Display(Name = "Nombre / Razón social")]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(250), Display(Name = "Nombre comercial")]
    public string? NombreComercial { get; set; }

    [Required, Display(Name = "Tipo de contribuyente")]
    public string TipoContribuyenteCodigo { get; set; } = "CONSUMIDOR_FINAL";

    [StringLength(20), Display(Name = "Código actividad")]
    public string? CodigoActividad { get; set; }

    [StringLength(250), Display(Name = "Actividad económica")]
    public string? ActividadEconomica { get; set; }

    [StringLength(30), Display(Name = "Departamento")]
    public string? DepartamentoCodigo { get; set; }

    [StringLength(100), Display(Name = "Municipio")]
    public string? MunicipioCodigo { get; set; }

    [StringLength(500), Display(Name = "Dirección")]
    public string? Direccion { get; set; }

    [EmailAddress, StringLength(150), Display(Name = "Correo")]
    public string? Correo { get; set; }

    [StringLength(30), Display(Name = "Teléfono")]
    public string? Telefono { get; set; }
}

public class EditClienteViewModel : CreateClienteViewModel
{
    public int Id { get; set; }

    [Required, Display(Name = "Estado")]
    public string EstadoCodigo { get; set; } = "ACTIVO";
}
