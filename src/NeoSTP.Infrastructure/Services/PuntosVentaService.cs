using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Empresas;
using NeoSTP.Application.Empresas.Dtos;
using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

public class PuntosVentaService : IPuntosVentaService
{
    private const string AuditModule = "PUNTOSVENTA";

    private readonly NeoStpDbContext _db;
    private readonly ILicenciaResolver _licencias;
    private readonly IAuditoriaService _auditoria;

    public PuntosVentaService(NeoStpDbContext db, ILicenciaResolver licencias, IAuditoriaService auditoria)
    {
        _db = db;
        _licencias = licencias;
        _auditoria = auditoria;
    }

    public async Task<Result<PagedResult<PuntoVentaDto>>> GetListAsync(int empresaId, int? sucursalId, PagedQuery query, CancellationToken ct = default)
    {
        var q = _db.PuntosVenta.AsNoTracking()
            .Include(p => p.Sucursal)
            .Where(p => p.Sucursal.EmpresaId == empresaId);

        if (sucursalId is not null) q = q.Where(p => p.SucursalId == sucursalId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(p => EF.Functions.Like(p.Codigo, $"%{s}%") || EF.Functions.Like(p.Nombre, $"%{s}%"));
        }

        var total = await q.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var items = await q
            .OrderBy(p => p.Sucursal.Codigo).ThenBy(p => p.Codigo)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new PuntoVentaDto
            {
                Id = p.Id, SucursalId = p.SucursalId,
                EmpresaId = p.Sucursal.EmpresaId,
                SucursalCodigo = p.Sucursal.Codigo,
                SucursalNombre = p.Sucursal.Nombre,
                Codigo = p.Codigo, Nombre = p.Nombre,
                CodigoPuntoVentaMh = p.CodigoPuntoVentaMh,
                EstadoCodigo = p.EstadoCodigo,
                CreatedAt = p.CreatedAt,
            })
            .ToListAsync(ct);

