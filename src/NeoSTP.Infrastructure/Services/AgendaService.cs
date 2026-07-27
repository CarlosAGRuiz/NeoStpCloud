using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Agenda;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Connect;
using NeoSTP.Domain.Core.Agenda;
using NeoSTP.Domain.Core.Connect;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

public class AgendaService : IAgendaService
{
    private const string AuditModule = "AGENDA";

    private readonly NeoStpDbContext _db;
    private readonly IAuditoriaService _auditoria;

    private readonly IConnectWebhookDispatcher? _webhooks;

    public AgendaService(
        NeoStpDbContext db,
        IAuditoriaService auditoria,
        IConnectWebhookDispatcher? webhooks = null)
    {
        _db = db;
        _auditoria = auditoria;
        _webhooks = webhooks;
    }

    public async Task<Result<IReadOnlyList<CitaDto>>> ListAsync(int empresaId, DateTime desde, DateTime hasta, int? empleadoId = null, CancellationToken ct = default)
    {
        var q = _db.Citas.AsNoTracking()
            .Where(c => c.EmpresaId == empresaId && c.FechaInicio >= desde && c.FechaInicio < hasta);
        if (empleadoId is int eid) q = q.Where(c => c.EmpleadoId == eid);

        var items = await q.OrderBy(c => c.FechaInicio).Select(c => ToDto(c)).ToListAsync(ct);
        return Result<IReadOnlyList<CitaDto>>.Ok(items);
    }

    public async Task<Result<CitaDto>> CrearAsync(int empresaId, CrearCitaRequest request, string? actor, CancellationToken ct = default)
    {
        if (request.DuracionMinutos is < 5 or > 480)
            return Result<CitaDto>.Fail("La duración debe estar entre 5 minutos y 8 horas.", "VALIDATION");

        // Cliente: del catálogo o texto libre.
        string clienteNombre;
        if (request.ClienteId is int cid)
        {
            var cliente = await _db.Clientes.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == cid && c.EmpresaId == empresaId, ct);
            if (cliente is null) return Result<CitaDto>.Fail("Cliente no encontrado.", "CLIENTE_NOT_FOUND");
            clienteNombre = cliente.Nombre;
        }
        else if (!string.IsNullOrWhiteSpace(request.ClienteNombre))
        {
            clienteNombre = request.ClienteNombre.Trim();
        }
        else
        {
            return Result<CitaDto>.Fail("Indica el cliente (del catálogo o su nombre).", "VALIDATION");
        }

