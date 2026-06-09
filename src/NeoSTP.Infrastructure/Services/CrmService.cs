using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Crm;
using NeoSTP.Application.Crm.Dtos;
using NeoSTP.Domain.Core.Crm;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

public class CrmService : ICrmService
{
    private const string AuditModule = "NEOCRM";

    private readonly NeoStpDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public CrmService(NeoStpDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    public async Task<Result<PagedResult<ContactoCrmDto>>> ListContactosAsync(int empresaId, PagedQuery query, CancellationToken ct = default)
    {
        var q = _db.ContactosCrm.AsNoTracking().Where(x => x.EmpresaId == empresaId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(x => x.Nombre.Contains(s) || (x.Email != null && x.Email.Contains(s)) || (x.Telefono != null && x.Telefono.Contains(s)));
        }

        var total = await q.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var contactos = await q.Include(x => x.Cliente)
            .OrderBy(x => x.EstadoCodigo == ContactoCrmEstados.Activo ? 0 : 1).ThenBy(x => x.Nombre)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);
        var items = contactos.Select(ToContactoDto).ToList();

        return Result<PagedResult<ContactoCrmDto>>.Ok(PagedResult<ContactoCrmDto>.Create(items, total, page, pageSize));
    }

    public async Task<Result<ContactoCrmDto>> GetContactoAsync(int empresaId, int id, CancellationToken ct = default)
    {
        var contacto = await _db.ContactosCrm.AsNoTracking()
            .Include(x => x.Cliente)
            .Where(x => x.EmpresaId == empresaId && x.Id == id)
            .FirstOrDefaultAsync(ct);
        return contacto is null
            ? Result<ContactoCrmDto>.Fail("Contacto no encontrado.", "CRM_CONTACTO_NOT_FOUND")
            : Result<ContactoCrmDto>.Ok(ToContactoDto(contacto));
    }

