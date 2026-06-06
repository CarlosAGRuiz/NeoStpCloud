using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Common;
using NeoSTP.Application.Scan;
using NeoSTP.Application.Scan.Dtos;
using NeoSTP.Domain.Core.Scan;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

/// <summary>
/// Consulta de DTE recibidos (registro/respaldo de proveedores). Solo lectura,
/// aislado por EmpresaId. Los registros se crean desde NeoScanAI (ScanService).
/// </summary>
public class DteRecibidoService : IDteRecibidoService
{
    private readonly NeoStpDbContext _db;

    public DteRecibidoService(NeoStpDbContext db) => _db = db;

    public async Task<Result<PagedResult<DteRecibidoDto>>> ListAsync(int empresaId, DteRecibidoQuery query, CancellationToken ct = default)
    {
        var q = _db.DteDocumentosRecibidos.AsNoTracking().Where(d => d.EmpresaId == empresaId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(d => EF.Functions.Like(d.EmisorNombre, $"%{s}%")
                          || EF.Functions.Like(d.EmisorNit ?? string.Empty, $"%{s}%")
                          || EF.Functions.Like(d.NumeroControl ?? string.Empty, $"%{s}%"));
        }
        if (query.Desde is DateOnly desde) q = q.Where(d => d.Fecha >= desde);
        if (query.Hasta is DateOnly hasta) q = q.Where(d => d.Fecha <= hasta);

        var total = await q.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var items = await q.OrderByDescending(d => d.Fecha).ThenByDescending(d => d.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(d => ToDto(d)).ToListAsync(ct);

        return Result<PagedResult<DteRecibidoDto>>.Ok(PagedResult<DteRecibidoDto>.Create(items, total, page, pageSize));
    }

    public async Task<Result<DteRecibidoDto>> GetAsync(int empresaId, int id, CancellationToken ct = default)
    {
        var d = await _db.DteDocumentosRecibidos.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        return d is null
            ? Result<DteRecibidoDto>.Fail("DTE recibido no encontrado.", "RECIBIDO_NOT_FOUND")
            : Result<DteRecibidoDto>.Ok(ToDto(d));
    }

    private static DteRecibidoDto ToDto(DteDocumentoRecibido d) => new()
    {
        Id = d.Id,
        EmisorNombre = d.EmisorNombre,
        EmisorNit = d.EmisorNit,
        EmisorNrc = d.EmisorNrc,
        Fecha = d.Fecha,
        TipoDteCodigo = d.TipoDteCodigo,
        NumeroControl = d.NumeroControl,
        SelloRecibido = d.SelloRecibido,
        Subtotal = d.Subtotal,
        Iva = d.Iva,
        Total = d.Total,
        ScanDocumentoId = d.ScanDocumentoId,
        CreatedAt = d.CreatedAt,
    };
}
