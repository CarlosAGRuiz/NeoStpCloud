using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Usuarios;
using NeoSTP.Application.Usuarios.Dtos;
using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Seguridad;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

public class UsuariosService : IUsuariosService
{
    private const string AuditModule = "USUARIOS";

    private readonly NeoStpDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IAuditoriaService _auditoria;

    public UsuariosService(NeoStpDbContext db, IPasswordHasher hasher, IAuditoriaService auditoria)
    {
        _db = db;
        _hasher = hasher;
        _auditoria = auditoria;
    }

    public async Task<Result<PagedResult<UsuarioDto>>> GetListAsync(int? empresaId, PagedQuery query, CancellationToken ct = default)
    {
        var q = _db.Usuarios
            .AsNoTracking()
            .Include(u => u.Roles).ThenInclude(ur => ur.Rol)
            .Where(u => empresaId == null || u.EmpresaId == empresaId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(u => EF.Functions.Like(u.Username, $"%{s}%")
                          || EF.Functions.Like(u.Email, $"%{s}%")
                          || EF.Functions.Like(u.NombreCompleto, $"%{s}%"));
        }

        var total = await q.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var items = await q
            .OrderBy(u => u.Username)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => MapToDto(u))
            .ToListAsync(ct);

        return Result<PagedResult<UsuarioDto>>.Ok(PagedResult<UsuarioDto>.Create(items, total, page, pageSize));
    }

    public async Task<Result<UsuarioDto>> GetByIdAsync(int? empresaId, int id, CancellationToken ct = default)
    {
        var usuario = await LoadAsync(empresaId, id, asNoTracking: true, ct);
        return usuario is null
            ? Result<UsuarioDto>.Fail("Usuario no encontrado.", "USER_NOT_FOUND")
            : Result<UsuarioDto>.Ok(MapToDto(usuario));
    }

    public async Task<Result<UsuarioDto>> CreateAsync(int? empresaId, CreateUsuarioRequest request, string? actor, CancellationToken ct = default)
    {
        var validation = ValidateCreate(request);
        if (validation.IsFailure) return Result<UsuarioDto>.Fail(validation.Error!, validation.ErrorCode, validation.ValidationErrors);

        var exists = await _db.Usuarios.AnyAsync(u => u.EmpresaId == empresaId &&
            (u.Username == request.Username || u.Email == request.Email), ct);
        if (exists)
        {
            return Result<UsuarioDto>.Fail("Username o email ya están en uso en esta empresa.", "USER_DUPLICATE");
        }

        var usuario = new Usuario
        {
            EmpresaId = empresaId,
            Username = request.Username.Trim(),
            Email = request.Email.Trim(),
            PasswordHash = _hasher.Hash(request.Password),
            NombreCompleto = request.NombreCompleto.Trim(),
            Telefono = request.Telefono,
            TipoUsuarioCodigo = NormalizeTipo(request.TipoUsuarioCodigo),
            EstadoCodigo = EstadoCodes.Activo,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actor,
        };

        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync(ct);

        if (request.RoleIds is { Count: > 0 })
        {
            await AssignRolesAsync(usuario, request.RoleIds, empresaId, ct);
            await _db.SaveChangesAsync(ct);
        }

        var dto = await ReloadDtoAsync(usuario.Id, ct);
        await Audit(empresaId, actor, "CREATE", "OK", $"Usuario {usuario.Username} creado", usuario.Id);
        return Result<UsuarioDto>.Ok(dto);
    }

    public async Task<Result<UsuarioDto>> UpdateAsync(int? empresaId, int id, UpdateUsuarioRequest request, string? actor, CancellationToken ct = default)
    {
        var usuario = await LoadAsync(empresaId, id, asNoTracking: false, ct);
        if (usuario is null) return Result<UsuarioDto>.Fail("Usuario no encontrado.", "USER_NOT_FOUND");

        usuario.Email = request.Email.Trim();
        usuario.NombreCompleto = request.NombreCompleto.Trim();
        usuario.Telefono = request.Telefono;
        usuario.TipoUsuarioCodigo = NormalizeTipo(request.TipoUsuarioCodigo);
        usuario.EstadoCodigo = string.IsNullOrWhiteSpace(request.EstadoCodigo) ? usuario.EstadoCodigo : request.EstadoCodigo;
        usuario.UpdatedAt = DateTime.UtcNow;
        usuario.UpdatedBy = actor;

        if (request.RoleIds is not null)
        {
            _db.UsuarioRoles.RemoveRange(usuario.Roles);
            usuario.Roles.Clear();
            await AssignRolesAsync(usuario, request.RoleIds, empresaId, ct);
        }

        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "UPDATE", "OK", $"Usuario {usuario.Username} actualizado", usuario.Id);

        var dto = await ReloadDtoAsync(usuario.Id, ct);
        return Result<UsuarioDto>.Ok(dto);
    }

    public async Task<Result> BloquearAsync(int? empresaId, int id, string? actor, CancellationToken ct = default)
    {
        var usuario = await LoadAsync(empresaId, id, asNoTracking: false, ct);
        if (usuario is null) return Result.Fail("Usuario no encontrado.", "USER_NOT_FOUND");

        usuario.EstadoCodigo = EstadoCodes.Bloqueado;
        usuario.UpdatedAt = DateTime.UtcNow;
        usuario.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "BLOQUEAR", "OK", $"Usuario {usuario.Username} bloqueado", usuario.Id);
        return Result.Ok();
    }

    public async Task<Result> DesbloquearAsync(int? empresaId, int id, string? actor, CancellationToken ct = default)
    {
        var usuario = await LoadAsync(empresaId, id, asNoTracking: false, ct);
        if (usuario is null) return Result.Fail("Usuario no encontrado.", "USER_NOT_FOUND");

        usuario.EstadoCodigo = EstadoCodes.Activo;
        usuario.IntentosFallidos = 0;
        usuario.BloqueadoHasta = null;
        usuario.UpdatedAt = DateTime.UtcNow;
        usuario.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "DESBLOQUEAR", "OK", $"Usuario {usuario.Username} desbloqueado", usuario.Id);
        return Result.Ok();
    }

    public async Task<Result> ChangePasswordAsync(int userId, ChangePasswordRequest request, string? actor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
        {
            return Result.Fail("La nueva contraseña debe tener al menos 8 caracteres.", "PWD_WEAK");
        }

        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (usuario is null) return Result.Fail("Usuario no encontrado.", "USER_NOT_FOUND");

        if (!_hasher.Verify(request.CurrentPassword, usuario.PasswordHash))
        {
            await Audit(usuario.EmpresaId, actor, "CHANGE_PWD", "FAIL", "Password actual inválido", usuario.Id);
            return Result.Fail("Contraseña actual incorrecta.", "PWD_INVALID");
        }

        usuario.PasswordHash = _hasher.Hash(request.NewPassword);
        usuario.UpdatedAt = DateTime.UtcNow;
        usuario.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(usuario.EmpresaId, actor, "CHANGE_PWD", "OK", "Contraseña cambiada", usuario.Id);
        return Result.Ok();
    }

    public async Task<Result> ResetPasswordAsync(int? empresaId, int id, ResetPasswordRequest request, string? actor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
        {
            return Result.Fail("La nueva contraseña debe tener al menos 8 caracteres.", "PWD_WEAK");
        }

        var usuario = await LoadAsync(empresaId, id, asNoTracking: false, ct);
        if (usuario is null) return Result.Fail("Usuario no encontrado.", "USER_NOT_FOUND");

        usuario.PasswordHash = _hasher.Hash(request.NewPassword);
        usuario.UpdatedAt = DateTime.UtcNow;
        usuario.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "RESET_PWD", "OK", $"Password de {usuario.Username} reseteada", usuario.Id);
        return Result.Ok();
    }

    // -- helpers -------------------------------------------------------

    private static string NormalizeTipo(string? raw)
    {
        var t = (raw ?? "OPERADOR").Trim().ToUpperInvariant();
        return t switch
        {
            "SUPERADMIN" or "ADMIN" or "OPERADOR" or "CONTADOR" or "READONLY" => t,
            _ => "OPERADOR",
        };
    }

    private static Result ValidateCreate(CreateUsuarioRequest r)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(r.Username)) errors.Add("Username es obligatorio.");
        if (string.IsNullOrWhiteSpace(r.Email)) errors.Add("Email es obligatorio.");
        if (string.IsNullOrWhiteSpace(r.NombreCompleto)) errors.Add("Nombre completo es obligatorio.");
        if (string.IsNullOrWhiteSpace(r.Password) || r.Password.Length < 8) errors.Add("Password debe tener al menos 8 caracteres.");
        return errors.Count == 0 ? Result.Ok() : Result.Fail("Datos inválidos.", "VALIDATION", errors);
    }

    private async Task<Usuario?> LoadAsync(int? empresaId, int id, bool asNoTracking, CancellationToken ct)
    {
        var q = _db.Usuarios.Include(u => u.Roles).ThenInclude(ur => ur.Rol).AsQueryable();
        if (asNoTracking) q = q.AsNoTracking();
        return await q.FirstOrDefaultAsync(u => u.Id == id && (empresaId == null || u.EmpresaId == empresaId), ct);
    }

    private async Task<UsuarioDto> ReloadDtoAsync(int id, CancellationToken ct)
    {
        var u = await _db.Usuarios.AsNoTracking()
            .Include(x => x.Roles).ThenInclude(ur => ur.Rol)
            .FirstAsync(x => x.Id == id, ct);
        return MapToDto(u);
    }

    private async Task AssignRolesAsync(Usuario usuario, IReadOnlyList<int> roleIds, int? empresaId, CancellationToken ct)
    {
        var validRoles = await _db.Roles
            .Where(r => roleIds.Contains(r.Id) && (r.EmpresaId == null || r.EmpresaId == empresaId))
            .Select(r => r.Id)
            .ToListAsync(ct);

        foreach (var rolId in validRoles.Distinct())
        {
            usuario.Roles.Add(new UsuarioRol { UsuarioId = usuario.Id, RolId = rolId, CreatedAt = DateTime.UtcNow });
        }
    }

    private static UsuarioDto MapToDto(Usuario u) => new()
    {
        Id = u.Id,
        EmpresaId = u.EmpresaId,
        Username = u.Username,
        Email = u.Email,
        NombreCompleto = u.NombreCompleto,
        Telefono = u.Telefono,
        TipoUsuarioCodigo = u.TipoUsuarioCodigo,
        EstadoCodigo = u.EstadoCodigo,
        UltimoLogin = u.UltimoLogin,
        IntentosFallidos = u.IntentosFallidos,
        BloqueadoHasta = u.BloqueadoHasta,
        CreatedAt = u.CreatedAt,
        RoleIds = u.Roles.Select(ur => ur.RolId).ToList(),
        RoleCodigos = u.Roles.Select(ur => ur.Rol.Codigo).ToList(),
    };

    private Task Audit(int? empresaId, string? actor, string accion, string resultado, string detalle, int entidadId)
        => _auditoria.RegistrarAsync(new AuditoriaEvent
        {
            EmpresaId = empresaId,
            Username = actor,
            Modulo = AuditModule,
            Accion = accion,
            Entidad = "Usuario",
            EntidadId = entidadId.ToString(),
            Resultado = resultado,
            Detalle = detalle,
        });
}
