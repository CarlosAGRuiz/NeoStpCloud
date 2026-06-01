using NeoSTP.Application.Common;

namespace NeoSTP.Application.Dte.Eventos;

/// <summary>
/// Sprint 15.3 — Renderiza la representación gráfica (PDF) de un evento DTE.
///
/// El servicio recibe el id del evento + el empresaId del solicitante, valida
/// que el evento pertenece a la empresa, lo carga con sus relaciones y
/// devuelve los bytes del PDF.
/// </summary>
public interface IDteEventoPdfService
{
    Task<Result<DteEventoPdfDto>> GenerarAsync(int empresaId, int eventoId, CancellationToken ct = default);
}

public class DteEventoPdfDto
{
    public string FileName { get; set; } = string.Empty;
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = "application/pdf";
}
