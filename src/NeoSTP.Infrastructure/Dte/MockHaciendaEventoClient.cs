using NeoSTP.Application.Dte.Abstractions;

namespace NeoSTP.Infrastructure.Dte;

/// <summary>Mock del cliente genérico de eventos para desarrollo sin red.</summary>
public class MockHaciendaEventoClient : IHaciendaEventoClient
{
    public Task<EventoResult> PostAsync(string endpointPath, string bodyJson, string token, string ambienteCodigo, CancellationToken ct = default)
        => Task.FromResult(new EventoResult
        {
            Success = true,
            CodigoHttp = 200,
            Estado = "PROCESADO",
            SelloRecibido = $"MOCK-EVT-{Guid.NewGuid():N}"[..40].ToUpperInvariant(),
            CodigoMsg = "001",
            DescripcionMsg = "RECIBIDO (mock)",
        });
}
