namespace NeoSTP.Application.Dte.Abstractions;

// ─── Envío de Lote (MOMENTO 3 — POST /fesv/recepcionlote) ────────────────────

/// <summary>
/// Solicitud de envío de lote de DTE en contingencia a Hacienda.
/// Endpoint: POST {base}/fesv/recepcionlote
/// </summary>
public class HaciendaLoteRequest
{
    /// <summary>"00" pruebas / "01" producción.</summary>
    public string Ambiente { get; set; } = "00";
    /// <summary>NIT del emisor.</summary>
    public string Nit { get; set; } = null!;
    /// <summary>Sello del evento de contingencia que autoriza este lote.</summary>
    public string SelloEvento { get; set; } = null!;
    /// <summary>Lista de JWS firmados de los DTE incluidos en el lote.</summary>
    public IReadOnlyList<HaciendaLoteItem> Items { get; set; } = Array.Empty<HaciendaLoteItem>();
    /// <summary>Token Bearer activo de Hacienda (sin el prefijo "Bearer ").</summary>
    public string Token { get; set; } = null!;
    public string AmbienteCodigo { get; set; } = "PRUEBAS";
}

public class HaciendaLoteItem
{
    public string TipoDte { get; set; } = null!;
    public string CodigoGeneracion { get; set; } = null!;
    /// <summary>JWS firmado del DTE.</summary>
    public string Documento { get; set; } = null!;
}

/// <summary>Resultado del envío del lote a Hacienda.</summary>
public class HaciendaLoteResult
{
    public bool Success { get; set; }
    public int? CodigoHttp { get; set; }
    /// <summary>Código de lote asignado por Hacienda (codigoLote).</summary>
    public string? CodigoLote { get; set; }
    public string? SelloRecibido { get; set; }
    public string? Estado { get; set; }
    public string? CodigoMsg { get; set; }
    public string? DescripcionMsg { get; set; }
    public IReadOnlyList<string> Observaciones { get; set; } = Array.Empty<string>();
    public string? Raw { get; set; }
}

/// <summary>Cliente de envío de lote de DTE en contingencia (POST /fesv/recepcionlote).</summary>
public interface IHaciendaLoteClient
{
    Task<HaciendaLoteResult> EnviarLoteAsync(HaciendaLoteRequest request, CancellationToken ct = default);
}

// ─── Consulta de Lote (GET /fesv/recepcion/consultadtelote/{codigoLote}) ──────

/// <summary>Solicitud de consulta del estado de un lote.</summary>
public class HaciendaConsultaLoteRequest
{
    /// <summary>Código de lote devuelto por Hacienda al enviar el lote.</summary>
    public string CodigoLote { get; set; } = null!;
    public string Token { get; set; } = null!;
    public string AmbienteCodigo { get; set; } = "PRUEBAS";
}

/// <summary>Resultado de la consulta de estado de un lote.</summary>
public class HaciendaConsultaLoteResult
{
    public bool Success { get; set; }
    public int? CodigoHttp { get; set; }
    public string? Estado { get; set; }
    public string? CodigoMsg { get; set; }
    public string? DescripcionMsg { get; set; }
    public IReadOnlyList<HaciendaConsultaLoteItemResult> Items { get; set; } = Array.Empty<HaciendaConsultaLoteItemResult>();
    public string? Raw { get; set; }
}

/// <summary>Estado individual de un DTE dentro del lote consultado.</summary>
public class HaciendaConsultaLoteItemResult
{
    public string CodigoGeneracion { get; set; } = null!;
    public string? SelloRecibido { get; set; }
    public string? Estado { get; set; }
    public string? CodigoMsg { get; set; }
    public string? DescripcionMsg { get; set; }
}

/// <summary>Cliente de consulta de lote de contingencia (GET /fesv/recepcion/consultadtelote/{codigoLote}).</summary>
public interface IHaciendaConsultaLoteClient
{
    Task<HaciendaConsultaLoteResult> ConsultarLoteAsync(HaciendaConsultaLoteRequest request, CancellationToken ct = default);
}
