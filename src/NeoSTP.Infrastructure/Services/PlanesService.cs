using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Common;
using NeoSTP.Application.Licenciamiento;
using NeoSTP.Application.Licenciamiento.Dtos;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

public class PlanesService : IPlanesService
{
    private readonly NeoStpDbContext _db;

    public PlanesService(NeoStpDbContext db) { _db = db; }

    public async Task<Result<IReadOnlyList<PlanDto>>> GetListAsync(CancellationToken ct = default)
    {
        var items = await _db.Planes.AsNoTracking()
            .Include(p => p.Modulos).ThenInclude(pm => pm.Modulo)
            .Where(p => p.Activo)
            .OrderBy(p => p.PrecioMensual)
            .Select(p => MapToDto(p))
            .ToListAsync(ct);
        return Result<IReadOnlyList<PlanDto>>.Ok(items);
    }

    public async Task<Result<PlanDto>> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var p = await _db.Planes.AsNoTracking()
            .Include(p => p.Modulos).ThenInclude(pm => pm.Modulo)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        return p is null
            ? Result<PlanDto>.Fail("Plan no encontrado.", "PLAN_NOT_FOUND")
            : Result<PlanDto>.Ok(MapToDto(p));
    }

    private static PlanDto MapToDto(NeoSTP.Domain.Core.Licenciamiento.Plan p) => new()
    {
        Id = p.Id, Codigo = p.Codigo, Nombre = p.Nombre, Descripcion = p.Descripcion,
        PrecioMensual = p.PrecioMensual, MonedaCodigo = p.MonedaCodigo,
        LimiteUsuarios = p.LimiteUsuarios, LimiteSucursales = p.LimiteSucursales,
        LimitePuntosVenta = p.LimitePuntosVenta, LimiteDteMensual = p.LimiteDteMensual,
        Activo = p.Activo,
        Modulos = p.Modulos.Select(pm => new ModuloDto
        {
            Id = pm.Modulo.Id, Codigo = pm.Modulo.Codigo, Nombre = pm.Modulo.Nombre,
            Descripcion = pm.Modulo.Descripcion, Icono = pm.Modulo.Icono,
            Orden = pm.Modulo.Orden, Activo = pm.Modulo.Activo,
        }).OrderBy(m => m.Orden).ToList(),
    };
}

public class ModulosService : IModulosService
{
    private readonly NeoStpDbContext _db;

    public ModulosService(NeoStpDbContext db) { _db = db; }

    public async Task<Result<IReadOnlyList<ModuloDto>>> GetListAsync(CancellationToken ct = default)
    {
        var items = await _db.Modulos.AsNoTracking()
            .OrderBy(m => m.Orden)
            .Select(m => new ModuloDto
            {
                Id = m.Id, Codigo = m.Codigo, Nombre = m.Nombre,
                Descripcion = m.Descripcion, Icono = m.Icono,
                Orden = m.Orden, Activo = m.Activo,
            })
            .ToListAsync(ct);
        return Result<IReadOnlyList<ModuloDto>>.Ok(items);
    }
}
