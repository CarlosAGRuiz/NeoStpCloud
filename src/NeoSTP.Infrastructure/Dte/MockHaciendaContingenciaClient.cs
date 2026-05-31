using NeoSTP.Application.Dte.Abstractions;

namespace NeoSTP.Infrastructure.Dte;

/// <summary>Mock del cliente de Evento de Contingencia para desarrollo sin red.</summary>
public class MockHaciendaContingenciaClient : IHaciendaContingenciaClient
{
    public Task<ContingenciaResult> EnviarAsync(ContingenciaRequest request, CancellationToken ct = default)
        => Task.FromResult(new ContingenciaResult
        {
            Success = true,
            CodigoHttp = 200,
            Estado = "PROCESADO",
            SelloRecibido = $"MOCK-CONT-{Guid.NewGuid():N}"[..40].ToUpperInvariant(),
            CodigoMsg = "001",
            DescripcionMsg = "RECIBIDO (mock)",
        });
}
