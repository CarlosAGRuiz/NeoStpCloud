using NeoSTP.Domain.Core.Dte;

namespace NeoSTP.Web.Models;

public sealed record DteEnvironmentBadge(string Label, bool IsProduction)
{
    public static DteEnvironmentBadge Resolve(bool haciendaHttp, string? ambiente) =>
        !haciendaHttp ? new("MOCK", false) : ambiente switch
        {
            DteAmbientes.Produccion => new("PRODUCCIÓN · HACIENDA", true),
            DteAmbientes.Pruebas => new("PRUEBAS · HACIENDA", false),
            _ => new("AMBIENTE SIN CONFIGURAR", false)
        };
}
