using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Roles;
using NeoSTP.Application.Roles.Dtos;
using NeoSTP.Domain.Core.Seguridad;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

public class RolesService : IRolesService
{
    private const string AuditModule = "ROLES";

    private readonly NeoStpDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public RolesService(NeoStpDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    public async Task<Result<IReadOnlyList<RolDto>>> GetListAsync(int? empresaId, CancellationToken ct = default)
    {
        var items = await _db.Roles
            .AsNoTracking()
            .Include(r => r.Permisos).ThenInclude(rp => rp.Permiso)
            .Where(r => r.EmpresaId == null || r.EmpresaId == empresaId)
            .OrderBy(r => r.EmpresaId == null ? 0 : 1)
            .ThenBy(r => r.Nombre)
            .Select(r => MapToDto(r))
            .ToListAsync(ct);
        return Result<IReadOnlyList<RolDto>>.Ok(items);
    }

    public async Task<Result<RolDto>> GetByIdAsync(int? empresaId, int id, CancellationToken ct = default)
    {
        var rol = await _db.Roles
            .AsNoTracking()
            .Include(r => r.Permisos).ThenInclude(rp => rp.Permiso)
            .FirstOrDefaultAsync(r => r.Id == id && (r.EmpresaId == null || r.EmpresaId == empresaId), ct);
        return rol is null
            ? Result<RolDto>.Fail("Rol no encontrado.", "ROLE_NOT_FOUND")
            : Result<RolDto>.Ok(MapToDto(rol));
    }

    public async Task<Result<RolDto>> CreateAsync(int? empresaId, CreateRolRequest request, string? actor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Codigo) || string.IsNullOrWhiteSpace(request.Nombre))
        {
            return Result<RolDto>.Fail("Código y nombre son obligatorios.", "VALIDATION");
        }

        var codigo = request.Codigo.Trim().ToUpperInvariant();
        var dup = await _db.Roles.AnyAsync(r => r.EmpresaId == empresaId && r.Codigo == codigo, ct);
        if (dup) return Result<RolDto>.Fail($"Ya existe un rol con código {codigo}.", "ROLE_DUPLICATE");

        var rol = new Rol
        {
            EmpresaId = empresaId,
            Codigo = codigo,
            Nombre = request.Nombre.Trim(),
            Descripcion = request.Descripcion,
            EsSistema = false,
            Activo = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actor,
        };
        _db.Roles.Add(rol);
        await _db.SaveChangesAsync(ct);

        if (request.PermisoIds is { Count: > 0 })
        {
            foreach (var pid in request.PermisoIds.Distinct())
            {
                _db.RolPermisos.Add(new RolPermiso { RolId = rol.Id, PermisoId = pid, CreatedAt = DateTime.UtcNow });
            }
            await _db.SaveChangesAsync(ct);
        }

        await Audit(empresaId, actor, "CREATE", "OK", $"Rol {rol.Codigo} creado", rol.Id);
        var dto = await ReloadDtoAsync(rol.Id, ct);
        return Result<RolDto>.Ok(dto);
    }

    public async Task<Result<RolDto>> UpdateAsync(int? empresaId, int id, UpdateRolRequest request, string? actor, CancellationToken ct = default)
    {
        var rol = await _db.Roles
            .Include(r => r.Permisos)
            .FirstOrDefaultAsync(r => r.Id == id && (r.EmpresaId == null || r.EmpresaId == empresaId), ct);
        if (rol is null) return Result<RolDto>.Fail("Rol no encontrado.", "ROLE_NOT_FOUND");
        if (rol.EsSistema) return Result<RolDto>.Fail("Los roles del sistema no se pueden modificar.", "ROLE_SYSTEM");

        rol.Nombre = request.Nombre.Trim();
        rol.Descripcion = request.Descripcion;
        rol.Activo = request.Activo;
        rol.UpdatedAt = DateTime.UtcNow;
        rol.UpdatedBy = actor;

        if (request.PermisoIds is not null)
        {
            _db.RolPermisos.RemoveRange(rol.Permisos);
            rol.Permisos.Clear();
            foreach (var pid in request.PermisoIds.Distinct())
            {
                _db.RolPermisos.Add(new RolPermiso { RolId = rol.Id, PermisoId = pid, CreatedAt = DateTime.UtcNow });
            }
        }

        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "UPDATE", "OK", $"Rol {rol.Codigo} actualizado", rol.Id);
        var dto = await ReloadDtoAsync(rol.Id, ct);
        return Result<RolDto>.Ok(dto);
    }

    public async Task<Result<IReadOnlyList<PermisoDto>>> GetPermisosAsync(CancellationToken ct = default)
    {
        var items = await _db.Permisos
            .AsNoTracking()
            .OrderBy(p => p.Modulo).ThenBy(p => p.Codigo)
            .Select(p => new PermisoDto { Id = p.Id, Codigo = p.Codigo, Modulo = p.Modulo, Descripcion = p.Descripcion })
            .ToListAsync(ct);
        return Result<IReadOnlyList<PermisoDto>>.Ok(items);
    }

    // -- helpers -------------------------------------------------------

    private async Task<RolDto> ReloadDtoAsync(int id, CancellationToken ct)
    {
        var rol = await _db.Roles.AsNoTracking()
            .Include(r => r.Permisos).ThenInclude(rp => rp.Permiso)
            .FirstAsync(r => r.Id == id, ct);
        return MapToDto(rol);
    }

    private static RolDto MapToDto(Rol r) => new()
    {
        Id = r.Id,
        EmpresaId = r.EmpresaId,
        Codigo = r.Codigo,
        Nombre = r.Nombre,
        Descripcion = r.Descripcion,
        EsSistema = r.EsSistema,
        Activo = r.Activo,
        PermisoIds = r.Permisos.Select(rp => rp.PermisoId).ToList(),
        PermisoCodigos = r.Permisos.Select(rp => rp.Permiso.Codigo).ToList(),
    };

    private Task Audit(int? empresaId, string? actor, string accion, string resultado, string detalle, int entidadId)
        => _auditoria.RegistrarAsync(new AuditoriaEvent
        {
            EmpresaId = empresaId,
            Username = actor,
            Modulo = AuditModule,
            Accion = accion,
            Entidad = "Rol",
            EntidadId = entidadId.ToString(),
            Resultado = resultado,
            Detalle = detalle,
        });
}
