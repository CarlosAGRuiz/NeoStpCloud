using Microsoft.Extensions.Logging;
using NeoSTP.Application.Dte.Abstractions;

namespace NeoSTP.Infrastructure.Dte;

/// <summary>
/// Mock del cliente Hacienda. Simula respuestas para permitir desarrollo
/// del flujo de Configuración DTE sin credenciales reales.
///
/// Reglas del mock:
///   - usuario o password vacíos          -> 400 USUARIO_OBLIGATORIO
///   - usuario contiene "invalid"          -> 401 CREDENCIALES_INVALIDAS
///   - usuario contiene "bloqueado"        -> 403 USUARIO_BLOQUEADO
///   - cualquier otro caso                 -> 200 con token simulado de 8h
/// </summary>
public class MockHaciendaAuthClient : IHaciendaAuthClient
{
    private readonly ILogger<MockHaciendaAuthClient> _logger;

    public MockHaciendaAuthClient(ILogger<MockHaciendaAuthClient> logger)
    {
        _logger = logger;
    }

    public Task<HaciendaAuthResult> AutenticarAsync(string usuario, string password, string ambienteCodigo, CancellationToken ct = default)
    {
        _logger.LogInformation("MockHaciendaAuthClient: autenticando usuario {Usuario} en ambiente {Ambiente}", usuario, ambienteCodigo);

        HaciendaAuthResult result;
        if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(password))
        {
            result = new HaciendaAuthResult
            {
                Success = false, CodigoHttp = 400,
                Mensaje = "USUARIO_OBLIGATORIO",
                Detalle = "Usuario y password son obligatorios.",
            };
        }
        else if (usuario.Contains("invalid", StringComparison.OrdinalIgnoreCase))
        {
            result = new HaciendaAuthResult
            {
                Success = false, CodigoHttp = 401,
                Mensaje = "CREDENCIALES_INVALIDAS",
                Detalle = "Las credenciales proporcionadas no son válidas (mock).",
            };
        }
        else if (usuario.Contains("bloqueado", StringComparison.OrdinalIgnoreCase))
        {
            result = new HaciendaAuthResult
            {
                Success = false, CodigoHttp = 403,
                Mensaje = "USUARIO_BLOQUEADO",
                Detalle = "Usuario bloqueado por Hacienda (mock).",
            };
        }
        else
        {
            var token = $"MOCK-{Guid.NewGuid():N}";
            result = new HaciendaAuthResult
            {
                Success = true, CodigoHttp = 200,
                Token = token,
                ExpiresAt = DateTime.UtcNow.AddHours(8),
                Mensaje = "OK",
                Detalle = $"Autenticación simulada exitosa para ambiente {ambienteCodigo}.",
            };
        }

        return Task.FromResult(result);
    }
}
