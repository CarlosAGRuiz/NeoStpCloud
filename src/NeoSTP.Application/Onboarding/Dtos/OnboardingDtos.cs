namespace NeoSTP.Application.Onboarding.Dtos;

/// <summary>
/// Estado de activación (onboarding) de una empresa: pasos derivados de datos reales
/// para llevar al cliente del registro al primer DTE sin soporte.
/// </summary>
public class OnboardingEstadoDto
{
    public int EmpresaId { get; set; }
    public string? EmpresaNombre { get; set; }

    public IReadOnlyList<OnboardingPasoDto> Pasos { get; set; } = Array.Empty<OnboardingPasoDto>();

    public int PasosTotal => Pasos.Count;
    public int PasosCompletados => Pasos.Count(p => p.Completado);

    /// <summary>Porcentaje de avance (0-100). 100 si no hay pasos.</summary>
    public int PorcentajeCompletado =>
        PasosTotal == 0 ? 100 : (int)Math.Round(PasosCompletados * 100.0 / PasosTotal);

    /// <summary>True cuando todos los pasos están completados.</summary>
    public bool Completo => PasosTotal > 0 && PasosCompletados == PasosTotal;

    /// <summary>Primer paso aún pendiente (para el CTA principal). Null si está completo.</summary>
    public OnboardingPasoDto? SiguientePaso => Pasos.FirstOrDefault(p => !p.Completado);
}

/// <summary>Un paso del checklist de activación.</summary>
public class OnboardingPasoDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    /// <summary>Nombre del icono de Material Symbols.</summary>
    public string Icono { get; set; } = string.Empty;
    public bool Completado { get; set; }
    /// <summary>Texto del botón de acción para completar el paso.</summary>
    public string AccionTexto { get; set; } = string.Empty;
    /// <summary>URL relativa de la acción que completa el paso.</summary>
    public string AccionUrl { get; set; } = string.Empty;
}
