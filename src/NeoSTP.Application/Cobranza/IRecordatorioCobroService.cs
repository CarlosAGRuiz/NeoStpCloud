using NeoSTP.Application.Cobranza.Dtos;
using NeoSTP.Application.Common;

namespace NeoSTP.Application.Cobranza;

/// <summary>
/// Envia recordatorios de cobro para facturas vencidas. Usa canales pluggables y registra
/// un log idempotente por documento/canal/dia.
/// </summary>
public interface IRecordatorioCobroService
{
    Task<Result<RecordatorioCobroResumenDto>> EjecutarAsync(
        int empresaId,
        EjecutarRecordatoriosCobroRequest request,
        string? actor,
        CancellationToken ct = default);
}
