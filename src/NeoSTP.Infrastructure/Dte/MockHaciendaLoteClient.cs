using NeoSTP.Application.Dte.Abstractions;

namespace NeoSTP.Infrastructure.Dte;

/// <summary>Mock del cliente de envío de lote de contingencia para desarrollo sin red.</summary>
public class MockHaciendaLoteClient : IHaciendaLoteClient
{
    public Task<HaciendaLoteResult> EnviarLoteAsync(HaciendaLoteRequest request, CancellationToken ct = default)
        => Task.FromResult(new HaciendaLoteResult
        {
            Success = true,
            CodigoHttp = 200,
            CodigoLote = $"MOCK-LOTE-{Guid.NewGuid():N}"[..24].ToUpperInvariant(),
            SelloRecibido = $"MOCK-SELLO-LOTE-{Guid.NewGuid():N}"[..40].ToUpperInvariant(),
            Estado = "RECIBIDO",
            CodigoMsg = "001",
            DescripcionMsg = "RECIBIDO (mock)",
        });
}
