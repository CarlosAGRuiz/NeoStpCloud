using NeoSTP.Domain.Common;

namespace NeoSTP.Domain.Core.Dte;

/// <summary>
/// Copia histórica de la configuración fiscal previa a una modificación.
/// Los secretos permanecen cifrados y nunca se exponen en DTOs o auditoría.
/// </summary>
public sealed class DteConfiguracionVersion : AuditableEntity
{
    public int EmpresaId { get; set; }
    public int ConfiguracionId { get; set; }
    public string Motivo { get; set; } = null!;

    public string AmbienteCodigo { get; set; } = "PRUEBAS";
    public string? TiposDteAutorizadosCsv { get; set; }
    public string? UsuarioMh { get; set; }
    public string? PasswordMhCifrado { get; set; }
    public string? TipoEstablecimientoCodigo { get; set; }
    public string? CodigoEstablecimientoMh { get; set; }
    public string? CodigoPuntoVentaMh { get; set; }

    public byte[]? CertificadoBlob { get; set; }
    public string? CertificadoNombre { get; set; }
    public string? CertificadoHuella { get; set; }
    public DateTime? CertificadoEmitido { get; set; }
    public DateTime? CertificadoVence { get; set; }
    public string? PasswordCertificadoCifrado { get; set; }
}
