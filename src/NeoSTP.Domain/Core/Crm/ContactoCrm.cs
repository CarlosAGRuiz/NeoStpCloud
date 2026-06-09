using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Clientes;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Domain.Core.Crm;

public class ContactoCrm : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    public int? ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    public string Nombre { get; set; } = null!;
    public string? Cargo { get; set; }
    public string? Email { get; set; }
    public string? Telefono { get; set; }
    public string Origen { get; set; } = ContactoCrmOrigenes.Manual;
    public string EstadoCodigo { get; set; } = ContactoCrmEstados.Activo;
    public string? Notas { get; set; }

    public ICollection<OportunidadCrm> Oportunidades { get; set; } = new List<OportunidadCrm>();
    public ICollection<ActividadCrm> Actividades { get; set; } = new List<ActividadCrm>();
    public ICollection<CotizacionCrm> Cotizaciones { get; set; } = new List<CotizacionCrm>();
}

public static class ContactoCrmEstados
{
    public const string Activo = "ACTIVO";
    public const string Inactivo = "INACTIVO";
}

public static class ContactoCrmOrigenes
{
    public const string Manual = "MANUAL";
    public const string Cliente = "CLIENTE";
    public const string Referido = "REFERIDO";
    public const string Web = "WEB";

    public static readonly string[] All = [Manual, Cliente, Referido, Web];
}
