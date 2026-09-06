using NeoSTP.Application.Common;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Infrastructure.Dte;

namespace NeoSTP.Infrastructure.Services;

public partial class DteDocumentosService
{
    private static Result ValidarContextoDocumento(DteDocumento doc, DteConfiguracion? config)
    {
        var result = DteFiscalContext.Validar(doc.AmbienteCodigo, config);
        if (result.IsFailure) return result;
        var prefix = $"DTE-{doc.TipoDteCodigo}-{BuildBloqueEstablecimiento(config)}-";
        if (!doc.NumeroControl.StartsWith(prefix, StringComparison.Ordinal))
            return Result.Fail("El establecimiento o punto de venta actual no corresponde al número de control. Revise la configuración original del DTE; no lo renumere.", "DTE_ESTABLECIMIENTO_INCOMPATIBLE");
        return Result.Ok();
    }
}
