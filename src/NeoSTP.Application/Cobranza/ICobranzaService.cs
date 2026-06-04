using NeoSTP.Application.Cobranza.Dtos;
using NeoSTP.Application.Common;

namespace NeoSTP.Application.Cobranza;

/// <summary>
/// Cuentas por cobrar / cobranza. Deriva saldos de DTE (factura/CCF a crédito) PROCESADO
/// menos los pagos CONFIRMADOS. Registra pagos. Todo aislado por EmpresaId.
/// </summary>
public interface ICobranzaService
{
    /// <summary>Resumen de cartera (para el dashboard): pendiente, vencido, # clientes con deuda.</summary>
    Task<CobranzaResumenDto> GetResumenAsync(int empresaId, CancellationToken ct = default);

    /// <summary>Facturas con saldo pendiente (pendientes/vencidas), filtrables y paginadas.</summary>
    Task<Result<PagedResult<CobroPendienteDto>>> GetPendientesAsync(int empresaId, CobranzaQuery query, CancellationToken ct = default);

    /// <summary>Saldo consolidado de un cliente + sus facturas pendientes.</summary>
    Task<Result<SaldoClienteDto>> GetSaldoClienteAsync(int empresaId, int clienteId, CancellationToken ct = default);

    /// <summary>Pagos registrados contra un DTE (historial).</summary>
    Task<Result<IReadOnlyList<PagoClienteDto>>> GetPagosAsync(int empresaId, int dteDocumentoId, CancellationToken ct = default);

    /// <summary>Registra un pago contra un DTE. Valida monto &gt; 0 y &le; saldo actual.</summary>
    Task<Result<PagoClienteDto>> RegistrarPagoAsync(int empresaId, int dteDocumentoId, RegistrarPagoRequest request, string? actor, CancellationToken ct = default);

    /// <summary>Anula un pago (deja de contar para el saldo).</summary>
    Task<Result> AnularPagoAsync(int empresaId, int pagoId, string? actor, CancellationToken ct = default);

    /// <summary>Confirma un pago que estaba PENDIENTE_REVISION.</summary>
    Task<Result> ConfirmarPagoAsync(int empresaId, int pagoId, string? actor, CancellationToken ct = default);
}
