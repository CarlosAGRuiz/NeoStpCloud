using System.ComponentModel.DataAnnotations;

namespace NeoSTP.Web.Models;

public class DteConfiguracionViewModel
{
    [Required, Display(Name = "Ambiente")]
    public string AmbienteCodigo { get; set; } = "PRUEBAS";

    [StringLength(100), Display(Name = "Usuario MH")]
    public string? UsuarioMh { get; set; }

    [DataType(DataType.Password), StringLength(200)]
    [Display(Name = "Password MH (dejar vacío para no cambiar)")]
    public string? PasswordMh { get; set; }

    public bool TienePasswordMh { get; set; }

    [Display(Name = "Tipo de establecimiento")]
    public string? TipoEstablecimientoCodigo { get; set; }

    [StringLength(20), Display(Name = "Código establecimiento (MH)")]
    public string? CodigoEstablecimientoMh { get; set; }

    [StringLength(20), Display(Name = "Código punto de venta (MH)")]
    public string? CodigoPuntoVentaMh { get; set; }

    // Solo lectura — info del cert ya cargado
    public bool TieneCertificado { get; set; }
    public string? CertificadoNombre { get; set; }
    public string? CertificadoHuella { get; set; }
    public DateTime? CertificadoVence { get; set; }

    public DateTime? UltimaPruebaAt { get; set; }
    public string? UltimaPruebaResultado { get; set; }
    public string? UltimaPruebaDetalle { get; set; }

    public bool EsCompleto { get; set; }
}

public class UploadCertificadoViewModel
{
    [Required, Display(Name = "Archivo de certificado (.crt o .pfx)")]
    public IFormFile? Archivo { get; set; }

    [DataType(DataType.Password), StringLength(200), Display(Name = "Password del certificado (opcional)")]
    public string? Password { get; set; }

    [Display(Name = "Emitido"), DataType(DataType.Date)]
    public DateTime? Emitido { get; set; }

    [Display(Name = "Vence"), DataType(DataType.Date)]
    public DateTime? Vence { get; set; }
}
