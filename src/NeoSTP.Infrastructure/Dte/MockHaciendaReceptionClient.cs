using Microsoft.Extensions.Logging;
using NeoSTP.Application.Dte.Abstractions;

namespace NeoSTP.Infrastructure.Dte;

/// <summary>
/// Mock del cliente de recepción de Hacienda.
///
/// Reglas:
///   - documento vacío         -> 400 ERROR
///   - token vacío             -> 401 NO_AUTORIZADO
///   - codigo termina en "000" -> 200 PROCESADO con sello
///   - codigo contiene "RECH"  -> 200 RECHAZADO
///   - codigo contiene "CONT"  -> 200 CONTINGENCIA
///   - cualquier otro caso     -> 200 PROCESADO con sello
/// </summary>
public class MockHaciendaReceptionClient : IHaciendaReceptionClient
{
    private readonly ILogger<MockHaciendaReceptionClient> _logger;

    public MockHaciendaReceptionClient(ILogger<MockHaciendaReceptionClient> logger)
    {
        _logger = logger;
    }

    public Task<HaciendaReceptionResult> EnviarAsync(HaciendaReceptionRequest req, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "MockHaciendaReceptionClient: recibiendo DTE {Tipo} código {Cod} en ambiente {Amb}",
            req.TipoDte, req.CodigoGeneracion, req.AmbienteCodigo);

        if (string.IsNullOrEmpty(req.Documento))
            return Task.FromResult(Fail(400, "ERROR", "DOCUMENTO_VACIO", "El campo documento es obligatorio."));

        if (string.IsNullOrEmpty(req.Token))
            return Task.FromResult(Fail(401, "ERROR", "NO_AUTORIZADO", "Token Bearer ausente o inválido."));

        var cod = req.CodigoGeneracion ?? string.Empty;

        if (cod.Contains("RECH", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new HaciendaReceptionResult
            {
                Success = true, CodigoHttp = 200,
                Estado = "RECHAZADO",
                FhProcesamiento = DateTime.UtcNow,
                ClasificaMsg = "INVALIDO",
                CodigoMsg = "001",
                DescripcionMsg = "Documento rechazado (mock).",
                Raw = "{\"estado\":\"RECHAZADO\"}",
            });
        }

        if (cod.Contains("CONT", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new HaciendaReceptionResult
            {
                Success = true, CodigoHttp = 200,
                Estado = "CONTINGENCIA",
                FhProcesamiento = DateTime.UtcNow,
                ClasificaMsg = "ADVERTENCIA",
                CodigoMsg = "002",
                DescripcionMsg = "Servicio MH no disponible — usar contingencia (mock).",
                Raw = "{\"estado\":\"CONTINGENCIA\"}",
            });
        }

        var sello = $"MOCK{Guid.NewGuid():N}".ToUpperInvariant();
        return Task.FromResult(new HaciendaReceptionResult
        {
            Success = true, CodigoHttp = 200,
            Estado = "PROCESADO",
            SelloRecibido = sello,
            FhProcesamiento = DateTime.UtcNow,
            ClasificaMsg = "OK",
            CodigoMsg = "000",
            DescripcionMsg = "DTE procesado correctamente (mock).",
            Raw = $$"""{"estado":"PROCESADO","selloRecibido":"{{sello}}"}""",
        });
    }

    private static HaciendaReceptionResult Fail(int code, string estado, string cod, string desc) => new()
    {
        Success = false, CodigoHttp = code,
        Estado = estado, ClasificaMsg = "ERROR",
        CodigoMsg = cod, DescripcionMsg = desc,
        Raw = $$"""{"estado":"{{estado}}","mensaje":"{{desc}}"}""",
    };
}
