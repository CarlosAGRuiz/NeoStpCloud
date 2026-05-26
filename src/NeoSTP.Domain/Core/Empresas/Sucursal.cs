using NeoSTP.Domain.Common;

namespace NeoSTP.Domain.Core.Empresas;

public class Sucursal : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string? CodigoEstablecimientoMh { get; set; }
    public string? TipoEstablecimientoCodigo { get; set; }
    public string? Direccion { get; set; }
    public string? Telefono { get; set; }
    public string? Departamento { get; set; }
    public string? Municipio { get; set; }
    public string EstadoCodigo { get; set; } = EstadoCodes.Activo;

    public ICollection<PuntoVenta> PuntosVenta { get; set; } = new List<PuntoVenta>();
}
