using System.Diagnostics.Metrics;

namespace NeoSTP.Infrastructure.Diagnostics;

/// <summary>
/// V2.5-S3 — métricas de negocio (System.Diagnostics.Metrics, Meter "NeoSTP").
/// Sin listener/OTLP configurado los contadores son no-op, así que instrumentar
/// no tiene costo en el default local. Las etiquetas siempre incluyen la empresa
/// para poder segmentar por tenant en el backend de métricas.
/// </summary>
public sealed class NeoStpMetrics
{
    public const string MeterName = "NeoSTP";

    private readonly Counter<long> _dteEmitidos;
    private readonly Counter<long> _dteErroresMh;
    private readonly Counter<long> _recordatoriosEnviados;
    private readonly Counter<long> _portalAccesos;

    public NeoStpMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _dteEmitidos = meter.CreateCounter<long>("neostp.dte.emitidos", description: "DTE transmitidos a Hacienda, por estado final.");
        _dteErroresMh = meter.CreateCounter<long>("neostp.dte.errores_mh", description: "Rechazos/errores devueltos por Hacienda.");
        _recordatoriosEnviados = meter.CreateCounter<long>("neostp.cobros.recordatorios", description: "Recordatorios de cobro enviados, por canal.");
        _portalAccesos = meter.CreateCounter<long>("neostp.portal.accesos", description: "Accesos públicos al portal del receptor.");
    }

    public void DteEmitido(int empresaId, string tipoDte, string estado)
    {
        _dteEmitidos.Add(1,
            new KeyValuePair<string, object?>("empresa", empresaId),
            new KeyValuePair<string, object?>("tipo", tipoDte),
            new KeyValuePair<string, object?>("estado", estado));
        if (estado == "RECHAZADO")
            _dteErroresMh.Add(1, new KeyValuePair<string, object?>("empresa", empresaId));
    }

    public void RecordatorioEnviado(int empresaId, string canal)
        => _recordatoriosEnviados.Add(1,
            new KeyValuePair<string, object?>("empresa", empresaId),
            new KeyValuePair<string, object?>("canal", canal));

    public void PortalAcceso(int empresaId, string tipo)
        => _portalAccesos.Add(1,
            new KeyValuePair<string, object?>("empresa", empresaId),
            new KeyValuePair<string, object?>("tipo", tipo));
}
