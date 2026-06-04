using NeoSTP.Application.Common;

namespace NeoSTP.Application.Empresas;

/// <summary>Estado del branding (logo y firma) de una empresa.</summary>
public sealed record BrandingDto
{
    public bool TieneLogo { get; init; }
    public string? LogoContentType { get; init; }
    public bool TieneFirma { get; init; }
    public string? FirmaContentType { get; init; }
    public string? FirmaTexto { get; init; }
}

/// <summary>Imagen binaria (logo o firma) para servir o embeber.</summary>
public sealed record BrandingImagen(byte[] Contenido, string ContentType);

/// <summary>
/// Gestión del branding de una empresa: logo y firma usados en la representación
/// gráfica del DTE (PDF) y en el correo de envío. Aislado por EmpresaId.
/// </summary>
public interface IBrandingService
{
    Task<BrandingDto> GetAsync(int empresaId, CancellationToken ct = default);
    Task<BrandingImagen?> GetLogoAsync(int empresaId, CancellationToken ct = default);
    Task<BrandingImagen?> GetFirmaAsync(int empresaId, CancellationToken ct = default);

    Task<Result> GuardarLogoAsync(int empresaId, byte[] contenido, string contentType, string fileName, string? actor, CancellationToken ct = default);
    Task<Result> GuardarFirmaAsync(int empresaId, byte[] contenido, string contentType, string fileName, string? actor, CancellationToken ct = default);
    Task<Result> GuardarFirmaTextoAsync(int empresaId, string? texto, string? actor, CancellationToken ct = default);

    Task<Result> EliminarLogoAsync(int empresaId, string? actor, CancellationToken ct = default);
    Task<Result> EliminarFirmaAsync(int empresaId, string? actor, CancellationToken ct = default);
}
