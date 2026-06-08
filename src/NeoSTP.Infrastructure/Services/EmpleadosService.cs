using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Rrhh;
using NeoSTP.Application.Rrhh.Dtos;
using NeoSTP.Domain.Core.Rrhh;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

/// <summary>
/// Gestión de empleados (NEORRHH). Mantiene el contrato vigente y calcula la vista previa
/// de nómina con <see cref="NominaCalculator"/>. Aislado por EmpresaId; soft-delete.
/// </summary>
public class EmpleadosService : IEmpleadosService
{
    private const string AuditModule = "NEORRHH";

    private readonly NeoStpDbContext _db;
    private readonly IAuditoriaService _auditoria;
    private readonly NominaOptions _nomina;
    private readonly NominaCalculator _calc = new();

    public EmpleadosService(NeoStpDbContext db, IAuditoriaService auditoria, IOptions<NominaOptions> nomina)
    {
        _db = db;
        _auditoria = auditoria;
        _nomina = nomina.Value;
    }

    public async Task<Result<PagedResult<EmpleadoDto>>> GetListAsync(int empresaId, PagedQuery query, CancellationToken ct = default)
    {
        var q = _db.Empleados.AsNoTracking().Where(e => e.EmpresaId == empresaId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(e => EF.Functions.Like(e.Nombres, $"%{s}%")
                          || EF.Functions.Like(e.Apellidos, $"%{s}%")
                          || EF.Functions.Like(e.Codigo, $"%{s}%")
                          || EF.Functions.Like(e.NumeroDocumento, $"%{s}%"));
        }

        var total = await q.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var items = await q.OrderBy(e => e.Apellidos).ThenBy(e => e.Nombres)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(e => new EmpleadoDto
            {
                Id = e.Id, Codigo = e.Codigo, NombreCompleto = e.Nombres + " " + e.Apellidos,
                NumeroDocumento = e.NumeroDocumento, Cargo = e.Cargo, EstadoCodigo = e.EstadoCodigo,
                SalarioMensual = e.Contratos.Where(c => c.EstadoCodigo == ContratoEstados.Vigente)
                    .Select(c => c.SalarioMensual).FirstOrDefault(),
                PeriodicidadPago = e.Contratos.Where(c => c.EstadoCodigo == ContratoEstados.Vigente)
                    .Select(c => c.PeriodicidadPago).FirstOrDefault() ?? "QUINCENAL",
            })
            .ToListAsync(ct);

        return Result<PagedResult<EmpleadoDto>>.Ok(PagedResult<EmpleadoDto>.Create(items, total, page, pageSize));
    }

    public async Task<Result<EmpleadoDetalleDto>> GetAsync(int empresaId, int id, CancellationToken ct = default)
    {
        var e = await _db.Empleados.AsNoTracking().Include(x => x.Contratos)
            .FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        return e is null
            ? Result<EmpleadoDetalleDto>.Fail("Empleado no encontrado.", "EMPLEADO_NOT_FOUND")
            : Result<EmpleadoDetalleDto>.Ok(ToDetalle(e));
    }

