using NeoSTP.Application.Common;
using NeoSTP.Application.Dte.Diagnostico.Dtos;

namespace NeoSTP.Application.Dte.Diagnostico;

public interface IDiagnosticoHaciendaService
{
    /// <summary>Resumen de errores de la empresa: totales, no resueltos, hoy, top códigos.</summary>
    Task<DiagnosticoResumenDto> ObtenerResumenAsync(int empresaId, CancellationToken ct = default);

    /// <summary>Lista paginada de ocurrencias con filtros opcionales.</summary>
    Task<PagedResult<ErrorOcurrenciaListItemDto>> ListarOcurrenciasAsync(
        int empresaId, DiagnosticoFiltroRequest filtro, CancellationToken ct = default);

    /// <summary>Diagnóstico completo de un documento DTE: JSON enviado, respuesta MH y errores.</summary>
    Task<DiagnosticoDocumentoDto?> ObtenerDiagnosticoDocumentoAsync(
        int empresaId, int dteDocumentoId, CancellationToken ct = default);

    /// <summary>Diagnóstico completo de un evento DTE.</summary>
    Task<DiagnosticoEventoDto?> ObtenerDiagnosticoEventoAsync(
        int empresaId, int dteEventoId, CancellationToken ct = default);

    /// <summary>Catálogo completo de errores conocidos.</summary>
    Task<IReadOnlyList<ErrorCatalogoDto>> ListarCatalogoAsync(CancellationToken ct = default);

    /// <summary>Marca una ocurrencia como resuelta.</summary>
    Task<Result<string>> MarcarResueltaAsync(
        int empresaId, int ocurrenciaId, string actor, CancellationToken ct = default);

    /// <summary>
    /// Importa errores desde los snapshots existentes (CertificacionError,
    /// DteDocumentoJson con estado RECHAZADO, DteEventoRespuestaHacienda con error).
    /// Idempotente: no duplica ocurrencias ya importadas.
    /// </summary>
    Task<int> SincronizarOcurrenciasAsync(int empresaId, CancellationToken ct = default);
}
