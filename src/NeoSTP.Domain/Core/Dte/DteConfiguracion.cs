using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Domain.Core.Dte;

/// <summary>
/// Configuración fiscal DTE de una empresa (Hacienda El Salvador).
/// Relación 1-a-1 con Empresa: una empresa tiene una sola configuración DTE activa.
/// Los campos sensibles se almacenan cifrados con DataProtection.
/// </summary>
public class DteConfiguracion : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    /// <summary>Catálogo AMBIENTE_DTE: PRUEBAS o PRODUCCION.</summary>
    public string AmbienteCodigo { get; set; } = "PRUEBAS";

    // ----- Credenciales API Hacienda (usuario MH) -----
    public string? UsuarioMh { get; set; }
    /// <summary>Cifrado con IDataProtector. Nunca exponer al cliente.</summary>
    public string? PasswordMhCifrado { get; set; }

    // ----- Datos del establecimiento emisor por defecto -----
    /// <summary>Catálogo TIPO_ESTABLECIMIENTO: CASA_MATRIZ, SUCURSAL, BODEGA…</summary>
    public string? TipoEstablecimientoCodigo { get; set; }
    public string? CodigoEstablecimientoMh { get; set; }
    public string? CodigoPuntoVentaMh { get; set; }

    // ----- Certificado de firma (.cer / .crt / .pfx) -----
    public byte[]? CertificadoBlob { get; set; }
    public string? CertificadoNombre { get; set; }
    public string? CertificadoHuella { get; set; }
    public DateTime? CertificadoEmitido { get; set; }
    public DateTime? CertificadoVence { get; set; }
    /// <summary>Cifrado con IDataProtector si el .pfx tiene password.</summary>
    public string? PasswordCertificadoCifrado { get; set; }

    // ----- Última prueba de conexión -----
    public DateTime? UltimaPruebaAt { get; set; }
    public string? UltimaPruebaResultado { get; set; }
    public string? UltimaPruebaDetalle { get; set; }

    // ----- Token MH cacheado -----
    public string? TokenMhCifrado { get; set; }
    public DateTime? TokenMhExpiraAt { get; set; }

    /// <summary>True cuando todos los datos obligatorios para emitir DTE están presentes.</summary>
    public bool EsCompleto =>
        !string.IsNullOrEmpty(UsuarioMh) &&
        !string.IsNullOrEmpty(PasswordMhCifrado) &&
        CertificadoBlob is { Length: > 0 } &&
        !string.IsNullOrEmpty(TipoEstablecimientoCodigo) &&
        !string.IsNullOrEmpty(CodigoEstablecimientoMh) &&
        !string.IsNullOrEmpty(CodigoPuntoVentaMh);
}
