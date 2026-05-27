namespace NeoSTP.Application.Dte.Abstractions;

public class HaciendaAuthResult
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? Mensaje { get; set; }
    public int? CodigoHttp { get; set; }
    public string? Detalle { get; set; }
}

/// <summary>
/// Cliente que autentica contra la API de Hacienda El Salvador.
/// MVP usa mock; Sprint 5/6 cablea el cliente HTTP real contra:
///   PRUEBAS:    https://apitest.dtes.mh.gob.sv/seguridad/auth
///   PRODUCCION: https://api.dtes.mh.gob.sv/seguridad/auth
/// </summary>
public interface IHaciendaAuthClient
{
    Task<HaciendaAuthResult> AutenticarAsync(string usuario, string password, string ambienteCodigo, CancellationToken ct = default);
}