        // Servicio: producto tipo SERVICIO o texto libre; el precio se congela.
        string servicioNombre;
        var precio = request.Precio ?? 0m;
        if (request.ServicioProductoId is int spid)
        {
            var servicio = await _db.Productos.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == spid && p.EmpresaId == empresaId, ct);
            if (servicio is null) return Result<CitaDto>.Fail("Servicio no encontrado.", "SERVICIO_NOT_FOUND");
            servicioNombre = servicio.Nombre;
            if (request.Precio is null) precio = servicio.PrecioUnitario;
        }
        else if (!string.IsNullOrWhiteSpace(request.ServicioNombre))
        {
            servicioNombre = request.ServicioNombre.Trim();
        }
        else
        {
            return Result<CitaDto>.Fail("Indica el servicio (del catálogo o su nombre).", "VALIDATION");
        }

        string? empleadoNombre = null;
        if (request.EmpleadoId is int empId)
        {
            var empleado = await _db.Empleados.AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == empId && e.EmpresaId == empresaId, ct);
            if (empleado is null) return Result<CitaDto>.Fail("Empleado no encontrado.", "EMPLEADO_NOT_FOUND");
            empleadoNombre = $"{empleado.Nombres} {empleado.Apellidos}".Trim();

            var traslape = await ExisteTraslapeAsync(empresaId, empId, request.FechaInicio,
                request.FechaInicio.AddMinutes(request.DuracionMinutos), excluirCitaId: null, ct);
            if (traslape is not null)
                return Result<CitaDto>.Fail(
                    $"El empleado ya tiene la cita '{traslape}' en esa franja.", "CITA_TRASLAPADA");
        }

        var cita = new Cita
        {
            EmpresaId = empresaId,
            ClienteId = request.ClienteId, ClienteNombre = clienteNombre,
            EmpleadoId = request.EmpleadoId, EmpleadoNombre = empleadoNombre,
            ServicioProductoId = request.ServicioProductoId, ServicioNombre = servicioNombre,
            Precio = precio,
            FechaInicio = request.FechaInicio, DuracionMinutos = request.DuracionMinutos,
            EstadoCodigo = CitaEstados.Programada,
            Nota = request.Nota?.Trim(),
            CreatedAt = DateTime.UtcNow, CreatedBy = actor,
        };
        _db.Citas.Add(cita);
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "CREAR", $"{clienteNombre} — {servicioNombre} {cita.FechaInicio:dd/MM HH:mm}", cita.Id);

        // E6: permite enganchar recordatorios por WhatsApp o sincronizar un calendario externo.
        if (_webhooks is not null)
        {
            await _webhooks.DispatchNegocioAsync(new ConnectEventoNegocioPayload
            {
                Evento = ConnectEventos.AgendaCitaCreada,
                EmpresaId = empresaId,
                EntidadTipo = "Cita",
                EntidadId = cita.Id,
                Descripcion = $"{clienteNombre} — {servicioNombre} el {cita.FechaInicio:dd/MM/yyyy HH:mm}.",
                Datos = new Dictionary<string, object?>
                {
                    ["cliente"] = clienteNombre,
                    ["servicio"] = servicioNombre,
                    ["fechaInicio"] = cita.FechaInicio,
                    ["duracionMinutos"] = cita.DuracionMinutos,
                    ["empleadoId"] = cita.EmpleadoId,
                    ["precio"] = cita.Precio,
                },
            }, ct);
        }

        return Result<CitaDto>.Ok(ToDto(cita));
    }

    public async Task<Result<CitaDto>> ReprogramarAsync(int empresaId, int id, DateTime nuevaFechaInicio, int? duracionMinutos, string? actor, CancellationToken ct = default)
    {
        var cita = await _db.Citas.FirstOrDefaultAsync(c => c.Id == id && c.EmpresaId == empresaId, ct);
        if (cita is null) return Result<CitaDto>.Fail("Cita no encontrada.", "CITA_NOT_FOUND");
        if (!CitaEstados.Activos.Contains(cita.EstadoCodigo))
            return Result<CitaDto>.Fail("Solo se reprograman citas programadas o confirmadas.", "INVALID_STATE");

        var duracion = duracionMinutos ?? cita.DuracionMinutos;
        if (cita.EmpleadoId is int empId)
        {
            var traslape = await ExisteTraslapeAsync(empresaId, empId, nuevaFechaInicio,
                nuevaFechaInicio.AddMinutes(duracion), excluirCitaId: id, ct);
            if (traslape is not null)
                return Result<CitaDto>.Fail($"El empleado ya tiene la cita '{traslape}' en esa franja.", "CITA_TRASLAPADA");
        }

        cita.FechaInicio = nuevaFechaInicio;
        cita.DuracionMinutos = duracion;
        cita.UpdatedAt = DateTime.UtcNow; cita.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "REPROGRAMAR", $"Cita {id} → {nuevaFechaInicio:dd/MM HH:mm}", id);
        return Result<CitaDto>.Ok(ToDto(cita));
    }

    public async Task<Result<CitaDto>> CambiarEstadoAsync(int empresaId, int id, string estado, string? actor, CancellationToken ct = default)
    {
        var nuevo = (estado ?? "").Trim().ToUpperInvariant();
        if (!CitaEstados.All.Contains(nuevo))
            return Result<CitaDto>.Fail($"Estado inválido: {estado}.", "VALIDATION");

        var cita = await _db.Citas.FirstOrDefaultAsync(c => c.Id == id && c.EmpresaId == empresaId, ct);
        if (cita is null) return Result<CitaDto>.Fail("Cita no encontrada.", "CITA_NOT_FOUND");

        cita.EstadoCodigo = nuevo;
        cita.UpdatedAt = DateTime.UtcNow; cita.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "ESTADO", $"Cita {id} → {nuevo}", id);
        return Result<CitaDto>.Ok(ToDto(cita));
    }

    public async Task<Result<IReadOnlyList<ComisionEmpleadoDto>>> ComisionesAsync(int empresaId, DateOnly desde, DateOnly hasta, CancellationToken ct = default)
    {
        var d = desde.ToDateTime(TimeOnly.MinValue);
        var h = hasta.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var filas = await (
                from c in _db.Citas.AsNoTracking()
                join e in _db.Empleados.AsNoTracking() on c.EmpleadoId equals e.Id
                where c.EmpresaId == empresaId && c.EstadoCodigo == CitaEstados.Completada
                      && c.FechaInicio >= d && c.FechaInicio < h
                select new { c.EmpleadoId, e.Nombres, e.Apellidos, e.ComisionPorcentaje, c.Precio })
            .ToListAsync(ct);

        var items = filas
            .GroupBy(f => new { f.EmpleadoId, f.Nombres, f.Apellidos, f.ComisionPorcentaje })
            .Select(g =>
            {
                var pct = g.Key.ComisionPorcentaje ?? 0m;
                var total = g.Sum(x => x.Precio);
                return new ComisionEmpleadoDto
                {
                    EmpleadoId = g.Key.EmpleadoId!.Value,
                    EmpleadoNombre = $"{g.Key.Nombres} {g.Key.Apellidos}".Trim(),
                    ComisionPorcentaje = pct,
                    CitasCompletadas = g.Count(),
                    TotalServicios = total,
                    MontoComision = Math.Round(total * pct / 100m, 2, MidpointRounding.AwayFromZero),
                };
            })
            .OrderByDescending(x => x.MontoComision)
            .ToList();

        return Result<IReadOnlyList<ComisionEmpleadoDto>>.Ok(items);
    }

    /// <summary>Nombre del servicio de la cita activa que traslapa la franja, o null.</summary>
    private async Task<string?> ExisteTraslapeAsync(int empresaId, int empleadoId, DateTime inicio, DateTime fin, int? excluirCitaId, CancellationToken ct)
    {
        var candidatas = await _db.Citas.AsNoTracking()
            .Where(c => c.EmpresaId == empresaId && c.EmpleadoId == empleadoId
                     && CitaEstados.Activos.Contains(c.EstadoCodigo)
                     && (excluirCitaId == null || c.Id != excluirCitaId)
                     && c.FechaInicio < fin)
            .Select(c => new { c.FechaInicio, c.DuracionMinutos, c.ServicioNombre })
            .ToListAsync(ct);

        return candidatas.FirstOrDefault(c => c.FechaInicio.AddMinutes(c.DuracionMinutos) > inicio)?.ServicioNombre;
    }

    private static CitaDto ToDto(Cita c) => new()
    {
        Id = c.Id,
        ClienteId = c.ClienteId, ClienteNombre = c.ClienteNombre,
        EmpleadoId = c.EmpleadoId, EmpleadoNombre = c.EmpleadoNombre,
        ServicioProductoId = c.ServicioProductoId, ServicioNombre = c.ServicioNombre,
        Precio = c.Precio,
        FechaInicio = c.FechaInicio, DuracionMinutos = c.DuracionMinutos, FechaFin = c.FechaFin,
        EstadoCodigo = c.EstadoCodigo, Nota = c.Nota,
    };

    private Task Audit(int empresaId, string? actor, string accion, string detalle, int entidadId)
        => _auditoria.RegistrarAsync(new AuditoriaEvent
        {
            EmpresaId = empresaId, Username = actor, Modulo = AuditModule, Accion = accion,
            Entidad = "Cita", EntidadId = entidadId.ToString(), Resultado = "OK", Detalle = detalle,
        });
}
