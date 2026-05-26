using System.ComponentModel.DataAnnotations;

namespace NeoSTP.Web.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Usuario o correo es obligatorio.")]
    [Display(Name = "Usuario o correo")]
    public string UsernameOrEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Contraseña es obligatoria.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Recordarme")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}
