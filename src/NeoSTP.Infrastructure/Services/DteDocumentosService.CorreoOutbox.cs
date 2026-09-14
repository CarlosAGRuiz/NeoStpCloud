using Microsoft.EntityFrameworkCore;
using NeoSTP.Domain.Core.Dte;

namespace NeoSTP.Infrastructure.Services;

public partial class DteDocumentosService
{
    internal async Task ProgramarCorreosAutomaticosAsync(
        DteDocumento doc,
        string? actor,
        CancellationToken ct = default)
    {
        if (doc.EstadoCodigo != DteEstadoCodigos.Procesado
            || string.IsNullOrWhiteSpace(doc.SelloRecibido))
            return;

        if (DteCorreoEntregaService.EsDestinoValido(doc.ReceptorCorreo))
        {
            await DteCorreoEntregaService.StagePendingAsync(
                _db,
                doc.EmpresaId,
                doc.Id,
                NeoSTP.Application.Dte.DteCorreoFinalidades.Receptor,
                doc.ReceptorCorreo!,
                automatico: true,
                $"DTE:{doc.Id}:AUTO:RECEPTOR",
                actor,
                ct);
        }

        var correoEmisor = await _db.Empresas.AsNoTracking()
            .Where(x => x.Id == doc.EmpresaId)
            .Select(x => x.Correo)
            .FirstOrDefaultAsync(ct);
        if (DteCorreoEntregaService.EsDestinoValido(correoEmisor))
        {
            await DteCorreoEntregaService.StagePendingAsync(
                _db,
                doc.EmpresaId,
                doc.Id,
                NeoSTP.Application.Dte.DteCorreoFinalidades.Emisor,
                correoEmisor!,
                automatico: true,
                $"DTE:{doc.Id}:AUTO:EMISOR",
                actor,
                ct);
        }
    }
}
