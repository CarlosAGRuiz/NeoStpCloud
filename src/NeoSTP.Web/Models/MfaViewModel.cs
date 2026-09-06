using System.ComponentModel.DataAnnotations;
using NeoSTP.Application.Ops;

namespace NeoSTP.Web.Models;

public sealed class MfaViewModel
{
    [Required(ErrorMessage = "Ingresa el código de tu aplicación autenticadora.")]
    [StringLength(32)]
    [DataType(DataType.Password)]
    public string Code { get; set; } = string.Empty;
    // Display-only values; actions bind only Code and never accept a secret from the browser.
    public MfaEnrollDto? Enrollment { get; set; }
}
