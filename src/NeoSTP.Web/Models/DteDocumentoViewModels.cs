using System.ComponentModel.DataAnnotations;

namespace NeoSTP.Web.Models;

public class CreateDteDocumentoViewModel
{
    [Required] public string TipoDteCodigo { get; set; } = "01";

    public int? ClienteId { get; set; }

    /// <summary>Receptor manual cuando no hay ClienteId.</summary>
    public string? ReceptorTipoDocumento { get; set; }
    public string? ReceptorNumeroDocumento { get; set; }
    public string? ReceptorNrc { get; set; }
    public string? ReceptorNombre { get; set; }
    public string? ReceptorTipoContribuyente { get; set; }
    public string? ReceptorCodigoActividad { get; set; }
    public string? ReceptorActividadEconomica { get; set; }
    public string? ReceptorDepartamentoCodigo { get; set; }
    public string? ReceptorMunicipioCodigo { get; set; }
    public string? ReceptorDireccion { get; set; }
    public string? ReceptorCorreo { get; set; }
    public string? ReceptorTelefono { get; set; }

    [Required] public string CondicionOperacionCodigo { get; set; } = "1";
    public string? FormaPagoCodigo { get; set; }
    public int? PlazoDias { get; set; }

    // Documento relacionado (para NC/ND)
    public string? NumeroDocumentoRelacionado { get; set; }
    public string? TipoDteRelacionado { get; set; }
    public string? TipoGeneracionRelacionado { get; set; } = "2";

    public string? Observaciones { get; set; }

    public List<DteLineaViewModel> Lineas { get; set; } = new();

    public bool GenerarInmediato { get; set; } = true;
}

public class DteLineaViewModel
{
    public int? ProductoId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string UnidadMedidaCodigo { get; set; } = "UNIDAD";
    public int TipoItem { get; set; } = 1;
    public decimal Cantidad { get; set; } = 1;
    public decimal PrecioUnitario { get; set; }
    public decimal MontoDescuento { get; set; }
    public string Clasificacion { get; set; } = "GRAVADA";
    public bool NoGravado { get; set; }
}
