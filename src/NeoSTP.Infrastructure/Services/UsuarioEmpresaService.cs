using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Usuarios;
using NeoSTP.Domain.Core.Seguridad;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

/// <summary>Miembros externos por membresía (E1). Aislado por EmpresaId; auditado.</summary>
public class UsuarioEmpresaService : IUsuarioEmpresaService
{
    private const string AuditModule = "USUARIOS";

    private readonly NeoStpDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public UsuarioEmpresaService(NeoStpDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    public async Task<Result<IReadOnlyList<MiembroExternoDto>>> ListarAsync(int empresaId, CancellationToken ct = default)
    {
        var items = await _db.UsuarioEmpresas.AsNoTracking()
            .Include(m => m.Usuario)
            .Include(m => m.Rol)
            .Where(m => m.EmpresaId == empresaId && m.EstadoCodigo == "ACTIVO")
            .OrderBy(m => m.Usuario.NombreCompleto)
            .Select(m => new MiembroExternoDto
            {
                UsuarioId = m.UsuarioId,
                Username = m.Usuario.Username,
                Email = m.Usuario.Email,
                NombreCompleto = m.Usuario.NombreCompleto,
                RolId = m.RolId,
                RolNombre = m.Rol.Nombre,
                EstadoCodigo = m.EstadoCodigo,
            })
            .ToListAsync(ct);
        return Result<IReadOnlyList<MiembroExternoDto>>.Ok(items);
    }

    public async Task<Result<MiembroExternoDto>> AgregarAsync(int empresaId, AgregarMiembroRequest request, string? actor, CancellationToken ct = default)
    {
        var clave = request.EmailOUsername?.Trim();
        if (string.IsNullOrEmpty(clave))
            return Result<MiembroExternoDto>.Fail("Indica el email o username del usuario a invitar.", "VALIDATION");

        var usuario = await _db.Usuarios
            .FirstOrDefaultAsync(u => u.Email == clave || u.Username == clave, ct);
        if (usuario is null)
            return Result<MiembroExternoDto>.Fail(
                "No existe un usuario con ese email/username. El miembro externo debe estar registrado en el sistema.",
                "USER_NOT_FOUND");
        if (usuario.EstadoCodigo != "ACTIVO")
            return Result<MiembroExternoDto>.Fail("El usuario no está activo.", "USER_DISABLED");
        if (usuario.TipoUsuarioCodigo == "SUPERADMIN")
            return Result<MiembroExternoDto>.Fail("SuperAdmin ya accede a todas las empresas en modo soporte.", "VALIDATION");
        if (usuario.EmpresaId == empresaId)
            return Result<MiembroExternoDto>.Fail("Ese usuario ya pertenece a esta empresa como usuario propio.", "MIEMBRO_ES_PROPIO");

        var rol = await _db.Roles.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.RolId && r.Activo
                                   && (r.EmpresaId == null || r.EmpresaId == empresaId), ct);
        if (rol is null)
            return Result<MiembroExternoDto>.Fail("El rol no existe o no es de esta empresa.", "ROLE_NOT_FOUND");

        var existente = await _db.UsuarioEmpresas
            .FirstOrDefaultAsync(m => m.UsuarioId == usuario.Id && m.EmpresaId == empresaId, ct);
        if (existente is not null && existente.EstadoCodigo == "ACTIVO")
            return Result<MiembroExternoDto>.Fail("El usuario ya es miembro de esta empresa.", "MIEMBRO_DUPLICADO");

        if (existente is not null)
        {
            // Reactivar la membresía revocada conservando historial.
            existente.EstadoCodigo = "ACTIVO";
            existente.RolId = rol.Id;
            existente.UpdatedAt = DateTime.UtcNow; existente.UpdatedBy = actor;
        }
        else
        {
            existente = new UsuarioEmpresa
            {
                UsuarioId = usuario.Id, EmpresaId = empresaId, RolId = rol.Id,
                EstadoCodigo = "ACTIVO", CreatedAt = DateTime.UtcNow, CreatedBy = actor,
            };
            _db.UsuarioEmpresas.Add(existente);
        }
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "MIEMBRO_AGREGAR", $"{usuario.Username} con rol {rol.Codigo}", existente.Id);

        return Result<MiembroExternoDto>.Ok(new MiembroExternoDto
        {
            UsuarioId = usuario.Id, Username = usuario.Username, Email = usuario.Email,
            NombreCompleto = usuario.NombreCompleto, RolId = rol.Id, RolNombre = rol.Nombre,
            EstadoCodigo = "ACTIVO",
        });
    }

    public async Task<Result> QuitarAsync(int empresaId, int usuarioId, string? actor, CancellationToken ct = default)
    {
        var membresia = await _db.UsuarioEmpresas
            .Include(m => m.Usuario)
            .FirstOrDefaultAsync(m => m.UsuarioId == usuarioId && m.EmpresaId == empresaId
                                   && m.EstadoCodigo == "ACTIVO", ct);
        if (membresia is null) return Result.Fail("Membresía no encontrada.", "MIEMBRO_NOT_FOUND");

        membresia.EstadoCodigo = "INACTIVO";
        membresia.UpdatedAt = DateTime.UtcNow; membresia.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "MIEMBRO_QUITAR", membresia.Usuario.Username, membresia.Id);
        return Result.Ok();
    }

    private Task Audit(int empresaId, string? actor, string accion, string detalle, int entidadId)
        => _auditoria.RegistrarAsync(new AuditoriaEvent
        {
            EmpresaId = empresaId, Username = actor, Modulo = AuditModule, Accion = accion,
            Entidad = "UsuarioEmpresa", EntidadId = entidadId.ToString(), Resultado = "OK", Detalle = detalle,
        });
}
