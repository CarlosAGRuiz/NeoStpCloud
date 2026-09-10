namespace NeoSTP.Application.Dte.Abstractions;

/// <summary>Consulta explícita de un DTE ya transmitido. No genera ni reenvía documentos.</summary>
public sealed class HaciendaConsultaDteRequest
{
    public string Ambiente { get; init; } = null!;
    public string AmbienteCodigo { get; init; } = null!;
    public string NitEmisor { get; init; } = null!;
    public string TipoDte { get; init; } = null!;
    public string CodigoGeneracion { get; init; } = null!;
    public string Token { get; init; } = null!;
}

public sealed class HaciendaConsultaDteResult
{
    public bool Success { get; init; }
    public int? CodigoHttp { get; init; }
    public string? Ambiente { get; init; }
    public string? Estado { get; init; }
    public string? CodigoGeneracion { get; init; }
    public string? SelloRecibido { get; init; }
    public DateTime? FhProcesamiento { get; init; }
    public string? CodigoMsg { get; init; }
    public string? DescripcionMsg { get; init; }
    public string? Raw { get; init; }
}

public interface IHaciendaConsultaDteClient
{
    Task<HaciendaConsultaDteResult> ConsultarAsync(HaciendaConsultaDteRequest request, CancellationToken ct = default);
}
