using System.Data;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Rrhh;
using NeoSTP.Application.Rrhh.Dtos;
using NeoSTP.Domain.Core.Rrhh;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

/// <summary>V3-S3 - vacaciones y aguinaldo con politica por empresa.</summary>
public sealed class PrestacionesRrhhService : IPrestacionesRrhhService
{
    private const string AuditModule = "NEORRHH";
    private readonly NeoStpDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public PrestacionesRrhhService(NeoStpDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    public async Task<Result<PoliticaPrestacionesDto>> GetPoliticaAsync(int empresaId, CancellationToken ct = default)
    {
        var politica = await _db.PoliticasPrestaciones.AsNoTracking()
            .FirstOrDefaultAsync(x => x.EmpresaId == empresaId, ct) ?? DefaultPolitica(empresaId);
        return Result<PoliticaPrestacionesDto>.Ok(ToPolitica(politica));
    }

    public async Task<Result<PoliticaPrestacionesDto>> UpdatePoliticaAsync(
        int empresaId, UpdatePoliticaPrestacionesRequest request, string? actor, CancellationToken ct = default)
    {
        if (request.MesesParaVacacion is < 1 or > 60
            || request.DiasVacacionAnuales is < 1 or > 60
            || request.PrimaVacacionPorcentaje is < 0m or > 2m
            || request.AguinaldoAniosTramoMedio is < 1 or > 50
            || request.AguinaldoAniosTramoLargo is < 2 or > 60
            || request.AguinaldoDiasTramoCorto is < 1m or > 60m
            || request.AguinaldoDiasTramoMedio is < 1m or > 60m
            || request.AguinaldoDiasTramoLargo is < 1m or > 60m)
            return Result<PoliticaPrestacionesDto>.Fail("Los parametros de prestaciones estan fuera de rango.", "VALIDATION");
        if (request.AguinaldoAniosTramoMedio >= request.AguinaldoAniosTramoLargo)
            return Result<PoliticaPrestacionesDto>.Fail("El tramo medio debe iniciar antes que el tramo largo.", "VALIDATION");
        if (request.AguinaldoDiasTramoCorto > request.AguinaldoDiasTramoMedio
            || request.AguinaldoDiasTramoMedio > request.AguinaldoDiasTramoLargo)
            return Result<PoliticaPrestacionesDto>.Fail("Los dias de aguinaldo deben crecer por antiguedad.", "VALIDATION");
        if (!FechaValida(2024, request.AguinaldoMesPago, request.AguinaldoDiaPago))
            return Result<PoliticaPrestacionesDto>.Fail("La fecha de pago de aguinaldo no es valida.", "VALIDATION");

        var politica = await _db.PoliticasPrestaciones.FirstOrDefaultAsync(x => x.EmpresaId == empresaId, ct);
        if (politica is null)
        {
            politica = new PoliticaPrestaciones { EmpresaId = empresaId, CreatedBy = actor };
            _db.PoliticasPrestaciones.Add(politica);
        }
        else
        {
            politica.UpdatedAt = DateTime.UtcNow;
            politica.UpdatedBy = actor;
        }

        politica.VigenteDesde = request.VigenteDesde ?? DateOnly.FromDateTime(DateTime.UtcNow);
        politica.MesesParaVacacion = request.MesesParaVacacion;
        politica.DiasVacacionAnuales = request.DiasVacacionAnuales;
        politica.PrimaVacacionPorcentaje = request.PrimaVacacionPorcentaje;
        politica.AguinaldoAniosTramoMedio = request.AguinaldoAniosTramoMedio;
        politica.AguinaldoAniosTramoLargo = request.AguinaldoAniosTramoLargo;
        politica.AguinaldoDiasTramoCorto = request.AguinaldoDiasTramoCorto;
        politica.AguinaldoDiasTramoMedio = request.AguinaldoDiasTramoMedio;
        politica.AguinaldoDiasTramoLargo = request.AguinaldoDiasTramoLargo;
        politica.AguinaldoMesPago = request.AguinaldoMesPago;
        politica.AguinaldoDiaPago = request.AguinaldoDiaPago;

        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "CONFIGURAR_PRESTACIONES", "Politica de vacaciones y aguinaldo", politica.Id);
        return Result<PoliticaPrestacionesDto>.Ok(ToPolitica(politica));
    }

