using Microsoft.EntityFrameworkCore;
using NeoSTP.Domain.Core.Dte;

namespace NeoSTP.Infrastructure.Services;

public partial class DteDocumentosService
{
    // Compatibilidad interna para pruebas y recuperación controlada. El camino fiscal normal
    // programa las filas antes de SaveChanges para confirmar DTE + outbox atómicamente.
    internal async Task EnviarCorreoAutomaticoAsync(int empresaId, int id, string? actor)
    {
        var doc = await _db.DteDocumentos.FirstOrDefaultAsync(d =>
            d.EmpresaId == empresaId && d.Id == id
            && d.EstadoCodigo == DteEstadoCodigos.Procesado
            && d.SelloRecibido != null && d.SelloRecibido != "");
        if (doc is null) return;
        await ProgramarCorreosAutomaticosAsync(doc, actor);
        await _db.SaveChangesAsync();
    }
}
