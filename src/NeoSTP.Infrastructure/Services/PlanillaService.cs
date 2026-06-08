using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Profit;
using NeoSTP.Application.Profit.Dtos;
using NeoSTP.Application.Rrhh;
using NeoSTP.Application.Rrhh.Dtos;
using NeoSTP.Domain.Core.Rrhh;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

/// <summary>
/// Corridas de planilla (NEORRHH Sprint 2). Calcula el período para los empleados activos,
/// recalcula, cierra (genera gasto PLANILLA en NeoProfit) y anula. Aislado por EmpresaId.
/// </summary>
public class PlanillaService : IPlanillaService
{
    private const string AuditModule = "NEORRHH";

    private readonly NeoStpDbContext _db;
    private readonly IAuditoriaService _auditoria;
    private readonly IProfitService _profit;
    private readonly NominaOptions _nomina;
    private readonly NominaCalculator _calc = new();

    public PlanillaService(NeoStpDbContext db, IAuditoriaService auditoria, IProfitService profit, IOptions<NominaOptions> nomina)
    {
        _db = db;
        _auditoria = auditoria;
        _profit = profit;
        _nomina = nomina.Value;
    }

    public async Task<Result<PagedResult<PlanillaPeriodoDto>>> ListAsync(int empresaId, PagedQuery query, CancellationToken ct = default)
    {
        var q = _db.PlanillaPeriodos.AsNoTracking().Where(p => p.EmpresaId == empresaId);
        var total = await q.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var items = await q.OrderByDescending(p => p.Anio).ThenByDescending(p => p.Mes).ThenByDescending(p => p.Quincena).ThenByDescending(p => p.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new PlanillaPeriodoDto
            {
                Id = p.Id, Anio = p.Anio, Mes = p.Mes, Quincena = p.Quincena,
                FechaInicio = p.FechaInicio, FechaFin = p.FechaFin, EstadoCodigo = p.EstadoCodigo,
                Empleados = p.Detalles.Count, TotalNeto = p.TotalNeto, TotalCostoPatronal = p.TotalCostoPatronal,
            }).ToListAsync(ct);
        return Result<PagedResult<PlanillaPeriodoDto>>.Ok(PagedResult<PlanillaPeriodoDto>.Create(items, total, page, pageSize));
    }

    public async Task<Result<PlanillaPeriodoDetalleDto>> GetAsync(int empresaId, int id, CancellationToken ct = default)
    {
        var p = await _db.PlanillaPeriodos.AsNoTracking().Include(x => x.Detalles)
            .FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        return p is null
            ? Result<PlanillaPeriodoDetalleDto>.Fail("Planilla no encontrada.", "PLANILLA_NOT_FOUND")
            : Result<PlanillaPeriodoDetalleDto>.Ok(ToDetalle(p));
    }