    public async Task<Result<EmpleadoDetalleDto>> CreateAsync(int empresaId, CreateEmpleadoRequest request, string? actor, CancellationToken ct = default)
    {
        var val = Validate(request);
        if (val.IsFailure) return Result<EmpleadoDetalleDto>.Fail(val.Error!, val.ErrorCode, val.ValidationErrors);

        var codigo = request.Codigo.Trim();
        if (await _db.Empleados.AnyAsync(x => x.EmpresaId == empresaId && x.Codigo == codigo, ct))
            return Result<EmpleadoDetalleDto>.Fail($"Ya existe un empleado con código {codigo}.", "DUPLICATE");

        var empleado = new Empleado
        {
            EmpresaId = empresaId,
            Codigo = codigo,
            Nombres = request.Nombres.Trim(),
            Apellidos = request.Apellidos.Trim(),
            TipoDocumento = request.TipoDocumento.Trim().ToUpperInvariant(),
            NumeroDocumento = request.NumeroDocumento.Trim(),
            Nit = Clean(request.Nit),
            IsssNumero = Clean(request.IsssNumero),
            AfpInstitucion = Clean(request.AfpInstitucion),
            AfpNumero = Clean(request.AfpNumero),
            FechaNacimiento = request.FechaNacimiento,
            FechaIngreso = request.FechaIngreso ?? DateOnly.FromDateTime(DateTime.UtcNow),
            Cargo = Clean(request.Cargo),
            Email = Clean(request.Email),
            Telefono = Clean(request.Telefono),
            EstadoCodigo = "ACTIVO",
            CreatedBy = actor,
        };
        empleado.Contratos.Add(NuevoContrato(empresaId, request, empleado.FechaIngreso, actor));

        _db.Empleados.Add(empleado);
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "CREAR_EMPLEADO", $"{empleado.NombreCompleto} ({codigo})", empleado.Id);
        return Result<EmpleadoDetalleDto>.Ok(ToDetalle(empleado));
    }

    public async Task<Result<EmpleadoDetalleDto>> UpdateAsync(int empresaId, int id, UpdateEmpleadoRequest request, string? actor, CancellationToken ct = default)
    {
        var val = Validate(request);
        if (val.IsFailure) return Result<EmpleadoDetalleDto>.Fail(val.Error!, val.ErrorCode, val.ValidationErrors);

        var e = await _db.Empleados.Include(x => x.Contratos)
            .FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        if (e is null) return Result<EmpleadoDetalleDto>.Fail("Empleado no encontrado.", "EMPLEADO_NOT_FOUND");

        e.Nombres = request.Nombres.Trim();
        e.Apellidos = request.Apellidos.Trim();
        e.TipoDocumento = request.TipoDocumento.Trim().ToUpperInvariant();
        e.NumeroDocumento = request.NumeroDocumento.Trim();
        e.Nit = Clean(request.Nit);
        e.IsssNumero = Clean(request.IsssNumero);
        e.AfpInstitucion = Clean(request.AfpInstitucion);
        e.AfpNumero = Clean(request.AfpNumero);
        e.FechaNacimiento = request.FechaNacimiento;
        if (request.FechaIngreso is DateOnly fi) e.FechaIngreso = fi;
        e.Cargo = Clean(request.Cargo);
        e.Email = Clean(request.Email);
        e.Telefono = Clean(request.Telefono);
        e.UpdatedAt = DateTime.UtcNow; e.UpdatedBy = actor;

        var vigente = e.Contratos.FirstOrDefault(c => c.EstadoCodigo == ContratoEstados.Vigente);
        if (vigente is null)
        {
            e.Contratos.Add(NuevoContrato(empresaId, request, e.FechaIngreso, actor));
        }
        else
        {
            vigente.TipoContrato = request.TipoContrato.Trim().ToUpperInvariant();
            vigente.SalarioMensual = request.SalarioMensual;
            vigente.PeriodicidadPago = request.PeriodicidadPago.Trim().ToUpperInvariant();
            vigente.UpdatedAt = DateTime.UtcNow; vigente.UpdatedBy = actor;
        }

        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "EDITAR_EMPLEADO", $"{e.NombreCompleto} ({e.Codigo})", e.Id);
        return Result<EmpleadoDetalleDto>.Ok(ToDetalle(e));
    }

    public async Task<Result> InactivarAsync(int empresaId, int id, string? actor, CancellationToken ct = default)
    {
        var e = await _db.Empleados.Include(x => x.Contratos)
            .FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        if (e is null) return Result.Fail("Empleado no encontrado.", "EMPLEADO_NOT_FOUND");

        e.EstadoCodigo = "INACTIVO";
        e.FechaEgreso = DateOnly.FromDateTime(DateTime.UtcNow);
        e.UpdatedAt = DateTime.UtcNow; e.UpdatedBy = actor;
        foreach (var c in e.Contratos.Where(c => c.EstadoCodigo == ContratoEstados.Vigente))
        {
            c.EstadoCodigo = ContratoEstados.Finalizado;
            c.FechaFin = e.FechaEgreso;
            c.UpdatedAt = DateTime.UtcNow; c.UpdatedBy = actor;
        }
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "INACTIVAR_EMPLEADO", e.NombreCompleto, e.Id);
        return Result.Ok();
    }

    public async Task<Result> RestaurarAsync(int empresaId, int id, string? actor, CancellationToken ct = default)
    {
        var e = await _db.Empleados.FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        if (e is null) return Result.Fail("Empleado no encontrado.", "EMPLEADO_NOT_FOUND");
        e.EstadoCodigo = "ACTIVO";
        e.FechaEgreso = null;
        e.UpdatedAt = DateTime.UtcNow; e.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "RESTAURAR_EMPLEADO", e.NombreCompleto, e.Id);
        return Result.Ok();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static ContratoLaboral NuevoContrato(int empresaId, CreateEmpleadoRequest r, DateOnly inicio, string? actor) => new()
    {
        EmpresaId = empresaId,
        TipoContrato = r.TipoContrato.Trim().ToUpperInvariant(),
        SalarioMensual = r.SalarioMensual,
        PeriodicidadPago = r.PeriodicidadPago.Trim().ToUpperInvariant(),
        FechaInicio = inicio,
        EstadoCodigo = ContratoEstados.Vigente,
        CreatedBy = actor,
    };

    private static Result Validate(CreateEmpleadoRequest r)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(r.Codigo)) errors.Add("El código es obligatorio.");
        if (string.IsNullOrWhiteSpace(r.Nombres)) errors.Add("Los nombres son obligatorios.");
        if (string.IsNullOrWhiteSpace(r.Apellidos)) errors.Add("Los apellidos son obligatorios.");
        if (string.IsNullOrWhiteSpace(r.NumeroDocumento)) errors.Add("El número de documento es obligatorio.");
        if (r.SalarioMensual <= 0) errors.Add("El salario mensual debe ser mayor a 0.");
        return errors.Count == 0 ? Result.Ok() : Result.Fail("Datos inválidos.", "VALIDATION", errors);
    }

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    private EmpleadoDetalleDto ToDetalle(Empleado e)
    {
        var vigente = e.Contratos.FirstOrDefault(c => c.EstadoCodigo == ContratoEstados.Vigente)
                   ?? e.Contratos.OrderByDescending(c => c.FechaInicio).FirstOrDefault();
        var salario = vigente?.SalarioMensual ?? 0m;
        return new EmpleadoDetalleDto
        {
            Id = e.Id, Codigo = e.Codigo, Nombres = e.Nombres, Apellidos = e.Apellidos,
            TipoDocumento = e.TipoDocumento, NumeroDocumento = e.NumeroDocumento, Nit = e.Nit,
            IsssNumero = e.IsssNumero, AfpInstitucion = e.AfpInstitucion, AfpNumero = e.AfpNumero,
            FechaNacimiento = e.FechaNacimiento, FechaIngreso = e.FechaIngreso, FechaEgreso = e.FechaEgreso,
            Cargo = e.Cargo, Email = e.Email, Telefono = e.Telefono, EstadoCodigo = e.EstadoCodigo,
            TipoContrato = vigente?.TipoContrato ?? "INDEFINIDO",
            SalarioMensual = salario,
            PeriodicidadPago = vigente?.PeriodicidadPago ?? "QUINCENAL",
            NominaPreview = salario > 0 ? _calc.CalcularMensual(salario, _nomina) : null,
        };
    }

    private Task Audit(int empresaId, string? actor, string accion, string detalle, int entidadId)
        => _auditoria.RegistrarAsync(new AuditoriaEvent
        {
            EmpresaId = empresaId, Username = actor, Modulo = AuditModule, Accion = accion,
            Entidad = "Empleado", EntidadId = entidadId.ToString(), Resultado = "OK", Detalle = detalle,
        });
}
