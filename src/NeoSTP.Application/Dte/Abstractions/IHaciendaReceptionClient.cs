namespace NeoSTP.Application.Dte.Abstractions;

public class HaciendaReceptionRequest
{
    /// <summary>"00" pruebas / "01" producción.</summary>
    public string Ambiente { get; set; } = "00";
    /// <summary>Identificador único del envío (cualquier número ascendente; suele usarse el id interno).</summary>
    public int IdEnvio { get; set; }
    public int Version { get; set; } = 2;
    /// <summary>Catálogo CAT-002 (01, 03, 05, 06, 14, …).</summary>
    public string TipoDte { get; set; } = "01";
    /// <summary>JWS firmado del DTE.</summary>
    public string Documento { get; set; } = null!;
    /// <summary>Código de generación del DTE (UUID en mayúsculas).</summary>
    public string CodigoGeneracion { get; set; } = null!;
    /// <summary>Token Bearer activo de Hacienda.</summary>
    public string Token { get; set; } = null!;
    public string AmbienteCodigo { get; set; } = "PRUEBAS"; // PRUEBAS/PRODUCCION
}

public class HaciendaReceptionResult
{
    public bool Success { get; set; }
    public int? CodigoHttp { get; set; }
    /// <summary>Estado devuelto por MH: PROCESADO, RECHAZADO, CONTINGENCIA, etc.</summary>
    public string? Estado { get; set; }
    public string? SelloRecibido { get; set; }
    public DateTime? FhProcesamiento { get; set; }
    public string? ClasificaMsg { get; set; }
    public string? CodigoMsg { get; set; }
    public string? DescripcionMsg { get; set; }
    public IReadOnlyList<string> Observaciones { get; set; } = Array.Empty<string>();
    /// <summary>Cuerpo crudo de la respuesta de MH para auditoría.</summary>
    public string? Raw { get; set; }
}

/// <summary>
/// Cliente de recepción DTE de Hacienda El Salvador.
///
/// Endpoint:  POST {base}/fesv/recepciondte
/// Headers:   Authorization: Bearer {token}, Content-Type: application/json
/// Body:      { ambiente, idEnvio, version, tipoDte, documento, codigoGeneracion }
/// </summary>
public interface IHaciendaReceptionClient
{
    Task<HaciendaReceptionResult> EnviarAsync(HaciendaReceptionRequest request, CancellationToken ct = default);
}
