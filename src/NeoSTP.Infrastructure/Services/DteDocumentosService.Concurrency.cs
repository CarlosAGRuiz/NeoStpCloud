using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Common;
using NeoSTP.Application.Dte.Diagnostico;
using NeoSTP.Application.Dte.Dtos;
using NeoSTP.Domain.Core.Dte;

namespace NeoSTP.Infrastructure.Services;

public partial class DteDocumentosService
{
    private static bool RequiereConciliacion(DteDocumento doc)
        => doc.EstadoCodigo == DteEstadoCodigos.Enviado
            || (doc.EnviadoAt.HasValue && DteDiagnosticoGuia.Crear(doc.EstadoCodigo,
                doc.SelloRecibido, doc.EnviadoAt, doc.Json?.RespuestaHacienda).RequiereConsultaHacienda);

    private async Task<Result<DteDocumentoDto>> EjecutarCambioFiscalAsync(int empresaId, int id,
        Func<Task<Result<DteDocumentoDto>>> operation, CancellationToken ct)
    {
        try { return await operation(); }
        catch (DbUpdateConcurrencyException)
        {
            // Never replay generation/signing/transmission after a stale write, especially after HTTP.
            // Discard pending changes so a later SaveChanges cannot overwrite the winning attempt.
            _db.ChangeTracker.Clear();
            var actual = await GetByIdAsync(empresaId, id, ct);
            if (actual.IsFailure) return actual;
            return Result<DteDocumentoDto>.FailWithValue(actual.Value!,
                "Otro proceso modificó este DTE. Se conservó su estado actual; recárguelo y consulte Hacienda si la recepción está pendiente.",
                "DTE_CONCURRENCY_CONFLICT");
        }
    }
}
