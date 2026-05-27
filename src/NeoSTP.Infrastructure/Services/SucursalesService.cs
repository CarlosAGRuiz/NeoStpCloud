using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Empresas;
using NeoSTP.Application.Empresas.Dtos;
using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

public class SucursalesService : ISucursalesService
{
    private const string AuditModule = "SUCURSALES";

    private readonly NeoStpDbContext _db;
    private readonly ILicenciaResolver _licencias;
    private readonly IAuditoriaService _auditoria;

    public SucursalesService(NeoStpDbContext db, ILicenciaResolver licencias, IAuditoriaService auditoria)
    {
        _db = db;
        _licencias = licencias;
        _auditoria = auditoria;
    }

    public async Task<Result<PagedResult<SucursalDto>>> GetListAsync(int empresaId, PagedQuery query, CancellationToken ct = default)
    {
        var q = _db.Sucursales.AsNoTracking().Where(s => s.EmpresaId == empresaId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(x => EF.Functions.Like(x.Codigo, $"%{s}%") || EF.Functions.Like(x.Nombre, $"%{s}%"));
        }

        var total = await q.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var items = await q
            .OrderBy(x => x.Codigo)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new SucursalDto
            {
                Id = s.Id, EmpresaId = s.EmpresaId,
                Codigo = s.Codigo, Nombre = s.Nombre,
                CodigoEstablecimientoMh = s.CodigoEstablecimientoMh,
                TipoEstablecimientoCodigo = s.TipoEstablecimientoCodigo,
                Direccion = s.Direccion, Telefono = s.Telefono,
                Departamento = s.Departamento, Municipio = s.Municipio,
                EstadoCodigo = s.EstadoCodigo,
                PuntosVenta = s.PuntosVenta.Count,
                CreatedAt = s.CreatedAt,
            })
            .ToListAsync(ct);

        return Result<PagedResult<SucursalDto>>.Ok(PagedResult<SucursalDto>.Create(items, total, page, pageSize));
    }

    public async Task<Result<SucursalDto>> GetByIdAsync(int empresaId, int id, CancellationToken ct = default)
    {
        var s = await _db.Sucursales.AsNoTracking()
            .Include(x => x.PuntosVenta)
            .FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        return s is null
            ? Result<SucursalDto>.Fail("Sucursal no encontrada.", "SUCURSAL_NOT_FOUND")
            : Result<SucursalDto>.Ok(MapToDto(s));
    }

    public async Task<Result<SucursalDto>> CreateAsync(int empresaId, CreateSucursalRequest request, string? actor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Codigo) || string.IsNullOrWhiteSpace(request.Nombre))
        {
            return Result<SucursalDto>.Fail("Código y nombre son obligatorios.", "VALIDATION");
        }

        var licencia = await _licencias.ResolveAsync(empresaId, ct);
        if (licencia is null) return Result<SucursalDto>.Fail("Empresa no encontrada.", "EMPRESA_NOT_FOUND");
        if (!licencia.Vigente) return Result<SucursalDto>.Fail("La empresa no tiene un plan vigente.", "LICENSE_INVALID");

        if (licencia.LimiteSucursales is int limite && licencia.SucursalesUsadas >= limite)
        {
            return Result<SucursalDto>.Fail(
                $"El plan {licencia.PlanCodigo} permite {limite} sucursales (actuales: {licencia.SucursalesUsadas}).",
                "LIMIT_EXCEEDED");
        }

        var codigo = request.Codigo.Trim().ToUpperInvariant();
        if (await _db.Sucursales.AnyAsync(s => s.EmpresaId == empresaId && s.Codigo == codigo, ct))
        {
            return Result<SucursalDto>.Fail($"Ya existe una sucursal con código {codigo}.", "SUCURSAL_DUPLICATE");
        }

        var sucursal = new Sucursal
        {
            EmpresaId = empresaId,
            Codigo = codigo,
            Nombre = request.Nombre.Trim(),
            CodigoEstablecimientoMh = request.CodigoEstablecimientoMh,
            TipoEstablecimientoCodigo = request.TipoEstablecimientoCodigo,
            Direccion = request.Direccion, Telefono = request.Telefono,
            Departamento = request.Departamento, Municipio = request.Municipio,
            EstadoCodigo = EstadoCodes.Activo,
            CreatedAt = DateTime.UtcNow, CreatedBy = actor,
        };
        _db.Sucursales.Add(sucursal);
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "CREATE", "OK", $"Sucursal {sucursal.Codigo} creada", sucursal.Id);

        return Result<SucursalDto>.Ok(MapToDto(sucursal));
    }

    public async Task<Result<SucursalDto>> UpdateAsync(int empresaId, int id, UpdateSucursalRequest request, string? actor, CancellationToken ct = default)
    {
        var sucursal = await _db.Sucursales.FirstOrDefaultAsync(s => s.Id == id && s.EmpresaId == empresaId, ct);
        if (sucursal is null) return Result<SucursalDto>.Fail("Sucursal no encontrada.", "SUCURSAL_NOT_FOUND");

        sucursal.Nombre = request.Nombre.Trim();
        sucursal.CodigoEstablecimientoMh = request.CodigoEstablecimientoMh;
        sucursal.TipoEstablecimientoCodigo = request.TipoEstablecimientoCodigo;
        sucursal.Direccion = request.Direccion;
        sucursal.Telefono = request.Telefono;
        sucursal.Departamento = request.Departamento;
        sucursal.Municipio = request.Municipio;
        if (!string.IsNullOrWhiteSpace(request.EstadoCodigo)) sucursal.EstadoCodigo = request.EstadoCodigo;
        sucursal.UpdatedAt = DateTime.UtcNow;
        sucursal.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "UPDATE", "OK", $"Sucursal {sucursal.Codigo} actualizada", sucursal.Id);

        return Result<SucursalDto>.Ok(MapToDto(sucursal));
    }

    public async Task<Result> InactivarAsync(int empresaId, int id, string? actor, CancellationToken ct = default)
    {
        var sucursal = await _db.Sucursales.FirstOrDefaultAsync(s => s.Id == id && s.EmpresaId == empresaId, ct);
        if (sucursal is null) return Result.Fail("Sucursal no encontrada.", "SUCURSAL_NOT_FOUND");

        sucursal.EstadoCodigo = EstadoCodes.Inactivo;
        sucursal.UpdatedAt = DateTime.UtcNow;
        sucursal.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "INACTIVAR", "OK", $"Sucursal {sucursal.Codigo} inactivada", sucursal.Id);
        return Result.Ok();
    }

    private static SucursalDto MapToDto(Sucursal s) => new()
    {
        Id = s.Id, EmpresaId = s.EmpresaId,
        Codigo = s.Codigo, Nombre = s.Nombre,
        CodigoEstablecimientoMh = s.CodigoEstablecimientoMh,
        TipoEstablecimientoCodigo = s.TipoEstablecimientoCodigo,
        Direccion = s.Direccion, Telefono = s.Telefono,
        Departamento = s.Departamento, Municipio = s.Municipio,
        EstadoCodigo = s.EstadoCodigo,
        PuntosVenta = s.PuntosVenta?.Count ?? 0,
        CreatedAt = s.CreatedAt,
    };

    private Task Audit(int empresaId, string? actor, string accion, string resultado, string detalle, int entidadId)
        => _auditoria.RegistrarAsync(new AuditoriaEvent
        {
            EmpresaId = empresaId, Username = actor,
            Modulo = AuditModule, Accion = accion,
            Entidad = "Sucursal", EntidadId = entidadId.ToString(),
            Resultado = resultado, Detalle = detalle,
        });
}
