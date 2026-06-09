using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Tesoreria;
using NeoSTP.Application.Tesoreria.Dtos;
using NeoSTP.Domain.Core.Tesoreria;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

/// <summary>
/// NEOTESORERIA (Fase A.3). Administra cuentas de banco/caja y sus movimientos, manteniendo
/// el saldo corriente. Aislado por EmpresaId. Los movimientos pueden referenciar su origen de
/// negocio (planilla, gasto, cobro) para conciliación.
/// </summary>
public class TesoreriaService : ITesoreriaService
{
    private const string AuditModule = "NEOTESORERIA";

    private readonly NeoStpDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public TesoreriaService(NeoStpDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    // ── Cuentas ──────────────────────────────────────────────────────────────

    public async Task<Result<PagedResult<CuentaTesoreriaDto>>> ListCuentasAsync(int empresaId, PagedQuery query, CancellationToken ct = default)
    {
        var q = _db.CuentasTesoreria.AsNoTracking().Where(c => c.EmpresaId == empresaId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(c => c.Codigo.Contains(s) || c.Nombre.Contains(s) || (c.Banco != null && c.Banco.Contains(s)));
        }
        var total = await q.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var items = await q.OrderBy(c => c.EstadoCodigo == EstadosCuentaTesoreria.Activa ? 0 : 1)
            .ThenBy(c => c.Codigo)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(c => ToDto(c)).ToListAsync(ct);
        return Result<PagedResult<CuentaTesoreriaDto>>.Ok(PagedResult<CuentaTesoreriaDto>.Create(items, total, page, pageSize));
    }

    public async Task<Result<CuentaTesoreriaDetalleDto>> GetCuentaAsync(int empresaId, int id, CancellationToken ct = default)
    {
        var c = await _db.CuentasTesoreria.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        if (c is null) return Result<CuentaTesoreriaDetalleDto>.Fail("Cuenta no encontrada.", "CUENTA_TES_NOT_FOUND");

        var movimientos = await _db.MovimientosTesoreria.AsNoTracking()
            .Where(m => m.CuentaId == id && m.EmpresaId == empresaId)
            .OrderByDescending(m => m.Fecha).ThenByDescending(m => m.Id)
            .Take(50)
            .Select(m => ToMovDto(m, c.Nombre)).ToListAsync(ct);

        return Result<CuentaTesoreriaDetalleDto>.Ok(new CuentaTesoreriaDetalleDto
        {
            Id = c.Id, Codigo = c.Codigo, Nombre = c.Nombre, TipoCuenta = c.TipoCuenta,
            Banco = c.Banco, NumeroCuenta = c.NumeroCuenta, MonedaCodigo = c.MonedaCodigo,
            SaldoInicial = c.SaldoInicial, SaldoActual = c.SaldoActual, EstadoCodigo = c.EstadoCodigo,
            Movimientos = movimientos,
        });
    }

    public async Task<Result<CuentaTesoreriaDto>> CrearCuentaAsync(int empresaId, CreateCuentaTesoreriaRequest request, string? actor, CancellationToken ct = default)
    {
        var codigo = (request.Codigo ?? "").Trim();
        if (string.IsNullOrWhiteSpace(codigo)) return Result<CuentaTesoreriaDto>.Fail("El código es obligatorio.", "VALIDATION");
        if (!TiposCuentaTesoreria.All.Contains(request.TipoCuenta)) return Result<CuentaTesoreriaDto>.Fail("Tipo de cuenta inválido.", "VALIDATION");

        var dup = await _db.CuentasTesoreria.AnyAsync(c => c.EmpresaId == empresaId && c.Codigo == codigo, ct);
        if (dup) return Result<CuentaTesoreriaDto>.Fail("Ya existe una cuenta con ese código.", "DUPLICATE");

        var cuenta = new CuentaTesoreria
        {
            EmpresaId = empresaId, Codigo = codigo, Nombre = request.Nombre.Trim(),
            TipoCuenta = request.TipoCuenta, Banco = request.Banco?.Trim(), NumeroCuenta = request.NumeroCuenta?.Trim(),
            MonedaCodigo = string.IsNullOrWhiteSpace(request.MonedaCodigo) ? "USD" : request.MonedaCodigo.Trim(),
            SaldoInicial = request.SaldoInicial, SaldoActual = request.SaldoInicial,
            EstadoCodigo = EstadosCuentaTesoreria.Activa, CreatedBy = actor,
        };
        _db.CuentasTesoreria.Add(cuenta);
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "CREAR_CUENTA", $"{cuenta.Codigo} · {cuenta.Nombre}", "CuentaTesoreria", cuenta.Id);
        return Result<CuentaTesoreriaDto>.Ok(ToDto(cuenta));
    }