    public async Task<Result<ContactoCrmDto>> CrearContactoAsync(int empresaId, UpsertContactoCrmRequest request, string? actor, CancellationToken ct = default)
    {
        if (!ContactoCrmOrigenes.All.Contains(request.Origen))
            return Result<ContactoCrmDto>.Fail("Origen de contacto invalido.", "VALIDATION");

        if (request.ClienteId is int clienteId && !await ClienteExiste(empresaId, clienteId, ct))
            return Result<ContactoCrmDto>.Fail("Cliente no encontrado.", "CLIENTE_NOT_FOUND");

        var contacto = new ContactoCrm
        {
            EmpresaId = empresaId,
            ClienteId = request.ClienteId,
            Nombre = request.Nombre.Trim(),
            Cargo = request.Cargo?.Trim(),
            Email = request.Email?.Trim(),
            Telefono = request.Telefono?.Trim(),
            Origen = request.Origen,
            EstadoCodigo = ContactoCrmEstados.Activo,
            Notas = request.Notas?.Trim(),
            CreatedBy = actor,
        };

        _db.ContactosCrm.Add(contacto);
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "CREAR_CONTACTO", contacto.Nombre, "ContactoCrm", contacto.Id);
        return await GetContactoAsync(empresaId, contacto.Id, ct);
    }

    public async Task<Result<ContactoCrmDto>> ActualizarContactoAsync(int empresaId, int id, UpsertContactoCrmRequest request, string? actor, CancellationToken ct = default)
    {
        var contacto = await _db.ContactosCrm.FirstOrDefaultAsync(x => x.EmpresaId == empresaId && x.Id == id, ct);
        if (contacto is null) return Result<ContactoCrmDto>.Fail("Contacto no encontrado.", "CRM_CONTACTO_NOT_FOUND");
        if (!ContactoCrmOrigenes.All.Contains(request.Origen)) return Result<ContactoCrmDto>.Fail("Origen de contacto invalido.", "VALIDATION");
        if (request.ClienteId is int clienteId && !await ClienteExiste(empresaId, clienteId, ct))
            return Result<ContactoCrmDto>.Fail("Cliente no encontrado.", "CLIENTE_NOT_FOUND");

        contacto.ClienteId = request.ClienteId;
        contacto.Nombre = request.Nombre.Trim();
        contacto.Cargo = request.Cargo?.Trim();
        contacto.Email = request.Email?.Trim();
        contacto.Telefono = request.Telefono?.Trim();
        contacto.Origen = request.Origen;
        contacto.Notas = request.Notas?.Trim();
        contacto.UpdatedAt = DateTime.UtcNow;
        contacto.UpdatedBy = actor;

        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "EDITAR_CONTACTO", contacto.Nombre, "ContactoCrm", contacto.Id);
        return await GetContactoAsync(empresaId, contacto.Id, ct);
    }

    public async Task<Result> InactivarContactoAsync(int empresaId, int id, string? actor, CancellationToken ct = default)
    {
        var contacto = await _db.ContactosCrm.FirstOrDefaultAsync(x => x.EmpresaId == empresaId && x.Id == id, ct);
        if (contacto is null) return Result.Fail("Contacto no encontrado.", "CRM_CONTACTO_NOT_FOUND");
        contacto.EstadoCodigo = ContactoCrmEstados.Inactivo;
        contacto.UpdatedAt = DateTime.UtcNow;
        contacto.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "INACTIVAR_CONTACTO", contacto.Nombre, "ContactoCrm", contacto.Id);
        return Result.Ok();
    }

    public async Task<Result<IReadOnlyList<EtapaPipelineCrmDto>>> ListEtapasAsync(int empresaId, CancellationToken ct = default)
    {
        await EnsureDefaultEtapasAsync(empresaId, null, ct);
        var etapas = await _db.EtapasPipelineCrm.AsNoTracking()
            .Where(x => x.EmpresaId == empresaId)
            .OrderBy(x => x.Orden)
            .Select(x => ToEtapaDto(x))
            .ToListAsync(ct);
        return Result<IReadOnlyList<EtapaPipelineCrmDto>>.Ok(etapas);
    }

    public async Task<Result<EtapaPipelineCrmDto>> CrearEtapaAsync(int empresaId, UpsertEtapaPipelineCrmRequest request, string? actor, CancellationToken ct = default)
    {
        var codigo = request.Codigo.Trim().ToUpperInvariant();
        var dup = await _db.EtapasPipelineCrm.AnyAsync(x => x.EmpresaId == empresaId && x.Codigo == codigo, ct);
        if (dup) return Result<EtapaPipelineCrmDto>.Fail("Ya existe una etapa con ese codigo.", "DUPLICATE");

        var etapa = new EtapaPipelineCrm
        {
            EmpresaId = empresaId,
            Codigo = codigo,
            Nombre = request.Nombre.Trim(),
            Orden = request.Orden,
            ProbabilidadDefault = request.ProbabilidadDefault,
            Activa = request.Activa,
            EsCierreGanado = request.EsCierreGanado,
            EsCierrePerdido = request.EsCierrePerdido,
            CreatedBy = actor,
        };
        _db.EtapasPipelineCrm.Add(etapa);
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "CREAR_ETAPA", etapa.Codigo, "EtapaPipelineCrm", etapa.Id);
        return Result<EtapaPipelineCrmDto>.Ok(ToEtapaDto(etapa));
    }

    public async Task<Result<EtapaPipelineCrmDto>> ActualizarEtapaAsync(int empresaId, int id, UpsertEtapaPipelineCrmRequest request, string? actor, CancellationToken ct = default)
    {
        var etapa = await _db.EtapasPipelineCrm.FirstOrDefaultAsync(x => x.EmpresaId == empresaId && x.Id == id, ct);
        if (etapa is null) return Result<EtapaPipelineCrmDto>.Fail("Etapa no encontrada.", "CRM_ETAPA_NOT_FOUND");
        var codigo = request.Codigo.Trim().ToUpperInvariant();
        var dup = await _db.EtapasPipelineCrm.AnyAsync(x => x.EmpresaId == empresaId && x.Id != id && x.Codigo == codigo, ct);
        if (dup) return Result<EtapaPipelineCrmDto>.Fail("Ya existe una etapa con ese codigo.", "DUPLICATE");

        etapa.Codigo = codigo;
        etapa.Nombre = request.Nombre.Trim();
        etapa.Orden = request.Orden;
        etapa.ProbabilidadDefault = request.ProbabilidadDefault;
        etapa.Activa = request.Activa;
        etapa.EsCierreGanado = request.EsCierreGanado;
        etapa.EsCierrePerdido = request.EsCierrePerdido;
        etapa.UpdatedAt = DateTime.UtcNow;
        etapa.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "EDITAR_ETAPA", etapa.Codigo, "EtapaPipelineCrm", etapa.Id);
        return Result<EtapaPipelineCrmDto>.Ok(ToEtapaDto(etapa));
    }

    public async Task<Result<PagedResult<OportunidadCrmDto>>> ListOportunidadesAsync(int empresaId, string? estado, int? etapaId, int? clienteId, PagedQuery query, CancellationToken ct = default)
    {
        var q = _db.OportunidadesCrm.AsNoTracking().Where(x => x.EmpresaId == empresaId);
        if (!string.IsNullOrWhiteSpace(estado)) q = q.Where(x => x.EstadoCodigo == estado);
        if (etapaId is int eid) q = q.Where(x => x.EtapaPipelineCrmId == eid);
        if (clienteId is int cid) q = q.Where(x => x.ClienteId == cid);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(x => x.Titulo.Contains(s) || (x.Cliente != null && x.Cliente.Nombre.Contains(s)));
        }

        var total = await q.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var oportunidades = await q.Include(x => x.Cliente)
            .Include(x => x.Contacto)
            .Include(x => x.Etapa)
            .Include(x => x.Actividades)
            .OrderBy(x => x.EstadoCodigo == OportunidadCrmEstados.Abierta ? 0 : 1).ThenByDescending(x => x.MontoEstimado)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);
        var items = oportunidades.Select(ToOportunidadDto).ToList();
        return Result<PagedResult<OportunidadCrmDto>>.Ok(PagedResult<OportunidadCrmDto>.Create(items, total, page, pageSize));
    }

    public async Task<Result<OportunidadCrmDetalleDto>> GetOportunidadAsync(int empresaId, int id, CancellationToken ct = default)
    {
        var opp = await _db.OportunidadesCrm.AsNoTracking()
            .Include(x => x.Cliente)
            .Include(x => x.Contacto)
            .Include(x => x.Etapa)
            .Include(x => x.Actividades)
            .FirstOrDefaultAsync(x => x.EmpresaId == empresaId && x.Id == id, ct);
        return opp is null
            ? Result<OportunidadCrmDetalleDto>.Fail("Oportunidad no encontrada.", "CRM_OPORTUNIDAD_NOT_FOUND")
            : Result<OportunidadCrmDetalleDto>.Ok(ToOportunidadDetalleDto(opp));
    }

    public async Task<Result<OportunidadCrmDetalleDto>> CrearOportunidadAsync(int empresaId, CrearOportunidadCrmRequest request, string? actor, CancellationToken ct = default)
    {
        var refs = await ValidarReferenciasAsync(empresaId, request.ClienteId, request.ContactoCrmId, null, null, ct);
        if (refs.IsFailure) return Result<OportunidadCrmDetalleDto>.Fail(refs.Error!, refs.ErrorCode);

        var etapa = request.EtapaPipelineCrmId is int etapaId
            ? await _db.EtapasPipelineCrm.FirstOrDefaultAsync(x => x.EmpresaId == empresaId && x.Id == etapaId && x.Activa, ct)
            : await GetDefaultEtapaAsync(empresaId, actor, ct);
        if (etapa is null) return Result<OportunidadCrmDetalleDto>.Fail("Etapa no encontrada.", "CRM_ETAPA_NOT_FOUND");

        var clienteId = request.ClienteId ?? refs.Value!.ClienteIdDesdeContacto;
        var opp = new OportunidadCrm
        {
            EmpresaId = empresaId,
            ClienteId = clienteId,
            ContactoCrmId = request.ContactoCrmId,
            EtapaPipelineCrmId = etapa.Id,
            Titulo = request.Titulo.Trim(),
            Descripcion = request.Descripcion?.Trim(),
            MontoEstimado = decimal.Round(request.MontoEstimado, 2, MidpointRounding.AwayFromZero),
            Probabilidad = request.Probabilidad ?? etapa.ProbabilidadDefault,
            FechaApertura = DateOnly.FromDateTime(DateTime.UtcNow),
            FechaCierreEstimada = request.FechaCierreEstimada,
            EstadoCodigo = OportunidadCrmEstados.Abierta,
            CreatedBy = actor,
        };

        _db.OportunidadesCrm.Add(opp);
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "CREAR_OPORTUNIDAD", opp.Titulo, "OportunidadCrm", opp.Id);
        return await GetOportunidadAsync(empresaId, opp.Id, ct);
    }

    public async Task<Result<OportunidadCrmDetalleDto>> ActualizarOportunidadAsync(int empresaId, int id, ActualizarOportunidadCrmRequest request, string? actor, CancellationToken ct = default)
    {
        var opp = await _db.OportunidadesCrm.FirstOrDefaultAsync(x => x.EmpresaId == empresaId && x.Id == id, ct);
        if (opp is null) return Result<OportunidadCrmDetalleDto>.Fail("Oportunidad no encontrada.", "CRM_OPORTUNIDAD_NOT_FOUND");
        if (!OportunidadCrmEstados.All.Contains(request.EstadoCodigo)) return Result<OportunidadCrmDetalleDto>.Fail("Estado de oportunidad invalido.", "VALIDATION");

        var refs = await ValidarReferenciasAsync(empresaId, request.ClienteId, request.ContactoCrmId, request.DteDocumentoId, request.CuentaCobroId, ct);
        if (refs.IsFailure) return Result<OportunidadCrmDetalleDto>.Fail(refs.Error!, refs.ErrorCode);

        EtapaPipelineCrm? etapa = null;
        if (request.EtapaPipelineCrmId is int etapaId)
        {
            etapa = await _db.EtapasPipelineCrm.FirstOrDefaultAsync(x => x.EmpresaId == empresaId && x.Id == etapaId, ct);
            if (etapa is null) return Result<OportunidadCrmDetalleDto>.Fail("Etapa no encontrada.", "CRM_ETAPA_NOT_FOUND");
            opp.EtapaPipelineCrmId = etapa.Id;
        }

        opp.ClienteId = request.ClienteId ?? refs.Value!.ClienteIdDesdeContacto;
        opp.ContactoCrmId = request.ContactoCrmId;
        opp.Titulo = request.Titulo.Trim();
        opp.Descripcion = request.Descripcion?.Trim();
        opp.MontoEstimado = decimal.Round(request.MontoEstimado, 2, MidpointRounding.AwayFromZero);
        opp.Probabilidad = request.Probabilidad ?? etapa?.ProbabilidadDefault ?? opp.Probabilidad;
        opp.FechaCierreEstimada = request.FechaCierreEstimada;
        opp.EstadoCodigo = request.EstadoCodigo;
        opp.MotivoPerdida = request.MotivoPerdida?.Trim();
        opp.DteDocumentoId = request.DteDocumentoId;
        opp.CuentaCobroId = request.CuentaCobroId;
        opp.FechaCierreReal = request.EstadoCodigo is OportunidadCrmEstados.Ganada or OportunidadCrmEstados.Perdida
            ? DateOnly.FromDateTime(DateTime.UtcNow)
            : null;
        opp.UpdatedAt = DateTime.UtcNow;
        opp.UpdatedBy = actor;

        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "EDITAR_OPORTUNIDAD", opp.Titulo, "OportunidadCrm", opp.Id);
        return await GetOportunidadAsync(empresaId, opp.Id, ct);
    }

    public async Task<Result<OportunidadCrmDetalleDto>> CambiarEtapaAsync(int empresaId, int id, CambiarEtapaOportunidadRequest request, string? actor, CancellationToken ct = default)
    {
        var opp = await _db.OportunidadesCrm.FirstOrDefaultAsync(x => x.EmpresaId == empresaId && x.Id == id, ct);
        if (opp is null) return Result<OportunidadCrmDetalleDto>.Fail("Oportunidad no encontrada.", "CRM_OPORTUNIDAD_NOT_FOUND");
        var etapa = await _db.EtapasPipelineCrm.FirstOrDefaultAsync(x => x.EmpresaId == empresaId && x.Id == request.EtapaPipelineCrmId && x.Activa, ct);
        if (etapa is null) return Result<OportunidadCrmDetalleDto>.Fail("Etapa no encontrada.", "CRM_ETAPA_NOT_FOUND");

        var refs = await ValidarReferenciasAsync(empresaId, null, null, request.DteDocumentoId, request.CuentaCobroId, ct);
        if (refs.IsFailure) return Result<OportunidadCrmDetalleDto>.Fail(refs.Error!, refs.ErrorCode);

        opp.EtapaPipelineCrmId = etapa.Id;
        opp.Probabilidad = request.Probabilidad ?? etapa.ProbabilidadDefault;
        opp.MotivoPerdida = request.MotivoPerdida?.Trim();
        opp.DteDocumentoId = request.DteDocumentoId ?? opp.DteDocumentoId;
        opp.CuentaCobroId = request.CuentaCobroId ?? opp.CuentaCobroId;
        if (etapa.EsCierreGanado)
        {
            opp.EstadoCodigo = OportunidadCrmEstados.Ganada;
            opp.FechaCierreReal = DateOnly.FromDateTime(DateTime.UtcNow);
        }
        else if (etapa.EsCierrePerdido)
        {
            opp.EstadoCodigo = OportunidadCrmEstados.Perdida;
            opp.FechaCierreReal = DateOnly.FromDateTime(DateTime.UtcNow);
        }
        else
        {
            opp.EstadoCodigo = OportunidadCrmEstados.Abierta;
            opp.FechaCierreReal = null;
        }
        opp.UpdatedAt = DateTime.UtcNow;
        opp.UpdatedBy = actor;

        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "CAMBIAR_ETAPA_OPORTUNIDAD", $"{opp.Titulo} -> {etapa.Codigo}", "OportunidadCrm", opp.Id);
        return await GetOportunidadAsync(empresaId, opp.Id, ct);
    }

    public async Task<Result<PagedResult<ActividadCrmDto>>> ListActividadesAsync(int empresaId, bool soloPendientes, int? oportunidadId, PagedQuery query, CancellationToken ct = default)
    {
        var q = _db.ActividadesCrm.AsNoTracking().Where(x => x.EmpresaId == empresaId);
        if (soloPendientes) q = q.Where(x => x.EstadoCodigo == ActividadCrmEstados.Pendiente);
        if (oportunidadId is int oid) q = q.Where(x => x.OportunidadCrmId == oid);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(x => x.Asunto.Contains(s) || (x.Descripcion != null && x.Descripcion.Contains(s)));
        }

        var total = await q.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var items = await q.OrderBy(x => x.EstadoCodigo == ActividadCrmEstados.Pendiente ? 0 : 1).ThenBy(x => x.FechaProgramada)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => ToActividadDto(x))
            .ToListAsync(ct);
        return Result<PagedResult<ActividadCrmDto>>.Ok(PagedResult<ActividadCrmDto>.Create(items, total, page, pageSize));
    }

    public async Task<Result<ActividadCrmDto>> CrearActividadAsync(int empresaId, CrearActividadCrmRequest request, string? actor, CancellationToken ct = default)
    {
        if (!ActividadCrmTipos.All.Contains(request.Tipo)) return Result<ActividadCrmDto>.Fail("Tipo de actividad invalido.", "VALIDATION");
        var refs = await ValidarReferenciasAsync(empresaId, request.ClienteId, request.ContactoCrmId, null, null, ct, request.OportunidadCrmId);
        if (refs.IsFailure) return Result<ActividadCrmDto>.Fail(refs.Error!, refs.ErrorCode);

        var actividad = new ActividadCrm
        {
            EmpresaId = empresaId,
            OportunidadCrmId = request.OportunidadCrmId,
            ContactoCrmId = request.ContactoCrmId,
            ClienteId = request.ClienteId ?? refs.Value!.ClienteIdDesdeContacto,
            Tipo = request.Tipo,
            Asunto = request.Asunto.Trim(),
            Descripcion = request.Descripcion?.Trim(),
            FechaProgramada = request.FechaProgramada ?? DateTime.UtcNow,
            RecordatorioAt = request.RecordatorioAt,
            EstadoCodigo = ActividadCrmEstados.Pendiente,
            CreatedBy = actor,
        };

        _db.ActividadesCrm.Add(actividad);
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "CREAR_ACTIVIDAD", actividad.Asunto, "ActividadCrm", actividad.Id);
        return Result<ActividadCrmDto>.Ok(ToActividadDto(actividad));
    }

    public async Task<Result<ActividadCrmDto>> CompletarActividadAsync(int empresaId, int id, CompletarActividadCrmRequest request, string? actor, CancellationToken ct = default)
    {
        var actividad = await _db.ActividadesCrm.FirstOrDefaultAsync(x => x.EmpresaId == empresaId && x.Id == id, ct);
        if (actividad is null) return Result<ActividadCrmDto>.Fail("Actividad no encontrada.", "CRM_ACTIVIDAD_NOT_FOUND");
        actividad.EstadoCodigo = ActividadCrmEstados.Realizada;
        actividad.FechaRealizada = DateTime.UtcNow;
        actividad.Resultado = request.Resultado?.Trim();
        actividad.UpdatedAt = DateTime.UtcNow;
        actividad.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "COMPLETAR_ACTIVIDAD", actividad.Asunto, "ActividadCrm", actividad.Id);
        return Result<ActividadCrmDto>.Ok(ToActividadDto(actividad));
    }

    public async Task<Result> CancelarActividadAsync(int empresaId, int id, string? actor, CancellationToken ct = default)
    {
        var actividad = await _db.ActividadesCrm.FirstOrDefaultAsync(x => x.EmpresaId == empresaId && x.Id == id, ct);
        if (actividad is null) return Result.Fail("Actividad no encontrada.", "CRM_ACTIVIDAD_NOT_FOUND");
        actividad.EstadoCodigo = ActividadCrmEstados.Cancelada;
        actividad.UpdatedAt = DateTime.UtcNow;
        actividad.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "CANCELAR_ACTIVIDAD", actividad.Asunto, "ActividadCrm", actividad.Id);
        return Result.Ok();
    }

    public async Task<Result<CrmResumenDto>> ResumenAsync(int empresaId, CancellationToken ct = default)
    {
        var abiertas = await _db.OportunidadesCrm.AsNoTracking()
            .Where(x => x.EmpresaId == empresaId && x.EstadoCodigo == OportunidadCrmEstados.Abierta)
            .Select(x => new { x.MontoEstimado, x.Probabilidad })
            .ToListAsync(ct);
        var ahora = DateTime.UtcNow;
        var resumen = new CrmResumenDto
        {
            ContactosActivos = await _db.ContactosCrm.CountAsync(x => x.EmpresaId == empresaId && x.EstadoCodigo == ContactoCrmEstados.Activo, ct),
            OportunidadesAbiertas = abiertas.Count,
            PipelineAbierto = abiertas.Sum(x => x.MontoEstimado),
            PipelinePonderado = abiertas.Sum(x => x.MontoEstimado * (x.Probabilidad / 100m)),
            ActividadesPendientes = await _db.ActividadesCrm.CountAsync(x => x.EmpresaId == empresaId && x.EstadoCodigo == ActividadCrmEstados.Pendiente, ct),
            ActividadesVencidas = await _db.ActividadesCrm.CountAsync(x => x.EmpresaId == empresaId && x.EstadoCodigo == ActividadCrmEstados.Pendiente && x.FechaProgramada < ahora, ct),
        };
        return Result<CrmResumenDto>.Ok(resumen);
    }

    private async Task EnsureDefaultEtapasAsync(int empresaId, string? actor, CancellationToken ct)
    {
        if (await _db.EtapasPipelineCrm.AnyAsync(x => x.EmpresaId == empresaId, ct)) return;
        _db.EtapasPipelineCrm.AddRange(
            Stage(empresaId, "LEAD", "Lead", 1, 10m, false, false, actor),
            Stage(empresaId, "CALIFICADA", "Calificada", 2, 25m, false, false, actor),
            Stage(empresaId, "PROPUESTA", "Propuesta", 3, 50m, false, false, actor),
            Stage(empresaId, "NEGOCIACION", "Negociacion", 4, 75m, false, false, actor),
            Stage(empresaId, "GANADA", "Ganada", 5, 100m, true, false, actor),
            Stage(empresaId, "PERDIDA", "Perdida", 6, 0m, false, true, actor));
        await _db.SaveChangesAsync(ct);
    }

    private async Task<EtapaPipelineCrm?> GetDefaultEtapaAsync(int empresaId, string? actor, CancellationToken ct)
    {
        await EnsureDefaultEtapasAsync(empresaId, actor, ct);
        return await _db.EtapasPipelineCrm
            .Where(x => x.EmpresaId == empresaId && x.Activa && !x.EsCierreGanado && !x.EsCierrePerdido)
            .OrderBy(x => x.Orden)
            .FirstOrDefaultAsync(ct);
    }

    private static EtapaPipelineCrm Stage(int empresaId, string codigo, string nombre, int orden, decimal probabilidad, bool ganada, bool perdida, string? actor) => new()
    {
        EmpresaId = empresaId,
        Codigo = codigo,
        Nombre = nombre,
        Orden = orden,
        ProbabilidadDefault = probabilidad,
        Activa = true,
        EsCierreGanado = ganada,
        EsCierrePerdido = perdida,
        CreatedBy = actor,
    };

    private async Task<Result<ReferenceValidation>> ValidarReferenciasAsync(
        int empresaId,
        int? clienteId,
        int? contactoId,
        int? dteDocumentoId,
        int? cuentaCobroId,
        CancellationToken ct,
        int? oportunidadId = null)
    {
        int? clienteDesdeContacto = null;
        if (clienteId is int cid && !await ClienteExiste(empresaId, cid, ct))
            return Result<ReferenceValidation>.Fail("Cliente no encontrado.", "CLIENTE_NOT_FOUND");
        if (contactoId is int contactId)
        {
            var contacto = await _db.ContactosCrm.AsNoTracking().FirstOrDefaultAsync(x => x.EmpresaId == empresaId && x.Id == contactId, ct);
            if (contacto is null) return Result<ReferenceValidation>.Fail("Contacto no encontrado.", "CRM_CONTACTO_NOT_FOUND");
            clienteDesdeContacto = contacto.ClienteId;
        }
        if (oportunidadId is int oid && !await _db.OportunidadesCrm.AnyAsync(x => x.EmpresaId == empresaId && x.Id == oid, ct))
            return Result<ReferenceValidation>.Fail("Oportunidad no encontrada.", "CRM_OPORTUNIDAD_NOT_FOUND");
        if (dteDocumentoId is int did && !await _db.DteDocumentos.AnyAsync(x => x.EmpresaId == empresaId && x.Id == did, ct))
            return Result<ReferenceValidation>.Fail("DTE no encontrado.", "DTE_NOT_FOUND");
        if (cuentaCobroId is int cta && !await _db.CuentasCobro.AnyAsync(x => x.EmpresaId == empresaId && x.Id == cta, ct))
            return Result<ReferenceValidation>.Fail("Cuenta de cobro no encontrada.", "CUENTA_COBRO_NOT_FOUND");

        return Result<ReferenceValidation>.Ok(new ReferenceValidation(clienteDesdeContacto));
    }

    private Task<bool> ClienteExiste(int empresaId, int clienteId, CancellationToken ct)
        => _db.Clientes.AnyAsync(x => x.EmpresaId == empresaId && x.Id == clienteId, ct);

    private Task Audit(int empresaId, string? actor, string accion, string detalle, string entidad, int id)
        => _auditoria.RegistrarAsync(new AuditoriaEvent
        {
            EmpresaId = empresaId,
            Username = actor,
            Modulo = AuditModule,
            Accion = accion,
            Entidad = entidad,
            EntidadId = id.ToString(),
            Resultado = "OK",
            Detalle = detalle,
        });

    private sealed record ReferenceValidation(int? ClienteIdDesdeContacto);

    private static ContactoCrmDto ToContactoDto(ContactoCrm c) => new()
    {
        Id = c.Id,
        ClienteId = c.ClienteId,
        ClienteNombre = c.Cliente != null ? c.Cliente.Nombre : null,
        Nombre = c.Nombre,
        Cargo = c.Cargo,
        Email = c.Email,
        Telefono = c.Telefono,
        Origen = c.Origen,
        EstadoCodigo = c.EstadoCodigo,
        Notas = c.Notas,
    };

    private static EtapaPipelineCrmDto ToEtapaDto(EtapaPipelineCrm e) => new()
    {
        Id = e.Id,
        Codigo = e.Codigo,
        Nombre = e.Nombre,
        Orden = e.Orden,
        ProbabilidadDefault = e.ProbabilidadDefault,
        Activa = e.Activa,
        EsCierreGanado = e.EsCierreGanado,
        EsCierrePerdido = e.EsCierrePerdido,
    };

    private static OportunidadCrmDto ToOportunidadDto(OportunidadCrm o) => new()
    {
        Id = o.Id,
        ClienteId = o.ClienteId,
        ClienteNombre = o.Cliente != null ? o.Cliente.Nombre : null,
        ContactoCrmId = o.ContactoCrmId,
        ContactoNombre = o.Contacto != null ? o.Contacto.Nombre : null,
        EtapaPipelineCrmId = o.EtapaPipelineCrmId,
        EtapaCodigo = o.Etapa.Codigo,
        EtapaNombre = o.Etapa.Nombre,
        Titulo = o.Titulo,
        Descripcion = o.Descripcion,
        MontoEstimado = o.MontoEstimado,
        Probabilidad = o.Probabilidad,
        FechaApertura = o.FechaApertura,
        FechaCierreEstimada = o.FechaCierreEstimada,
        FechaCierreReal = o.FechaCierreReal,
        EstadoCodigo = o.EstadoCodigo,
        MotivoPerdida = o.MotivoPerdida,
        DteDocumentoId = o.DteDocumentoId,
        CuentaCobroId = o.CuentaCobroId,
        ActividadesPendientes = o.Actividades.Count(a => a.EstadoCodigo == ActividadCrmEstados.Pendiente),
    };

    private static OportunidadCrmDetalleDto ToOportunidadDetalleDto(OportunidadCrm o)
    {
        var dto = new OportunidadCrmDetalleDto
        {
            Id = o.Id,
            ClienteId = o.ClienteId,
            ClienteNombre = o.Cliente != null ? o.Cliente.Nombre : null,
            ContactoCrmId = o.ContactoCrmId,
            ContactoNombre = o.Contacto != null ? o.Contacto.Nombre : null,
            EtapaPipelineCrmId = o.EtapaPipelineCrmId,
            EtapaCodigo = o.Etapa.Codigo,
            EtapaNombre = o.Etapa.Nombre,
            Titulo = o.Titulo,
            Descripcion = o.Descripcion,
            MontoEstimado = o.MontoEstimado,
            Probabilidad = o.Probabilidad,
            FechaApertura = o.FechaApertura,
            FechaCierreEstimada = o.FechaCierreEstimada,
            FechaCierreReal = o.FechaCierreReal,
            EstadoCodigo = o.EstadoCodigo,
            MotivoPerdida = o.MotivoPerdida,
            DteDocumentoId = o.DteDocumentoId,
            CuentaCobroId = o.CuentaCobroId,
            ActividadesPendientes = o.Actividades.Count(a => a.EstadoCodigo == ActividadCrmEstados.Pendiente),
        };
        dto.Actividades = o.Actividades.OrderBy(a => a.FechaProgramada).Select(ToActividadDto).ToList();
        return dto;
    }

    private static ActividadCrmDto ToActividadDto(ActividadCrm a) => new()
    {
        Id = a.Id,
        OportunidadCrmId = a.OportunidadCrmId,
        ContactoCrmId = a.ContactoCrmId,
        ClienteId = a.ClienteId,
        Tipo = a.Tipo,
        Asunto = a.Asunto,
        Descripcion = a.Descripcion,
        FechaProgramada = a.FechaProgramada,
        FechaRealizada = a.FechaRealizada,
        RecordatorioAt = a.RecordatorioAt,
        EstadoCodigo = a.EstadoCodigo,
        Resultado = a.Resultado,
    };
}
