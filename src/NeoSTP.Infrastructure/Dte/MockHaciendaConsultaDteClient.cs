using NeoSTP.Application.Dte.Abstractions;

namespace NeoSTP.Infrastructure.Dte;

/// <summary>El modo mock nunca inventa una confirmación fiscal para una consulta real.</summary>
public sealed class MockHaciendaConsultaDteClient : IHaciendaConsultaDteClient
{
    public Task<HaciendaConsultaDteResult> ConsultarAsync(HaciendaConsultaDteRequest request, CancellationToken ct = default)
        => Task.FromResult(new HaciendaConsultaDteResult
        {
            Success = false,
            CodigoMsg = "CONSULTA_MOCK_NO_CONFIRMA",
            DescripcionMsg = "La consulta fiscal no está disponible en modo mock; el DTE conserva su estado anterior.",
        });
}
