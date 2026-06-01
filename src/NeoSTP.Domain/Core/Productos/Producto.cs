using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Domain.Core.Productos;

/// <summary>
/// Producto o servicio facturable por la empresa.
/// </summary>
public class Producto : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    public string CodigoInterno { get; set; } = null!;
    public string? CodigoBarra { get; set; }
    public string Nombre { get; set; } = null!;
    public string? Descripcion { get; set; }

    /// <summary>BIEN o SERVICIO.</summary>
    public string TipoItem { get; set; } = "BIEN";

    /// <summary>Código MH del catálogo CAT-014 UNIDAD_MEDIDA (59=Unidad, 58=Docena, 22=Galón…).</summary>
    public string UnidadMedidaCodigo { get; set; } = "59";

    public decimal PrecioUnitario { get; set; }
    public decimal? CostoUnitario { get; set; }

    public bool AplicaIva { get; set; } = true;
    /// <summary>Código del tributo (IVA 13%, etc.). Por ahora libre.</summary>
    public string? TributoCodigo { get; set; }

    public string EstadoCodigo { get; set; } = "ACTIVO";

    public bool EsServicio => TipoItem == "SERVICIO";
}
