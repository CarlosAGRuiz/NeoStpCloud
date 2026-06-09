using NeoSTP.Application.Common;
using NeoSTP.Application.Pos.Dtos;

namespace NeoSTP.Application.Pos;

/// <summary>
/// NEOPOS — punto de venta. Registra ventas (tickets de cobro no fiscales, promovibles a DTE),
/// genera el ticket para impresión/PDF/correo y resume el día. Aislado por empresa.
/// </summary>
public interface IPosService
{
    Task<Result<PagedResult<VentaPosDto>>> ListAsync(int empresaId, DateOnly? desde, DateOnly? hasta, PagedQuery query, CancellationToken ct = default);
    Task<Result<VentaPosDetalleDto>> GetAsync(int empresaId, int id, CancellationToken ct = default);
    Task<Result<VentaPosDetalleDto>> CrearVentaAsync(int empresaId, CrearVentaRequest request, string? actor, CancellationToken ct = default);
    Task<Result> AnularAsync(int empresaId, int id, string? actor, CancellationToken ct = default);

    /// <summary>Modelo del ticket para PDF/impresión (incluye datos de empresa y logo).</summary>
    Task<Result<TicketModel>> GetTicketAsync(int empresaId, int id, CancellationToken ct = default);

    /// <summary>Envía el ticket en PDF al correo indicado.</summary>
    Task<Result> EnviarTicketCorreoAsync(int empresaId, int id, string email, string? actor, CancellationToken ct = default);

    Task<Result<PosResumenDiaDto>> ResumenDiaAsync(int empresaId, DateOnly fecha, CancellationToken ct = default);
}

/// <summary>Genera el PDF de un ticket de venta (formato térmico 58/80mm).</summary>
public interface ITicketPdfService
{
    byte[] GenerarTicket(TicketModel modelo);
}
