using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Clientes;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Domain.Core.Cobranza;

/// <summary>
/// Log idempotente de recordatorios salientes de cobranza. Permite auditar y evitar duplicados
/// diarios por documento/canal.
/// </summary>
public class RecordatorioCobro : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    public int DteDocumentoId { get; set; }
    public DteDocumento DteDocumento { get; set; } = null!;

    public int? ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    public DateOnly FechaRecordatorio { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public string Canal { get; set; } = RecordatorioCanales.Email;
    public string Destinatario { get; set; } = string.Empty;
    public string EstadoCodigo { get; set; } = RecordatorioEstados.Enviado;
    public string? Motivo { get; set; }
    public string? MessageId { get; set; }
    public decimal Saldo { get; set; }
    public int DiasVencido { get; set; }
}

/// <summary>
/// Configuración por empresa de los recordatorios automáticos de cobro (V2-D3):
/// reglas (días vencidos, frecuencia, máximo), canales y plantilla del mensaje.
/// El worker solo procesa empresas con configuración activa.
/// </summary>
public class ConfigRecordatorioCobro : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    public bool Activo { get; set; }

    /// <summary>Días de vencimiento mínimos para recordar (0 = desde el primer día vencida).</summary>
    public int DiasVencidoMinimo { get; set; } = 1;

    /// <summary>Cada cuántos días se repite el recordatorio de una misma factura (1 = diario).</summary>
    public int FrecuenciaDias { get; set; } = 3;

    public int MaximoPorEjecucion { get; set; } = 50;

    public bool EnviarEmail { get; set; } = true;
    public bool EnviarWhatsApp { get; set; }

    /// <summary>Plantilla del asunto. Placeholders: {numeroControl} {cliente} {saldo} {diasVencido} {vencimiento}.</summary>
    public string? AsuntoPlantilla { get; set; }

    /// <summary>Plantilla del cuerpo (texto). Mismos placeholders; null = mensaje por defecto.</summary>
    public string? MensajePlantilla { get; set; }
}

public static class RecordatorioCanales
{
    public const string Email = "EMAIL";
    public const string WhatsApp = "WHATSAPP";
}

public static class RecordatorioEstados
{
    public const string Enviado = "ENVIADO";
    public const string Omitido = "OMITIDO";
    public const string Fallido = "FALLIDO";
}
