using System.ComponentModel.DataAnnotations;

namespace NeoSTP.Web.Models;

public class CreateEmpresaViewModel
{
    [Required, StringLength(20)]
    [Display(Name = "NIT")]
    public string Nit { get; set; } = string.Empty;

    [StringLength(20)]
    [Display(Name = "NRC")]
    public string? Nrc { get; set; }

    [Required, StringLength(250)]
    [Display(Name = "Razón social")]
    public string RazonSocial { get; set; } = string.Empty;

    [StringLength(250)]
    [Display(Name = "Nombre comercial")]
    public string? NombreComercial { get; set; }

    [StringLength(20)]
    [Display(Name = "Código actividad")]
    public string? CodigoActividad { get; set; }

    [StringLength(250)]
    [Display(Name = "Actividad económica")]
    public string? ActividadEconomica { get; set; }

    [StringLength(100)]
    [Display(Name = "Departamento")]
    public string? Departamento { get; set; }

    [StringLength(100)]
    [Display(Name = "Municipio")]
    public string? Municipio { get; set; }

    [StringLength(500)]
    [Display(Name = "Dirección")]
    public string? Direccion { get; set; }

    [StringLength(30)]
    [Display(Name = "Teléfono")]
    public string? Telefono { get; set; }

    [EmailAddress, StringLength(150)]
    [Display(Name = "Correo")]
    public string? Correo { get; set; }
}

public class EditEmpresaViewModel : CreateEmpresaViewModel
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Estado")]
    public string EstadoCodigo { get; set; } = "ACTIVA";
}

public class AsignarPlanViewModel
{
    public int EmpresaId { get; set; }
    public string EmpresaNombre { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Plan")]
    public int PlanId { get; set; }

    [Display(Name = "Inicio")]
    [DataType(DataType.Date)]
    public DateTime? FechaInicio { get; set; }

    [Display(Name = "Fin (opcional)")]
    [DataType(DataType.Date)]
    public DateTime? FechaFin { get; set; }
}
