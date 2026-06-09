using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Clientes;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Domain.Core.Crm;

public class CotizacionCrm : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    public int? OportunidadCrmId { get; set; }
    public OportunidadCrm? Oportunidad { get; set; }

    public int? ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    public int? ContactoCrmId { get; set; }
    public ContactoCrm? Contacto { get; set; }

    public int? DteDocumentoId { get; set; }
    public DteDocumento? DteDocumento { get; set; }

    public string Numero { get; set; } = null!;
    public string Titulo { get; set; } = null!;
    public DateOnly FechaEmision { get; set; }
    public DateOnly? FechaValidez { get; set; }
    public string EstadoCodigo { get; set; } = CotizacionCrmEstados.Borrador;
    public string MonedaCodigo { get; set; } = "USD";

    public decimal SubTotal { get; set; }
    public decimal DescuentoTotal { get; set; }
    public decimal IvaTotal { get; set; }
    public decimal Total { get; set; }

    public string? Observaciones { get; set; }
    public string? Terminos { get; set; }

    public ICollection<CotizacionCrmLinea> Lineas { get; set; } = new List<CotizacionCrmLinea>();
}

public static class CotizacionCrmEstados
{
    public const string Borrador = "BORRADOR";
    public const string Enviada = "ENVIADA";
    public const string Aceptada = "ACEPTADA";
    public const string Rechazada = "RECHAZADA";
    public const string Convertida = "CONVERTIDA";
    public const string Anulada = "ANULADA";

    public static readonly string[] All =
    [
        Borrador,
        Enviada,
        Aceptada,
        Rechazada,
        Convertida,
        Anulada,
    ];
}
