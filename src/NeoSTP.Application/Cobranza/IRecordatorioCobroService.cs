using System.ComponentModel.DataAnnotations;
using NeoSTP.Application.Cobranza.Dtos;
using NeoSTP.Application.Common;

namespace NeoSTP.Application.Cobranza;

/// <summary>
/// Envia recordatorios de cobro para facturas vencidas. Usa canales pluggables y registra
/// un log idempotente por documento/canal/dia. La configuración por empresa define reglas,
/// canales, frecuencia y plantilla; el worker solo procesa empresas con configuración activa.
/// </summary>
public interface IRecordatorioCobroService
{
    Task<Result<RecordatorioCobroResumenDto>> EjecutarAsync(
        int empresaId,
        EjecutarRecordatoriosCobroRequest request,
        string? actor,
        CancellationToken ct = default);

    /// <summary>Ejecuta usando la configuración guardada de la empresa. Falla con RECORDATORIOS_DESHABILITADOS si no hay config activa.</summary>
    Task<Result<RecordatorioCobroResumenDto>> EjecutarSegunConfiguracionAsync(int empresaId, string? actor, CancellationToken ct = default);

    Task<Result<ConfigRecordatorioCobroDto>> GetConfiguracionAsync(int empresaId, CancellationToken ct = default);
    Task<Result<ConfigRecordatorioCobroDto>> GuardarConfiguracionAsync(int empresaId, GuardarConfigRecordatorioRequest request, string? actor, CancellationToken ct = default);
}

public class ConfigRecordatorioCobroDto
{
    public bool Activo { get; set; }
    public int DiasVencidoMinimo { get; set; } = 1;
    public int FrecuenciaDias { get; set; } = 3;
    public int MaximoPorEjecucion { get; set; } = 50;
    public bool EnviarEmail { get; set; } = true;
    public bool EnviarWhatsApp { get; set; }
    public string? AsuntoPlantilla { get; set; }
    public string? MensajePlantilla { get; set; }
}

public class GuardarConfigRecordatorioRequest
{
    public bool Activo { get; set; }
    [Range(0, 365)] public int DiasVencidoMinimo { get; set; } = 1;
    [Range(1, 60)] public int FrecuenciaDias { get; set; } = 3;
    [Range(1, 500)] public int MaximoPorEjecucion { get; set; } = 50;
    public bool EnviarEmail { get; set; } = true;
    public bool EnviarWhatsApp { get; set; }
    [StringLength(160)] public string? AsuntoPlantilla { get; set; }
    [StringLength(1000)] public string? MensajePlantilla { get; set; }
}
