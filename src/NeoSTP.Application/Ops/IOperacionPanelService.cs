namespace NeoSTP.Application.Ops;

/// <summary>
/// V2.5-S3 — panel operativo SaaS para SuperAdmin: métricas derivadas de la BD
/// (sin depender de un backend de telemetría) para vigilar el servicio completo.
/// </summary>
public interface IOperacionPanelService
{
    Task<PanelOperacionDto> GetPanelAsync(CancellationToken ct = default);
}

public class PanelOperacionDto
{
    public int EmpresasActivas { get; set; }
    public int EmpresasTotal { get; set; }

    public DtePeriodoDto Dte24h { get; set; } = new();
    public DtePeriodoDto Dte7d { get; set; } = new();

    /// <summary>Empresas con más rechazos de Hacienda en los últimos 7 días.</summary>
    public List<EmpresaConteoDto> TopRechazos7d { get; set; } = [];

    public int AlertasActivas { get; set; }
    public int Recordatorios7d { get; set; }
    public int PortalEnlacesActivos { get; set; }
    public int PortalAccesos7d { get; set; }
    public int ApiKeysActivas { get; set; }
}

public class DtePeriodoDto
{
    public int Total { get; set; }
    public int Procesados { get; set; }
    public int Rechazados { get; set; }
    public int Contingencia { get; set; }
}

public class EmpresaConteoDto
{
    public int EmpresaId { get; set; }
    public string Empresa { get; set; } = null!;
    public int Conteo { get; set; }
}
