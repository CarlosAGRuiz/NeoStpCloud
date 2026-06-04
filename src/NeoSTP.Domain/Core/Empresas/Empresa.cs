using NeoSTP.Domain.Common;

namespace NeoSTP.Domain.Core.Empresas;

public class Empresa : AuditableEntity
{
    public string Nit { get; set; } = null!;
    public string? Nrc { get; set; }
    public string RazonSocial { get; set; } = null!;
    public string? NombreComercial { get; set; }
    public string? CodigoActividad { get; set; }
    public string? ActividadEconomica { get; set; }
    public string? Departamento { get; set; }
    public string? Municipio { get; set; }
    /// <summary>Código de Distrito (CAT-008, división territorial 2024). Requerido para DTE v2/v4.</summary>
    public string? Distrito { get; set; }
    public string? Direccion { get; set; }
    public string? Telefono { get; set; }
    public string? Correo { get; set; }
    public string? LogoUrl { get; set; }
    public string EstadoCodigo { get; set; } = EstadoCodes.Activo;

    // ── Branding (logo y firma para la representación gráfica del DTE y el correo) ──
    /// <summary>Imagen del logo (PNG/JPG) embebida; se muestra en el PDF y el correo.</summary>
    public byte[]? LogoBlob { get; set; }
    public string? LogoContentType { get; set; }
    /// <summary>Imagen de la firma autorizada (PNG/JPG) para el pie del DTE.</summary>
    public byte[]? FirmaBlob { get; set; }
    public string? FirmaContentType { get; set; }
    /// <summary>Texto de firma al pie (ej. "Firma autorizada — Juan Pérez / Gerente"). Alternativa o complemento a la imagen.</summary>
    public string? FirmaTexto { get; set; }

    public ICollection<Sucursal> Sucursales { get; set; } = new List<Sucursal>();
}
