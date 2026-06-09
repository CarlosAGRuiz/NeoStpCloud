using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Clientes;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Domain.Core.Crm;

public class ActividadCrm : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    public int? OportunidadCrmId { get; set; }
    public OportunidadCrm? Oportunidad { get; set; }

    public int? ContactoCrmId { get; set; }
    public ContactoCrm? Contacto { get; set; }

    public int? ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    public string Tipo { get; set; } = ActividadCrmTipos.Nota;
    public string Asunto { get; set; } = null!;
    public string? Descripcion { get; set; }
    public DateTime FechaProgramada { get; set; } = DateTime.UtcNow;
    public DateTime? FechaRealizada { get; set; }
    public DateTime? RecordatorioAt { get; set; }
    public string EstadoCodigo { get; set; } = ActividadCrmEstados.Pendiente;
    public string? Resultado { get; set; }
}

public static class ActividadCrmEstados
{
    public const string Pendiente = "PENDIENTE";
    public const string Realizada = "REALIZADA";
    public const string Cancelada = "CANCELADA";
}

public static class ActividadCrmTipos
{
    public const string Llamada = "LLAMADA";
    public const string Correo = "CORREO";
    public const string Visita = "VISITA";
    public const string Tarea = "TAREA";
    public const string Nota = "NOTA";

    public static readonly string[] All = [Llamada, Correo, Visita, Tarea, Nota];
}
