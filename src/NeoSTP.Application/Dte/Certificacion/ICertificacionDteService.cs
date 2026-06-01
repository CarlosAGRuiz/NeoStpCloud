using NeoSTP.Application.Common;
using NeoSTP.Application.Dte.Certificacion.Dtos;

namespace NeoSTP.Application.Dte.Certificacion;

/// <summary>
/// Sprint 14 — Operaciones de certificación DTE: consultar matriz y progreso,
/// asociar documentos a escenarios, reintentar y registrar errores Hacienda.
/// </summary>
public interface ICertificacionDteService
{
    Task<Result<CertificacionResumenDto>> GetResumenAsync(int empresaId, CancellationToken ct = default);

    Task<Result<IReadOnlyList<CertificacionMatrizProgresoDto>>> GetMatrizAsync(int empresaId, CancellationToken ct = default);

    Task<Result<IReadOnlyList<CertificacionEscenarioDto>>> GetEscenariosAsync(string tipoDteCodigo, int empresaId, CancellationToken ct = default);

    /// <summary>Crea (o reutiliza) la prueba PENDIENTE para el primer escenario disponible del tipo.</summary>
    Task<Result<CertificacionPruebaDto>> GenerarPruebaAsync(string tipoDteCodigo, int empresaId, string? actor, CancellationToken ct = default);

    /// <summary>Asocia un DteDocumento a un escenario y registra COMPLETADO si el DTE tiene sello.</summary>
    Task<Result<CertificacionPruebaDto>> MarcarCompletadoAsync(int documentoId, MarcarCompletadoRequest request, int empresaId, string? actor, CancellationToken ct = default);

    /// <summary>Marca la prueba actual del documento como ERROR y abre un nuevo intento.</summary>
    Task<Result<CertificacionPruebaDto>> ReintentarAsync(int documentoId, int empresaId, string? actor, CancellationToken ct = default);

    /// <summary>Lista de errores Hacienda registrados; filtrable por código MH.</summary>
    Task<Result<IReadOnlyList<CertificacionErrorDto>>> GetErroresAsync(int empresaId, string? codigoMh = null, CancellationToken ct = default);
}
