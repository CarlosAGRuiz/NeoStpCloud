namespace NeoSTP.Application.Dte.Dtos;

public class DteConfiguracionDto
{
    public int EmpresaId { get; set; }
    public string AmbienteCodigo { get; set; } = "PRUEBAS";

    public string? UsuarioMh { get; set; }
    /// <summary>True si hay password configurado; el valor nunca se devuelve.</summary>
    public bool TienePasswordMh { get; set; }

    public string? TipoEstablecimientoCodigo { get; set; }
    public string? CodigoEstablecimientoMh { get; set; }
    public string? CodigoPuntoVentaMh { get; set; }

    /// <summary>True si hay certificado cargado; el blob no se devuelve.</summary>
    public bool TieneCertificado { get; set; }
    public string? CertificadoNombre { get; set; }
    public string? CertificadoHuella { get; set; }
    public DateTime? CertificadoEmitido { get; set; }
    public DateTime? CertificadoVence { get; set; }
    public bool CertificadoTienePassword { get; set; }

    public DateTime? UltimaPruebaAt { get; set; }
    public string? UltimaPruebaResultado { get; set; }
    public string? UltimaPruebaDetalle { get; set; }

    public bool EsCompleto { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class SaveDteConfiguracionRequest
{
    public string AmbienteCodigo { get; set; } = "PRUEBAS";
    public string? UsuarioMh { get; set; }
    /// <summary>Solo se actualiza si viene con valor. null/empty significa "no cambiar".</summary>
    public string? PasswordMh { get; set; }
    public string? TipoEstablecimientoCodigo { get; set; }
    public string? CodigoEstablecimientoMh { get; set; }
    public string? CodigoPuntoVentaMh { get; set; }
}

public class UploadCertificadoRequest
{
    public string Nombre { get; set; } = null!;
    /// <summary>Contenido del certificado en base64.</summary>
    public string ContenidoBase64 { get; set; } = null!;
    public string? Password { get; set; }
    public DateTime? Emitido { get; set; }
    public DateTime? Vence { get; set; }
}

public class ProbarConexionResultadoDto
{
    public bool Exitoso { get; set; }
    public string? Mensaje { get; set; }
    public int? CodigoHttp { get; set; }
    public string? Detalle { get; set; }
    public DateTime ProbadoAt { get; set; } = DateTime.UtcNow;
}
