namespace NeoSTP.Application.Dte.Diagnostico.Dtos;

public record ErrorCatalogoDto(
    string Codigo,
    string Tipo,
    string MensajeTecnico,
    string Descripcion,
    string CausaProbable,
    string AccionSugerida,
    string Severidad
);

public record ErrorOcurrenciaListItemDto(
    int Id,
    string CodigoError,
    string Mensaje,
    string Fuente,
    int? DteDocumentoId,
    int? DteEventoId,
    DateTime OcurrioAt,
    bool Resuelta,
    // Del catálogo (null si código no existe en catálogo)
    string? Descripcion,
    string? CausaProbable,
    string? AccionSugerida,
    string? Severidad
);

public record DiagnosticoDocumentoDto(
    int DteDocumentoId,
    string CodigoGeneracion,
    string NumeroControl,
    string TipoDteCodigo,
    string EstadoCodigo,
    string? JsonEnviado,
    string? RespuestaMhJson,
    IReadOnlyList<ErrorOcurrenciaListItemDto> Errores
);

public record DiagnosticoEventoDto(
    int DteEventoId,
    string TipoEventoCodigo,
    string EstadoCodigo,
    string? RespuestaMhJson,
    IReadOnlyList<ErrorOcurrenciaListItemDto> Errores
);

public record DiagnosticoResumenDto(
    int TotalErrores,
    int ErroresNoResueltos,
    int ErroresHoy,
    IReadOnlyList<ErrorPorCodigoDto> TopCodigos
);

public record ErrorPorCodigoDto(string CodigoError, int Cantidad, string? Descripcion);

public record DiagnosticoFiltroRequest(
    string? CodigoError,
    string? Fuente,
    DateTime? Desde,
    DateTime? Hasta,
    bool? SoloNoResueltas,
    int Page,
    int PageSize
);

public record MarcarResueltaRequest(int OcurrenciaId);