        return Result<PagedResult<PuntoVentaDto>>.Ok(PagedResult<PuntoVentaDto>.Create(items, total, page, pageSize));
    }

    public async Task<Result<PuntoVentaDto>> GetByIdAsync(int empresaId, int id, CancellationToken ct = default)
    {
        var p = await _db.PuntosVenta.AsNoTracking().Include(x => x.Sucursal)
            .FirstOrDefaultAsync(x => x.Id == id && x.Sucursal.EmpresaId == empresaId, ct);
        return p is null
            ? Result<PuntoVentaDto>.Fail("Punto de venta no encontrado.", "PV_NOT_FOUND")
            : Result<PuntoVentaDto>.Ok(MapToDto(p));
    }

    public async Task<Result<PuntoVentaDto>> CreateAsync(int empresaId, CreatePuntoVentaRequest request, string? actor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Codigo) || string.IsNullOrWhiteSpace(request.Nombre))
        {
            return Result<PuntoVentaDto>.Fail("Código y nombre son obligatorios.", "VALIDATION");
        }

        var sucursal = await _db.Sucursales.FirstOrDefaultAsync(s => s.Id == request.SucursalId && s.EmpresaId == empresaId, ct);
        if (sucursal is null) return Result<PuntoVentaDto>.Fail("Sucursal no encontrada en esta empresa.", "SUCURSAL_NOT_FOUND");

        var licencia = await _licencias.ResolveAsync(empresaId, ct);
        if (licencia is null) return Result<PuntoVentaDto>.Fail("Empresa no encontrada.", "EMPRESA_NOT_FOUND");
        if (!licencia.Vigente) return Result<PuntoVentaDto>.Fail("La empresa no tiene un plan vigente.", "LICENSE_INVALID");

        if (licencia.LimitePuntosVenta is int limite && licencia.PuntosVentaUsados >= limite)
        {
            return Result<PuntoVentaDto>.Fail(
                $"El plan {licencia.PlanCodigo} permite {limite} puntos de venta (actuales: {licencia.PuntosVentaUsados}).",
                "LIMIT_EXCEEDED");
        }

        var codigo = request.Codigo.Trim().ToUpperInvariant();
        if (await _db.PuntosVenta.AnyAsync(p => p.SucursalId == sucursal.Id && p.Codigo == codigo, ct))
        {
            return Result<PuntoVentaDto>.Fail($"Ya existe un punto de venta con código {codigo} en esta sucursal.", "PV_DUPLICATE");
        }

        var pv = new PuntoVenta
        {
            SucursalId = sucursal.Id,
            Codigo = codigo,
            Nombre = request.Nombre.Trim(),
            CodigoPuntoVentaMh = request.CodigoPuntoVentaMh,
            EstadoCodigo = EstadoCodes.Activo,
            CreatedAt = DateTime.UtcNow, CreatedBy = actor,
        };
        _db.PuntosVenta.Add(pv);
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "CREATE", "OK", $"Punto de venta {pv.Codigo} creado en sucursal {sucursal.Codigo}", pv.Id);

        pv.Sucursal = sucursal;
        return Result<PuntoVentaDto>.Ok(MapToDto(pv));
    }

    public async Task<Result<PuntoVentaDto>> UpdateAsync(int empresaId, int id, UpdatePuntoVentaRequest request, string? actor, CancellationToken ct = default)
    {
        var pv = await _db.PuntosVenta.Include(p => p.Sucursal)
            .FirstOrDefaultAsync(p => p.Id == id && p.Sucursal.EmpresaId == empresaId, ct);
        if (pv is null) return Result<PuntoVentaDto>.Fail("Punto de venta no encontrado.", "PV_NOT_FOUND");

        pv.Nombre = request.Nombre.Trim();
        pv.CodigoPuntoVentaMh = request.CodigoPuntoVentaMh;
        if (!string.IsNullOrWhiteSpace(request.EstadoCodigo)) pv.EstadoCodigo = request.EstadoCodigo;
        pv.UpdatedAt = DateTime.UtcNow;
        pv.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "UPDATE", "OK", $"Punto de venta {pv.Codigo} actualizado", pv.Id);

        return Result<PuntoVentaDto>.Ok(MapToDto(pv));
    }

    public async Task<Result> InactivarAsync(int empresaId, int id, string? actor, CancellationToken ct = default)
    {
        var pv = await _db.PuntosVenta.Include(p => p.Sucursal)
            .FirstOrDefaultAsync(p => p.Id == id && p.Sucursal.EmpresaId == empresaId, ct);
        if (pv is null) return Result.Fail("Punto de venta no encontrado.", "PV_NOT_FOUND");

        pv.EstadoCodigo = EstadoCodes.Inactivo;
        pv.UpdatedAt = DateTime.UtcNow;
        pv.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "INACTIVAR", "OK", $"Punto de venta {pv.Codigo} inactivado", pv.Id);
        return Result.Ok();
    }

    private static PuntoVentaDto MapToDto(PuntoVenta p) => new()
    {
        Id = p.Id, SucursalId = p.SucursalId,
        EmpresaId = p.Sucursal.EmpresaId,
        SucursalCodigo = p.Sucursal.Codigo,
        SucursalNombre = p.Sucursal.Nombre,
        Codigo = p.Codigo, Nombre = p.Nombre,
        CodigoPuntoVentaMh = p.CodigoPuntoVentaMh,
        EstadoCodigo = p.EstadoCodigo,
        CreatedAt = p.CreatedAt,
    };

    private Task Audit(int empresaId, string? actor, string accion, string resultado, string detalle, int entidadId)
        => _auditoria.RegistrarAsync(new AuditoriaEvent
        {
            EmpresaId = empresaId, Username = actor,
            Modulo = AuditModule, Accion = accion,
            Entidad = "PuntoVenta", EntidadId = entidadId.ToString(),
            Resultado = resultado, Detalle = detalle,
        });
}
