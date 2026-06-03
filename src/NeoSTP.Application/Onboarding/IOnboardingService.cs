using NeoSTP.Application.Onboarding.Dtos;

namespace NeoSTP.Application.Onboarding;

/// <summary>
/// Calcula el estado de activación (onboarding) de una empresa a partir de datos reales.
/// No persiste estado: cada paso se deriva de la base (perfil, config DTE, catálogo, DTE).
/// </summary>
public interface IOnboardingService
{
    Task<OnboardingEstadoDto> GetEstadoAsync(int empresaId, CancellationToken ct = default);
}

/// <summary>Códigos estables de los pasos de onboarding.</summary>
public static class OnboardingPasos
{
    public const string PerfilEmpresa = "PERFIL_EMPRESA";
    public const string ConfigDte = "CONFIG_DTE";
    public const string Certificado = "CERTIFICADO";
    public const string CatalogoBase = "CATALOGO_BASE";
    public const string PrimerDte = "PRIMER_DTE";
}
