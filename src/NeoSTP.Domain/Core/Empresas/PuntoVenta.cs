using NeoSTP.Domain.Common;

namespace NeoSTP.Domain.Core.Empresas;

public class PuntoVenta : AuditableEntity
{
    public int SucursalId { get; set; }
    public Sucursal Sucursal { get; set; } = null!;

    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string? CodigoPuntoVentaMh { get; set; }
    public string EstadoCodigo { get; set; } = EstadoCodes.Activo;
}
