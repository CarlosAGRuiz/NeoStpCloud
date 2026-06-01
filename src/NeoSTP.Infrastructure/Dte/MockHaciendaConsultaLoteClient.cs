using NeoSTP.Application.Dte.Abstractions;

namespace NeoSTP.Infrastructure.Dte;

/// <summary>Mock del cliente de consulta de lote de contingencia para desarrollo sin red.</summary>
public class MockHaciendaConsultaLoteClient : IHaciendaConsultaLoteClient
{
    public Task<HaciendaConsultaLoteResult> ConsultarLoteAsync(HaciendaConsultaLoteRequest request, CancellationToken ct = default)
        => Task.FromResult(new HaciendaConsultaLoteResult
        {
            Success = true,
            CodigoHttp = 200,
            Estado = "PROCESADO",
            CodigoMsg = "001",
            DescripcionMsg = "PROCESADO (mock)",
            Items = Array.Empty<HaciendaConsultaLoteItemResult>(),
        });
}
