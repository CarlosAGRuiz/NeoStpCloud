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
    public string? Direccion { get; set; }
    public string? Telefono { get; set; }
    public string? Correo { get; set; }
    public string? LogoUrl { get; set; }
    public string EstadoCodigo { get; set; } = EstadoCodes.Activo;

    public ICollection<Sucursal> Sucursales { get; set; } = new List<Sucursal>();
}
