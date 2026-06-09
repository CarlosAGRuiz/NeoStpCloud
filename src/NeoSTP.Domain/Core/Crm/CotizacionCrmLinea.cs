using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Productos;

namespace NeoSTP.Domain.Core.Crm;

public class CotizacionCrmLinea : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    public int CotizacionCrmId { get; set; }
    public CotizacionCrm Cotizacion { get; set; } = null!;

    public int NumeroLinea { get; set; }

    public int? ProductoId { get; set; }
    public Producto? Producto { get; set; }

    public int TipoItem { get; set; } = 1;
    public string? Codigo { get; set; }
    public string Descripcion { get; set; } = null!;
    public string UnidadMedidaCodigo { get; set; } = "59";

    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal PorcentajeDescuento { get; set; }
    public decimal MontoDescuento { get; set; }

    public decimal VentaNoSujeta { get; set; }
    public decimal VentaExenta { get; set; }
    public decimal VentaGravada { get; set; }
    public decimal IvaItem { get; set; }
    public decimal TotalLinea { get; set; }
}
