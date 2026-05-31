namespace NeoSTP.Application.Dte.Abstractions;

/// <summary>
/// Solicitud de transmisión de un Evento de Contingencia a Hacienda.
/// Endpoint: POST {base}/fesv/contingencia
/// </summary>
public class ContingenciaRequest
{
    /// <summary>NIT del emisor.</summary>
    public string Nit { get; set; } = null!;
    /// <summary>"00" pruebas / "01" producción.</summary>
    public string Ambiente { get; set; } = "00";
    /// <summary>JWS firmado del evento de contingencia.</summary>
    public string Documento { get; set; } = null!;
    /// <summary>Token Bearer activo de Hacienda (sin el prefijo "Bearer ").</summary>
    public string Token { get; set; } = null!;
    public string AmbienteCodigo { get; set; } = "PRUEBAS";
}

public class ContingenciaResult
{
    public bool Success { get; set; }
    public int? CodigoHttp { get; set; }
    public string? Estado { get; set; }
    /// <summary>Sello de recepción del evento (o código de lote).</summary>
    public string? SelloRecibido { get; set; }
    public string? CodigoMsg { get; set; }
    public string? DescripcionMsg { get; set; }
    public IReadOnlyList<string> Observaciones { get; set; } = Array.Empty<string>();
    public string? Raw { get; set; }
}

/// <summary>Cliente de transmisión de Evento de Contingencia (POST {base}/fesv/contingencia).</summary>
public interface IHaciendaContingenciaClient
{
    Task<ContingenciaResult> EnviarAsync(ContingenciaRequest request, CancellationToken ct = default);
}