    public async Task<Result<PlanillaPeriodoDetalleDto>> CrearAsync(int empresaId, CrearPlanillaRequest request, string? actor, CancellationToken ct = default)
    {
        if (request.Anio < 2000 || request.Anio > 2100) return Fail("Año inválido.");
        if (request.Mes < 1 || request.Mes > 12) return Fail("Mes inválido.");
        if (request.Quincena is < 0 or > 2) return Fail("Quincena inválida (0 mensual, 1 o 2).");

        var existe = await _db.PlanillaPeriodos.AnyAsync(p => p.EmpresaId == empresaId
            && p.Anio == request.Anio && p.Mes == request.Mes && p.Quincena == request.Quincena
            && p.EstadoCodigo != PlanillaEstados.Anulada, ct);
        if (existe) return Fail("Ya existe una corrida para ese período. Anúlala o recalcúlala.", "DUPLICATE");

        var (inicio, fin) = RangoFechas(request.Anio, request.Mes, request.Quincena);
        var periodo = new PlanillaPeriodo
        {
            EmpresaId = empresaId, Anio = request.Anio, Mes = request.Mes, Quincena = request.Quincena,
            FechaInicio = inicio, FechaFin = fin, EstadoCodigo = PlanillaEstados.Calculada, CreatedBy = actor,
        };

        await GenerarDetallesAsync(empresaId, periodo, request.Quincena, ct);
        _db.PlanillaPeriodos.Add(periodo);
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "CREAR_PLANILLA", $"{periodo.Mes:00}/{periodo.Anio} Q{periodo.Quincena} · {periodo.Detalles.Count} empleados", periodo.Id);
        return Result<PlanillaPeriodoDetalleDto>.Ok(ToDetalle(periodo));
    }

    public async Task<Result<PlanillaPeriodoDetalleDto>> RecalcularAsync(int empresaId, int id, string? actor, CancellationToken ct = default)
    {
        var p = await _db.PlanillaPeriodos.Include(x => x.Detalles).FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        if (p is null) return Result<PlanillaPeriodoDetalleDto>.Fail("Planilla no encontrada.", "PLANILLA_NOT_FOUND");
        if (p.EstadoCodigo is PlanillaEstados.Cerrada or PlanillaEstados.Anulada)
            return Result<PlanillaPeriodoDetalleDto>.Fail("No se puede recalcular una planilla cerrada o anulada.", "INVALID_STATE");

        _db.PlanillaDetalles.RemoveRange(p.Detalles);
        p.Detalles.Clear();
        await GenerarDetallesAsync(empresaId, p, p.Quincena, ct);
        p.EstadoCodigo = PlanillaEstados.Calculada;
        p.UpdatedAt = DateTime.UtcNow; p.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "RECALCULAR_PLANILLA", $"{p.Mes:00}/{p.Anio} Q{p.Quincena}", p.Id);
        return Result<PlanillaPeriodoDetalleDto>.Ok(ToDetalle(p));
    }

    public async Task<Result> CerrarAsync(int empresaId, int id, string? actor, CancellationToken ct = default)
    {
        var p = await _db.PlanillaPeriodos.FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        if (p is null) return Result.Fail("Planilla no encontrada.", "PLANILLA_NOT_FOUND");
        if (p.EstadoCodigo != PlanillaEstados.Calculada)
            return Result.Fail("Solo se puede cerrar una planilla calculada.", "INVALID_STATE");

        var gasto = await _profit.CreateGastoAsync(empresaId, new CreateProfitGastoRequest
        {
            Fecha = p.FechaFin,
            Categoria = "PLANILLA",
            Descripcion = $"Planilla {p.Mes:00}/{p.Anio}" + (p.Quincena == 0 ? " (mensual)" : $" Q{p.Quincena}"),
            Proveedor = "Nómina",
            Monto = p.TotalCostoPatronal,
            IvaMonto = 0m,
            IvaDeducible = false,
        }, actor, ct);
        if (gasto.IsFailure) return Result.Fail(gasto.Error ?? "No se pudo registrar el gasto de planilla.", gasto.ErrorCode);

        p.ProfitGastoId = gasto.Value!.Id;
        p.EstadoCodigo = PlanillaEstados.Cerrada;
        p.UpdatedAt = DateTime.UtcNow; p.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "CERRAR_PLANILLA", $"{p.Mes:00}/{p.Anio} Q{p.Quincena} → Gasto #{gasto.Value.Id}", p.Id);
        return Result.Ok();
    }

    public async Task<Result> AnularAsync(int empresaId, int id, string? actor, CancellationToken ct = default)
    {
        var p = await _db.PlanillaPeriodos.FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        if (p is null) return Result.Fail("Planilla no encontrada.", "PLANILLA_NOT_FOUND");
        if (p.EstadoCodigo == PlanillaEstados.Anulada) return Result.Fail("La planilla ya está anulada.", "INVALID_STATE");

        if (p.ProfitGastoId is int gastoId)
            await _profit.InactivarGastoAsync(empresaId, gastoId, actor, ct); // best-effort: revierte el gasto

        p.EstadoCodigo = PlanillaEstados.Anulada;
        p.ProfitGastoId = null;
        p.UpdatedAt = DateTime.UtcNow; p.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "ANULAR_PLANILLA", $"{p.Mes:00}/{p.Anio} Q{p.Quincena}", p.Id);
        return Result.Ok();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task GenerarDetallesAsync(int empresaId, PlanillaPeriodo periodo, int quincena, CancellationToken ct)
    {
        var empleados = await _db.Empleados.AsNoTracking()
            .Where(e => e.EmpresaId == empresaId && e.EstadoCodigo == "ACTIVO")
            .Select(e => new
            {
                e.Id, e.Codigo, e.Nombres, e.Apellidos,
                Salario = e.Contratos.Where(c => c.EstadoCodigo == ContratoEstados.Vigente).Select(c => c.SalarioMensual).FirstOrDefault(),
            })
            .Where(e => e.Salario > 0)
            .ToListAsync(ct);

        decimal devengado = 0, deducciones = 0, neto = 0, costo = 0;
        foreach (var e in empleados)
        {
            var r = quincena == 0 ? _calc.CalcularMensual(e.Salario, _nomina) : _calc.CalcularQuincena(e.Salario, _nomina);
            periodo.Detalles.Add(new PlanillaDetalle
            {
                EmpleadoId = e.Id, EmpleadoCodigo = e.Codigo, EmpleadoNombre = $"{e.Nombres} {e.Apellidos}",
                SalarioMensual = e.Salario, Devengado = r.SalarioBruto,
                Isss = r.IsssEmpleado, Afp = r.AfpEmpleado, Renta = r.Renta, OtrosDescuentos = 0m,
                TotalDeducciones = r.TotalDeduccionesEmpleado, SalarioNeto = r.SalarioNeto,
                IsssPatronal = r.IsssPatronal, AfpPatronal = r.AfpPatronal, CostoPatronal = r.CostoPatronal,
                CreatedBy = periodo.CreatedBy,
            });
            devengado += r.SalarioBruto; deducciones += r.TotalDeduccionesEmpleado; neto += r.SalarioNeto; costo += r.CostoPatronal;
        }
        periodo.TotalDevengado = devengado;
        periodo.TotalDeducciones = deducciones;
        periodo.TotalNeto = neto;
        periodo.TotalCostoPatronal = costo;
    }

    private static (DateOnly inicio, DateOnly fin) RangoFechas(int anio, int mes, int quincena)
    {
        var dias = DateTime.DaysInMonth(anio, mes);
        return quincena switch
        {
            1 => (new DateOnly(anio, mes, 1), new DateOnly(anio, mes, 15)),
            2 => (new DateOnly(anio, mes, 16), new DateOnly(anio, mes, dias)),
            _ => (new DateOnly(anio, mes, 1), new DateOnly(anio, mes, dias)),
        };
    }

    private static Result<PlanillaPeriodoDetalleDto> Fail(string msg, string code = "VALIDATION")
        => Result<PlanillaPeriodoDetalleDto>.Fail(msg, code);

    private static PlanillaPeriodoDetalleDto ToDetalle(PlanillaPeriodo p) => new()
    {
        Id = p.Id, Anio = p.Anio, Mes = p.Mes, Quincena = p.Quincena,
        FechaInicio = p.FechaInicio, FechaFin = p.FechaFin, EstadoCodigo = p.EstadoCodigo, ProfitGastoId = p.ProfitGastoId,
        TotalDevengado = p.TotalDevengado, TotalDeducciones = p.TotalDeducciones, TotalNeto = p.TotalNeto, TotalCostoPatronal = p.TotalCostoPatronal,
        Detalles = p.Detalles.OrderBy(d => d.EmpleadoCodigo).Select(d => new PlanillaDetalleDto
        {
            EmpleadoCodigo = d.EmpleadoCodigo, EmpleadoNombre = d.EmpleadoNombre, SalarioMensual = d.SalarioMensual,
            Devengado = d.Devengado, Isss = d.Isss, Afp = d.Afp, Renta = d.Renta, OtrosDescuentos = d.OtrosDescuentos,
            TotalDeducciones = d.TotalDeducciones, SalarioNeto = d.SalarioNeto,
            IsssPatronal = d.IsssPatronal, AfpPatronal = d.AfpPatronal, CostoPatronal = d.CostoPatronal,
        }).ToList(),
    };

    private Task Audit(int empresaId, string? actor, string accion, string detalle, int entidadId)
        => _auditoria.RegistrarAsync(new AuditoriaEvent
        {
            EmpresaId = empresaId, Username = actor, Modulo = AuditModule, Accion = accion,
            Entidad = "PlanillaPeriodo", EntidadId = entidadId.ToString(), Resultado = "OK", Detalle = detalle,
        });
}
