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
