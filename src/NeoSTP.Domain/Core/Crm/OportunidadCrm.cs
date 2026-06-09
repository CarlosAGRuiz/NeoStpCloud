using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Clientes;
using NeoSTP.Domain.Core.Cobranza;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Domain.Core.Crm;

public class OportunidadCrm : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    public int? ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    public int? ContactoCrmId { get; set; }
    public ContactoCrm? Contacto { get; set; }

    public int EtapaPipelineCrmId { get; set; }
    public EtapaPipelineCrm Etapa { get; set; } = null!;

    public string Titulo { get; set; } = null!;
    public string? Descripcion { get; set; }
    public decimal MontoEstimado { get; set; }
    public decimal Probabilidad { get; set; }
    public DateOnly FechaApertura { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public DateOnly? FechaCierreEstimada { get; set; }
    public DateOnly? FechaCierreReal { get; set; }
    public string EstadoCodigo { get; set; } = OportunidadCrmEstados.Abierta;
    public string? MotivoPerdida { get; set; }

    public int? DteDocumentoId { get; set; }
    public DteDocumento? DteDocumento { get; set; }

    public int? CuentaCobroId { get; set; }
    public CuentaCobro? CuentaCobro { get; set; }

    public ICollection<ActividadCrm> Actividades { get; set; } = new List<ActividadCrm>();
    public ICollection<CotizacionCrm> Cotizaciones { get; set; } = new List<CotizacionCrm>();
}

public static class OportunidadCrmEstados
{
    public const string Abierta = "ABIERTA";
    public const string Ganada = "GANADA";
    public const string Perdida = "PERDIDA";
    public const string Anulada = "ANULADA";

    public static readonly string[] All = [Abierta, Ganada, Perdida, Anulada];
}