    public async Task<Result<CuentaTesoreriaDto>> ActualizarCuentaAsync(int empresaId, int id, UpdateCuentaTesoreriaRequest request, string? actor, CancellationToken ct = default)
    {
        var cuenta = await _db.CuentasTesoreria.FirstOrDefaultAsync(c => c.Id == id && c.EmpresaId == empresaId, ct);
        if (cuenta is null) return Result<CuentaTesoreriaDto>.Fail("Cuenta no encontrada.", "CUENTA_TES_NOT_FOUND");
        if (!TiposCuentaTesoreria.All.Contains(request.TipoCuenta)) return Result<CuentaTesoreriaDto>.Fail("Tipo de cuenta inválido.", "VALIDATION");

        cuenta.Nombre = request.Nombre.Trim();
        cuenta.TipoCuenta = request.TipoCuenta;
        cuenta.Banco = request.Banco?.Trim();
        cuenta.NumeroCuenta = request.NumeroCuenta?.Trim();
        cuenta.UpdatedAt = DateTime.UtcNow; cuenta.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "EDITAR_CUENTA", $"{cuenta.Codigo}", "CuentaTesoreria", cuenta.Id);
        return Result<CuentaTesoreriaDto>.Ok(ToDto(cuenta));
    }

    public async Task<Result> InactivarCuentaAsync(int empresaId, int id, string? actor, CancellationToken ct = default)
        => await CambiarEstadoCuenta(empresaId, id, EstadosCuentaTesoreria.Inactiva, "INACTIVAR_CUENTA", actor, ct);

    public async Task<Result> ReactivarCuentaAsync(int empresaId, int id, string? actor, CancellationToken ct = default)
        => await CambiarEstadoCuenta(empresaId, id, EstadosCuentaTesoreria.Activa, "REACTIVAR_CUENTA", actor, ct);

    private async Task<Result> CambiarEstadoCuenta(int empresaId, int id, string estado, string accion, string? actor, CancellationToken ct)
    {
        var cuenta = await _db.CuentasTesoreria.FirstOrDefaultAsync(c => c.Id == id && c.EmpresaId == empresaId, ct);
        if (cuenta is null) return Result.Fail("Cuenta no encontrada.", "CUENTA_TES_NOT_FOUND");
        cuenta.EstadoCodigo = estado;
        cuenta.UpdatedAt = DateTime.UtcNow; cuenta.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, accion, cuenta.Codigo, "CuentaTesoreria", cuenta.Id);
        return Result.Ok();
    }

    // ── Movimientos ──────────────────────────────────────────────────────────

    public async Task<Result<PagedResult<MovimientoTesoreriaDto>>> ListMovimientosAsync(int empresaId, int? cuentaId, PagedQuery query, CancellationToken ct = default)
    {
        var q = _db.MovimientosTesoreria.AsNoTracking().Where(m => m.EmpresaId == empresaId);
        if (cuentaId is int cid) q = q.Where(m => m.CuentaId == cid);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(m => m.Concepto.Contains(s) || (m.Referencia != null && m.Referencia.Contains(s)));
        }
        var total = await q.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var items = await q
            .Join(_db.CuentasTesoreria.AsNoTracking(), m => m.CuentaId, c => c.Id, (m, c) => new { m, c.Nombre })
            .OrderByDescending(x => x.m.Fecha).ThenByDescending(x => x.m.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => ToMovDto(x.m, x.Nombre)).ToListAsync(ct);
        return Result<PagedResult<MovimientoTesoreriaDto>>.Ok(PagedResult<MovimientoTesoreriaDto>.Create(items, total, page, pageSize));
    }

    public async Task<Result<MovimientoTesoreriaDto>> RegistrarMovimientoAsync(int empresaId, RegistrarMovimientoRequest request, string? actor, CancellationToken ct = default)
    {
        if (request.Monto <= 0) return Result<MovimientoTesoreriaDto>.Fail("El monto debe ser mayor que cero.", "VALIDATION");
        if (!TiposMovimientoTesoreria.All.Contains(request.Tipo)) return Result<MovimientoTesoreriaDto>.Fail("Tipo de movimiento inválido.", "VALIDATION");
        var origen = OrigenesMovimientoTesoreria.All.Contains(request.Origen) ? request.Origen : OrigenesMovimientoTesoreria.Manual;

        var cuenta = await _db.CuentasTesoreria.FirstOrDefaultAsync(c => c.Id == request.CuentaId && c.EmpresaId == empresaId, ct);
        if (cuenta is null) return Result<MovimientoTesoreriaDto>.Fail("Cuenta no encontrada.", "CUENTA_TES_NOT_FOUND");
        if (cuenta.EstadoCodigo != EstadosCuentaTesoreria.Activa) return Result<MovimientoTesoreriaDto>.Fail("La cuenta está inactiva.", "INVALID_STATE");

        var signo = request.Tipo == TiposMovimientoTesoreria.Ingreso ? 1m : -1m;
        cuenta.SaldoActual = decimal.Round(cuenta.SaldoActual + signo * request.Monto, 2, MidpointRounding.AwayFromZero);

        var mov = new MovimientoTesoreria
        {
            EmpresaId = empresaId, CuentaId = cuenta.Id, Fecha = request.Fecha ?? DateOnly.FromDateTime(DateTime.UtcNow),
            Tipo = request.Tipo, Monto = decimal.Round(request.Monto, 2, MidpointRounding.AwayFromZero),
            Concepto = request.Concepto.Trim(), Referencia = request.Referencia?.Trim(),
            Origen = origen, OrigenId = request.OrigenId, SaldoResultante = cuenta.SaldoActual,
            EstadoCodigo = EstadosMovimientoTesoreria.Confirmado, CreatedBy = actor,
        };
        _db.MovimientosTesoreria.Add(mov);
        cuenta.UpdatedAt = DateTime.UtcNow; cuenta.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "REGISTRAR_MOVIMIENTO", $"{mov.Tipo} {mov.Monto:N2} · {cuenta.Codigo} · {mov.Concepto}", "MovimientoTesoreria", mov.Id);
        return Result<MovimientoTesoreriaDto>.Ok(ToMovDto(mov, cuenta.Nombre));
    }

    public async Task<Result> AnularMovimientoAsync(int empresaId, int id, string? actor, CancellationToken ct = default)
    {
        var mov = await _db.MovimientosTesoreria.FirstOrDefaultAsync(m => m.Id == id && m.EmpresaId == empresaId, ct);
        if (mov is null) return Result.Fail("Movimiento no encontrado.", "MOVIMIENTO_NOT_FOUND");
        if (mov.EstadoCodigo == EstadosMovimientoTesoreria.Anulado) return Result.Fail("El movimiento ya está anulado.", "INVALID_STATE");

        var cuenta = await _db.CuentasTesoreria.FirstOrDefaultAsync(c => c.Id == mov.CuentaId && c.EmpresaId == empresaId, ct);
        if (cuenta is null) return Result.Fail("Cuenta no encontrada.", "CUENTA_TES_NOT_FOUND");

        // Revierte el efecto del movimiento en el saldo.
        var signo = mov.Tipo == TiposMovimientoTesoreria.Ingreso ? -1m : 1m;
        cuenta.SaldoActual = decimal.Round(cuenta.SaldoActual + signo * mov.Monto, 2, MidpointRounding.AwayFromZero);
        mov.EstadoCodigo = EstadosMovimientoTesoreria.Anulado;
        mov.UpdatedAt = DateTime.UtcNow; mov.UpdatedBy = actor;
        cuenta.UpdatedAt = DateTime.UtcNow; cuenta.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "ANULAR_MOVIMIENTO", $"{mov.Tipo} {mov.Monto:N2} · {cuenta.Codigo}", "MovimientoTesoreria", mov.Id);
        return Result.Ok();
    }

    public async Task<Result<TesoreriaResumenDto>> ResumenAsync(int empresaId, CancellationToken ct = default)
    {
        var cuentas = await _db.CuentasTesoreria.AsNoTracking()
            .Where(c => c.EmpresaId == empresaId && c.EstadoCodigo == EstadosCuentaTesoreria.Activa)
            .Select(c => new { c.TipoCuenta, c.SaldoActual }).ToListAsync(ct);
        return Result<TesoreriaResumenDto>.Ok(new TesoreriaResumenDto
        {
            CuentasActivas = cuentas.Count,
            SaldoTotal = cuentas.Sum(c => c.SaldoActual),
            SaldoBancos = cuentas.Where(c => c.TipoCuenta == TiposCuentaTesoreria.Banco).Sum(c => c.SaldoActual),
            SaldoCaja = cuentas.Where(c => c.TipoCuenta == TiposCuentaTesoreria.Caja).Sum(c => c.SaldoActual),
        });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static CuentaTesoreriaDto ToDto(CuentaTesoreria c) => new()
    {
        Id = c.Id, Codigo = c.Codigo, Nombre = c.Nombre, TipoCuenta = c.TipoCuenta,
        Banco = c.Banco, NumeroCuenta = c.NumeroCuenta, MonedaCodigo = c.MonedaCodigo,
        SaldoInicial = c.SaldoInicial, SaldoActual = c.SaldoActual, EstadoCodigo = c.EstadoCodigo,
    };

    private static MovimientoTesoreriaDto ToMovDto(MovimientoTesoreria m, string cuentaNombre) => new()
    {
        Id = m.Id, CuentaId = m.CuentaId, CuentaNombre = cuentaNombre, Fecha = m.Fecha,
        Tipo = m.Tipo, Monto = m.Monto, Concepto = m.Concepto, Referencia = m.Referencia,
        Origen = m.Origen, OrigenId = m.OrigenId, SaldoResultante = m.SaldoResultante, EstadoCodigo = m.EstadoCodigo,
    };

    private Task Audit(int empresaId, string? actor, string accion, string detalle, string entidad, int entidadId)
        => _auditoria.RegistrarAsync(new AuditoriaEvent
        {
            EmpresaId = empresaId, Username = actor, Modulo = AuditModule, Accion = accion,
            Entidad = entidad, EntidadId = entidadId.ToString(), Resultado = "OK", Detalle = detalle,
        });
}