    public async Task<Result<VacacionResumenEmpleadoDto>> GetVacacionResumenAsync(
        int empresaId, int empleadoId, DateOnly? fechaCorte = null, CancellationToken ct = default)
    {
        var empleado = await _db.Empleados.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == empleadoId && x.EmpresaId == empresaId, ct);
        if (empleado is null)
            return Result<VacacionResumenEmpleadoDto>.Fail("Empleado no encontrado.", "EMPLEADO_NOT_FOUND");
        var politica = await GetPoliticaEntity(empresaId, ct);
        return Result<VacacionResumenEmpleadoDto>.Ok(await BuildResumen(empleado, politica, fechaCorte, null, ct));
    }

    public async Task<Result<PagedResult<SolicitudVacacionDto>>> ListVacacionesAsync(
        int empresaId, int? empleadoId, string? estado, PagedQuery query, CancellationToken ct = default)
    {
        var q = _db.SolicitudesVacacion.AsNoTracking().Where(x => x.EmpresaId == empresaId);
        if (empleadoId is int eid) q = q.Where(x => x.EmpleadoId == eid);
        if (!string.IsNullOrWhiteSpace(estado))
        {
            var normalizado = estado.Trim().ToUpperInvariant();
            if (!VacacionEstados.All.Contains(normalizado))
                return Result<PagedResult<SolicitudVacacionDto>>.Fail("Estado de vacacion invalido.", "VALIDATION");
            q = q.Where(x => x.EstadoCodigo == normalizado);
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            q = q.Where(x => x.Empleado.Codigo.Contains(search)
                || x.Empleado.Nombres.Contains(search) || x.Empleado.Apellidos.Contains(search));
        }

        var total = await q.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var items = await q.OrderByDescending(x => x.FechaInicio).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new SolicitudVacacionDto
            {
                Id = x.Id, EmpleadoId = x.EmpleadoId, EmpleadoCodigo = x.Empleado.Codigo,
                EmpleadoNombre = x.Empleado.Nombres + " " + x.Empleado.Apellidos,
                FechaInicio = x.FechaInicio, FechaFin = x.FechaFin, Dias = x.Dias,
                PrimaMonto = x.PrimaMonto, EstadoCodigo = x.EstadoCodigo, Motivo = x.Motivo,
                ResolucionNota = x.ResolucionNota, PlanillaPeriodoId = x.PlanillaPeriodoId,
            }).ToListAsync(ct);
        return Result<PagedResult<SolicitudVacacionDto>>.Ok(
            PagedResult<SolicitudVacacionDto>.Create(items, total, page, pageSize));
    }

    public async Task<Result<SolicitudVacacionDto>> SolicitarVacacionAsync(
        int empresaId, CrearSolicitudVacacionRequest request, string? actor, CancellationToken ct = default)
    {
        if (request.Motivo?.Length > 500)
            return Result<SolicitudVacacionDto>.Fail("El motivo no puede exceder 500 caracteres.", "VALIDATION");
        if (request.FechaFin < request.FechaInicio)
            return Result<SolicitudVacacionDto>.Fail("La fecha final no puede ser anterior al inicio.", "VALIDATION");
        var dias = request.FechaFin.DayNumber - request.FechaInicio.DayNumber + 1;
        var empleado = await _db.Empleados.FirstOrDefaultAsync(
            x => x.Id == request.EmpleadoId && x.EmpresaId == empresaId, ct);
        if (empleado is null)
            return Result<SolicitudVacacionDto>.Fail("Empleado no encontrado.", "EMPLEADO_NOT_FOUND");
        if (empleado.EstadoCodigo != "ACTIVO")
            return Result<SolicitudVacacionDto>.Fail("El empleado esta inactivo.", "INVALID_STATE");
        if (request.FechaInicio < empleado.FechaIngreso)
            return Result<SolicitudVacacionDto>.Fail("La vacacion no puede iniciar antes del ingreso.", "VALIDATION");

        var traslape = await _db.SolicitudesVacacion.AnyAsync(x => x.EmpresaId == empresaId
            && x.EmpleadoId == empleado.Id
            && (x.EstadoCodigo == VacacionEstados.Solicitada || x.EstadoCodigo == VacacionEstados.Aprobada)
            && x.FechaInicio <= request.FechaFin && x.FechaFin >= request.FechaInicio, ct);
        if (traslape)
            return Result<SolicitudVacacionDto>.Fail("El empleado ya tiene una solicitud que se traslapa.", "VACACION_TRASLAPE");

        var politica = await GetPoliticaEntity(empresaId, ct);
        var resumen = await BuildResumen(empleado, politica, request.FechaInicio, null, ct);
        if (dias > resumen.DiasDisponibles)
            return Result<SolicitudVacacionDto>.Fail(
                $"La solicitud excede el saldo disponible ({resumen.DiasDisponibles} dias).", "VACACION_SALDO_INSUFICIENTE");

        var solicitud = new SolicitudVacacion
        {
            EmpresaId = empresaId, EmpleadoId = empleado.Id, Empleado = empleado,
            FechaInicio = request.FechaInicio, FechaFin = request.FechaFin, Dias = dias,
            EstadoCodigo = VacacionEstados.Solicitada, Motivo = request.Motivo?.Trim(), CreatedBy = actor,
        };
        _db.SolicitudesVacacion.Add(solicitud);
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "SOLICITAR_VACACION", $"{empleado.Codigo}: {dias} dias", solicitud.Id);
        return Result<SolicitudVacacionDto>.Ok(ToVacacion(solicitud));
    }

    public Task<Result<SolicitudVacacionDto>> AprobarVacacionAsync(
        int empresaId, int id, ResolverSolicitudVacacionRequest request, string? actor, CancellationToken ct = default)
        => ResolverVacacion(empresaId, id, request, VacacionEstados.Aprobada, actor, ct);

    public Task<Result<SolicitudVacacionDto>> RechazarVacacionAsync(
        int empresaId, int id, ResolverSolicitudVacacionRequest request, string? actor, CancellationToken ct = default)
        => ResolverVacacion(empresaId, id, request, VacacionEstados.Rechazada, actor, ct);

    public async Task<Result> CancelarVacacionAsync(
        int empresaId, int id, string? actor, CancellationToken ct = default)
    {
        var solicitud = await _db.SolicitudesVacacion.FirstOrDefaultAsync(
            x => x.Id == id && x.EmpresaId == empresaId, ct);
        if (solicitud is null) return Result.Fail("Solicitud no encontrada.", "VACACION_NOT_FOUND");
        if (solicitud.EstadoCodigo is VacacionEstados.Rechazada or VacacionEstados.Cancelada)
            return Result.Fail("La solicitud no puede cancelarse en su estado actual.", "INVALID_STATE");
        if (solicitud.PlanillaPeriodoId is not null)
            return Result.Fail("La prima ya esta vinculada a una planilla; anule o recalcule esa corrida primero.", "INVALID_STATE");
        solicitud.EstadoCodigo = VacacionEstados.Cancelada;
        solicitud.UpdatedAt = DateTime.UtcNow;
        solicitud.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "CANCELAR_VACACION", solicitud.EmpleadoId.ToString(), solicitud.Id);
        return Result.Ok();
    }

    public async Task<Result<List<AguinaldoCalculoDto>>> CalcularAguinaldosAsync(
        int empresaId, int anio, string? actor, CancellationToken ct = default)
    {
        if (anio is < 2000 or > 2100)
            return Result<List<AguinaldoCalculoDto>>.Fail("Anio invalido.", "VALIDATION");
        await using var transaction = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
            : null;
        var politica = await GetPoliticaEntity(empresaId, ct);
        var corte = FechaPago(anio, politica);
        var empleados = await _db.Empleados.Include(x => x.Contratos)
            .Where(x => x.EmpresaId == empresaId && x.EstadoCodigo == "ACTIVO" && x.FechaIngreso <= corte)
            .ToListAsync(ct);
        var existentes = await _db.AguinaldosCalculados
            .Where(x => x.EmpresaId == empresaId && x.Anio == anio)
            .ToDictionaryAsync(x => x.EmpleadoId, ct);

        foreach (var empleado in empleados)
        {
            var salario = empleado.Contratos.Where(x => x.EstadoCodigo == ContratoEstados.Vigente)
                .OrderByDescending(x => x.FechaInicio).Select(x => x.SalarioMensual).FirstOrDefault();
            if (salario <= 0) continue;
            var calculo = PrestacionesCalculator.CalcularAguinaldo(
                empleado.FechaIngreso, corte, salario,
                politica.AguinaldoAniosTramoMedio, politica.AguinaldoAniosTramoLargo,
                politica.AguinaldoDiasTramoCorto, politica.AguinaldoDiasTramoMedio,
                politica.AguinaldoDiasTramoLargo);
            if (calculo.Monto <= 0) continue;

            if (!existentes.TryGetValue(empleado.Id, out var item))
            {
                item = new AguinaldoCalculo
                {
                    EmpresaId = empresaId, EmpleadoId = empleado.Id, Empleado = empleado,
                    Anio = anio, EstadoCodigo = AguinaldoEstados.Calculado, CreatedBy = actor,
                };
                _db.AguinaldosCalculados.Add(item);
                existentes[empleado.Id] = item;
            }
            else if (item.EstadoCodigo is AguinaldoEstados.Aprobado or AguinaldoEstados.Pagado)
            {
                continue;
            }

            item.FechaCorte = corte;
            item.AntiguedadAnios = calculo.AntiguedadAnios;
            item.SalarioMensual = salario;
            item.DiasCalculados = calculo.Dias;
            item.Monto = calculo.Monto;
            item.EstadoCodigo = AguinaldoEstados.Calculado;
            item.UpdatedAt = item.Id == 0 ? null : DateTime.UtcNow;
            item.UpdatedBy = item.Id == 0 ? null : actor;
        }

        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "CALCULAR_AGUINALDO", $"{anio}: {existentes.Count} empleados", anio);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return await ListAguinaldosAsync(empresaId, anio, ct);
    }

    public async Task<Result<List<AguinaldoCalculoDto>>> ListAguinaldosAsync(
        int empresaId, int anio, CancellationToken ct = default)
    {
        if (anio is < 2000 or > 2100)
            return Result<List<AguinaldoCalculoDto>>.Fail("Anio invalido.", "VALIDATION");
        var items = await _db.AguinaldosCalculados.AsNoTracking()
            .Where(x => x.EmpresaId == empresaId && x.Anio == anio)
            .OrderBy(x => x.Empleado.Apellidos).ThenBy(x => x.Empleado.Nombres)
            .Select(x => new AguinaldoCalculoDto
            {
                Id = x.Id, EmpleadoId = x.EmpleadoId, EmpleadoCodigo = x.Empleado.Codigo,
                EmpleadoNombre = x.Empleado.Nombres + " " + x.Empleado.Apellidos,
                Anio = x.Anio, FechaCorte = x.FechaCorte, AntiguedadAnios = x.AntiguedadAnios,
                SalarioMensual = x.SalarioMensual, DiasCalculados = x.DiasCalculados,
                Monto = x.Monto, EstadoCodigo = x.EstadoCodigo, PlanillaPeriodoId = x.PlanillaPeriodoId,
            }).ToListAsync(ct);
        return Result<List<AguinaldoCalculoDto>>.Ok(items);
    }

    public async Task<Result<int>> AprobarAguinaldosAsync(
        int empresaId, int anio, string? actor, CancellationToken ct = default)
    {
        if (anio is < 2000 or > 2100)
            return Result<int>.Fail("Anio invalido.", "VALIDATION");
        var items = await _db.AguinaldosCalculados.Where(x => x.EmpresaId == empresaId
            && x.Anio == anio && x.EstadoCodigo == AguinaldoEstados.Calculado).ToListAsync(ct);
        foreach (var item in items)
        {
            item.EstadoCodigo = AguinaldoEstados.Aprobado;
            item.UpdatedAt = DateTime.UtcNow;
            item.UpdatedBy = actor;
        }
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "APROBAR_AGUINALDO", $"{anio}: {items.Count} empleados", anio);
        return Result<int>.Ok(items.Count);
    }

    private async Task<Result<SolicitudVacacionDto>> ResolverVacacion(
        int empresaId, int id, ResolverSolicitudVacacionRequest request, string estado,
        string? actor, CancellationToken ct)
    {
        if (request.Nota?.Length > 500)
            return Result<SolicitudVacacionDto>.Fail("La nota no puede exceder 500 caracteres.", "VALIDATION");
        await using var transaction = estado == VacacionEstados.Aprobada && _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
            : null;
        var solicitud = await _db.SolicitudesVacacion.Include(x => x.Empleado).ThenInclude(x => x.Contratos)
            .FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        if (solicitud is null)
            return Result<SolicitudVacacionDto>.Fail("Solicitud no encontrada.", "VACACION_NOT_FOUND");
        if (solicitud.EstadoCodigo != VacacionEstados.Solicitada)
            return Result<SolicitudVacacionDto>.Fail("Solo una solicitud pendiente puede resolverse.", "INVALID_STATE");

        if (estado == VacacionEstados.Aprobada)
        {
            var politica = await GetPoliticaEntity(empresaId, ct);
            var resumen = await BuildResumen(solicitud.Empleado, politica, solicitud.FechaInicio, solicitud.Id, ct);
            if (solicitud.Dias > resumen.DiasDisponibles)
                return Result<SolicitudVacacionDto>.Fail(
                    $"La solicitud excede el saldo disponible ({resumen.DiasDisponibles} dias).", "VACACION_SALDO_INSUFICIENTE");
            var salario = solicitud.Empleado.Contratos.Where(x => x.EstadoCodigo == ContratoEstados.Vigente)
                .OrderByDescending(x => x.FechaInicio).Select(x => x.SalarioMensual).FirstOrDefault();
            if (salario <= 0)
                return Result<SolicitudVacacionDto>.Fail("El empleado no tiene contrato vigente con salario.", "CONTRATO_NOT_FOUND");
            solicitud.PrimaMonto = PrestacionesCalculator.PrimaVacacion(
                salario, solicitud.Dias, politica.PrimaVacacionPorcentaje);
        }

        solicitud.EstadoCodigo = estado;
        solicitud.ResolucionNota = request.Nota?.Trim();
        solicitud.ResueltaAt = DateTime.UtcNow;
        solicitud.ResueltaPor = actor;
        solicitud.UpdatedAt = DateTime.UtcNow;
        solicitud.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, estado == VacacionEstados.Aprobada ? "APROBAR_VACACION" : "RECHAZAR_VACACION",
            $"{solicitud.Empleado.Codigo}: {solicitud.Dias} dias", solicitud.Id);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return Result<SolicitudVacacionDto>.Ok(ToVacacion(solicitud));
    }

    private async Task<VacacionResumenEmpleadoDto> BuildResumen(
        Empleado empleado, PoliticaPrestaciones politica, DateOnly? fechaCorte, int? excluirSolicitudId, CancellationToken ct)
    {
        var corte = fechaCorte ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var usados = await _db.SolicitudesVacacion.AsNoTracking()
            .Where(x => x.EmpresaId == empleado.EmpresaId && x.EmpleadoId == empleado.Id
                && x.EstadoCodigo == VacacionEstados.Aprobada
                && (!excluirSolicitudId.HasValue || x.Id != excluirSolicitudId.Value))
            .SumAsync(x => (int?)x.Dias, ct) ?? 0;
        var devengados = PrestacionesCalculator.DiasVacacionDevengados(
            empleado.FechaIngreso, corte, politica.MesesParaVacacion, politica.DiasVacacionAnuales);
        return new VacacionResumenEmpleadoDto
        {
            EmpleadoId = empleado.Id, EmpleadoCodigo = empleado.Codigo,
            EmpleadoNombre = empleado.NombreCompleto, FechaIngreso = empleado.FechaIngreso,
            FechaCorte = corte, DiasDevengados = devengados, DiasAprobados = usados,
            DiasDisponibles = Math.Max(0, devengados - usados),
        };
    }

    private async Task<PoliticaPrestaciones> GetPoliticaEntity(int empresaId, CancellationToken ct)
        => await _db.PoliticasPrestaciones.AsNoTracking().FirstOrDefaultAsync(x => x.EmpresaId == empresaId, ct)
            ?? DefaultPolitica(empresaId);

    private static PoliticaPrestaciones DefaultPolitica(int empresaId) => new()
    {
        EmpresaId = empresaId,
        VigenteDesde = new DateOnly(DateTime.UtcNow.Year, 1, 1),
    };

    private static DateOnly FechaPago(int anio, PoliticaPrestaciones politica)
    {
        var dia = Math.Min(politica.AguinaldoDiaPago, DateTime.DaysInMonth(anio, politica.AguinaldoMesPago));
        return new DateOnly(anio, politica.AguinaldoMesPago, dia);
    }

    private static bool FechaValida(int anio, int mes, int dia)
        => mes is >= 1 and <= 12 && dia >= 1 && dia <= DateTime.DaysInMonth(anio, mes);

    private static PoliticaPrestacionesDto ToPolitica(PoliticaPrestaciones x) => new()
    {
        VigenteDesde = x.VigenteDesde, MesesParaVacacion = x.MesesParaVacacion,
        DiasVacacionAnuales = x.DiasVacacionAnuales, PrimaVacacionPorcentaje = x.PrimaVacacionPorcentaje,
        AguinaldoAniosTramoMedio = x.AguinaldoAniosTramoMedio, AguinaldoAniosTramoLargo = x.AguinaldoAniosTramoLargo,
        AguinaldoDiasTramoCorto = x.AguinaldoDiasTramoCorto, AguinaldoDiasTramoMedio = x.AguinaldoDiasTramoMedio,
        AguinaldoDiasTramoLargo = x.AguinaldoDiasTramoLargo, AguinaldoMesPago = x.AguinaldoMesPago,
        AguinaldoDiaPago = x.AguinaldoDiaPago,
    };

    private static SolicitudVacacionDto ToVacacion(SolicitudVacacion x) => new()
    {
        Id = x.Id, EmpleadoId = x.EmpleadoId, EmpleadoCodigo = x.Empleado?.Codigo ?? string.Empty,
        EmpleadoNombre = x.Empleado?.NombreCompleto ?? string.Empty, FechaInicio = x.FechaInicio,
        FechaFin = x.FechaFin, Dias = x.Dias, PrimaMonto = x.PrimaMonto, EstadoCodigo = x.EstadoCodigo,
        Motivo = x.Motivo, ResolucionNota = x.ResolucionNota, PlanillaPeriodoId = x.PlanillaPeriodoId,
    };

    private Task Audit(int empresaId, string? actor, string accion, string detalle, int id)
        => _auditoria.RegistrarAsync(new AuditoriaEvent
        {
            EmpresaId = empresaId, Username = actor, Modulo = AuditModule, Accion = accion,
            Entidad = "PrestacionLaboral", EntidadId = id.ToString(), Resultado = "OK", Detalle = detalle,
        });
}
