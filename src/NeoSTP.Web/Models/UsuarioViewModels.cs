using System.ComponentModel.DataAnnotations;

namespace NeoSTP.Web.Models;

public class CreateUsuarioViewModel
{
    [Required, StringLength(100)]
    [Display(Name = "Usuario")]
    public string Username { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(200)]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(200)]
    [Display(Name = "Nombre completo")]
    public string NombreCompleto { get; set; } = string.Empty;

    [StringLength(30)]
    [Display(Name = "Teléfono")]
    public string? Telefono { get; set; }

    [Required, MinLength(8)]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña inicial")]
    public string Password { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Tipo de usuario")]
    public string TipoUsuarioCodigo { get; set; } = "OPERADOR";

    [Display(Name = "Roles")]
    public int[] RoleIds { get; set; } = Array.Empty<int>();

    [Range(typeof(bool), "true", "true", ErrorMessage = "Debes aceptar los Términos y Condiciones y la Política de Privacidad para continuar.")]
    [Display(Name = "Acepto los Términos y Condiciones y la Política de Privacidad")]
    public bool AceptaTerminos { get; set; }
}

public class EditUsuarioViewModel
{
    public int Id { get; set; }

    [Display(Name = "Usuario")]
    public string Username { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(200)]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(200)]
    [Display(Name = "Nombre completo")]
    public string NombreCompleto { get; set; } = string.Empty;

    [StringLength(30)]
    [Display(Name = "Teléfono")]
    public string? Telefono { get; set; }

    [Required]
    [Display(Name = "Tipo de usuario")]
    public string TipoUsuarioCodigo { get; set; } = "OPERADOR";

    [Required]
    [Display(Name = "Estado")]
    public string EstadoCodigo { get; set; } = "ACTIVO";

    [Display(Name = "Roles")]
    public int[] RoleIds { get; set; } = Array.Empty<int>();
}

public class ChangePasswordViewModel
{
    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña actual")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, MinLength(8)]
    [DataType(DataType.Password)]
    [Display(Name = "Nueva contraseña")]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Confirmar nueva contraseña")]
    [Compare(nameof(NewPassword), ErrorMessage = "Las contraseñas no coinciden.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
