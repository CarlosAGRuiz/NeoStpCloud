using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Domain.Core.Auditoria;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Auth;

/// <summary>
/// Consulta de la bitácora de auditoría (M3.4): filtros, paginación y export.
/// El aislamiento por empresa lo decide el llamador (controller) según el rol.
/// </summary>
public sealed class AuditoriaQueryService : IAuditoriaQueryService
{
    private readonly NeoStpDbContext _db;

    public AuditoriaQueryService(NeoStpDbContext db) => _db = db;

    public async Task<PagedResult<AuditoriaDto>> ListAsync(AuditoriaQuery query, CancellationToken ct = default)
    {
        var q = Filtrar(query);
        var total = await q.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var items = await q.OrderByDescending(a => a.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => ToDto(a)).ToListAsync(ct);
        return PagedResult<AuditoriaDto>.Create(items, total, page, pageSize);
    }

    public async Task<IReadOnlyList<AuditoriaDto>> ExportAsync(AuditoriaQuery query, int max = 10000, CancellationToken ct = default)
    {
        var q = Filtrar(query);
        return await q.OrderByDescending(a => a.Id).Take(Math.Clamp(max, 1, 50000))
            .Select(a => ToDto(a)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetModulosAsync(int? empresaId, CancellationToken ct = default)
    {
        var q = _db.Auditoria.AsNoTracking().AsQueryable();
        if (empresaId is int eid) q = q.Where(a => a.EmpresaId == eid);
        return await q.Select(a => a.Modulo).Distinct().OrderBy(m => m).ToListAsync(ct);
    }

    private IQueryable<Auditoria> Filtrar(AuditoriaQuery query)
    {
        var q = _db.Auditoria.AsNoTracking().AsQueryable();

        if (query.EmpresaId is int eid) q = q.Where(a => a.EmpresaId == eid);
        if (!string.IsNullOrWhiteSpace(query.Modulo)) q = q.Where(a => a.Modulo == query.Modulo);
        if (!string.IsNullOrWhiteSpace(query.Accion)) q = q.Where(a => a.Accion == query.Accion);
        if (!string.IsNullOrWhiteSpace(query.Resultado)) q = q.Where(a => a.Resultado == query.Resultado);
        if (!string.IsNullOrWhiteSpace(query.Username))
        {
            var u = query.Username.Trim();
            q = q.Where(a => a.Username != null && EF.Functions.Like(a.Username, $"%{u}%"));
        }
        if (query.Desde is DateTime desde) q = q.Where(a => a.CreatedAt >= desde);
        if (query.Hasta is DateTime hasta) q = q.Where(a => a.CreatedAt <= hasta);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(a => EF.Functions.Like(a.Accion, $"%{s}%")
                          || EF.Functions.Like(a.Modulo, $"%{s}%")
                          || (a.Entidad != null && EF.Functions.Like(a.Entidad, $"%{s}%"))
                          || (a.Detalle != null && EF.Functions.Like(a.Detalle, $"%{s}%")));
        }
        return q;
    }

    private static AuditoriaDto ToDto(Auditoria a) => new()
    {
        Id = a.Id, EmpresaId = a.EmpresaId, UsuarioId = a.UsuarioId, Username = a.Username,
        Modulo = a.Modulo, Accion = a.Accion, Entidad = a.Entidad, EntidadId = a.EntidadId,
        Resultado = a.Resultado, Detalle = a.Detalle, IpAddress = a.IpAddress, TraceId = a.TraceId,
        CreatedAt = a.CreatedAt,
    };
}
