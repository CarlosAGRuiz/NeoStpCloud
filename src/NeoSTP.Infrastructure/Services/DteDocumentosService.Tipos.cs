using NeoSTP.Application.Dte.Dtos;
using NeoSTP.Infrastructure.Dte;
using Microsoft.EntityFrameworkCore;

namespace NeoSTP.Infrastructure.Services;

public partial class DteDocumentosService
{
    public Task<IReadOnlyList<TipoDteDisponibleDto>> GetTiposDisponiblesAsync(int empresaId, CancellationToken ct = default)
        => DteTypeAuthorization.ReadAsync(_db, empresaId, ct);

    public async Task<IReadOnlyList<TipoDteDisponibleDto>> GetTiposConsultaAsync(int empresaId, CancellationToken ct = default)
    {
        var available = await GetTiposDisponiblesAsync(empresaId, ct);
        var historical = await _db.DteDocumentos.AsNoTracking().Where(d => d.EmpresaId == empresaId)
            .Select(d => d.TipoDteCodigo).Distinct().ToListAsync(ct);
        return available.Concat(historical.Where(code => !available.Any(t => t.Codigo == code))
            .Select(code => new TipoDteDisponibleDto(code, "Histórico"))).OrderBy(t => t.Codigo).ToArray();
    }
}
