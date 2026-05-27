using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Empresas;
using NeoSTP.Application.Empresas.Dtos;
using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Licenciamiento;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

public class EmpresasService : IEmpresasService, ILicenciaResolver
{
    private const string AuditModule = "EMPRESAS";

    private readonly NeoStpDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public EmpresasService(NeoStpDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    public async Task<Result<PagedResult<EmpresaDto>>> GetListAsync(int? scopeEmpresaId, PagedQuery query, CancellationToken ct = default)
    {
        var q = _db.Empresas.AsNoTracking().AsQueryable();
        if (scopeEmpresaId is not null)
        {
            q = q.Where(e => e.Id == scopeEmpresaId);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(e => EF.Functions.Like(e.Nit, $"%{s}%")
                           || EF.Functions.Like(e.RazonSocial, $"%{s}%")
                           || EF.Functions.Like(e.NombreComercial ?? string.Empty, $"%{s}%"));
        }

        var total = await q.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var raw = await q.OrderBy(e => e.RazonSocial)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        var ids = raw.Select(e => e.Id).ToArray();
        var planesPorEmpresa = await _db.EmpresaPlanes.AsNoTracking()
            .Where(ep => ids.Contains(ep.EmpresaId))
            .Include(ep => ep.Plan)
            .GroupBy(ep => ep.EmpresaId)
            .Select(g => g.OrderByDescending(x => x.FechaInicio).First())
            .ToListAsync(ct);

        var sucursalesPorEmpresa = await _db.Sucursales.AsNoTracking()
            .Where(s => ids.Contains(s.EmpresaId))
            .GroupBy(s => s.EmpresaId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var pvPorEmpresa = await _db.PuntosVenta.AsNoTracking()
            .Where(p => ids.Contains(p.Sucursal.EmpresaId))
            .GroupBy(p => p.Sucursal.EmpresaId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var usuariosPorEmpresa = await _db.Usuarios.AsNoTracking()
            .Where(u => u.EmpresaId != null && ids.Contains(u.EmpresaId!.Value))
            .GroupBy(u => u.EmpresaId!.Value).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var items = raw.Select(e =>
        {
            var plan = planesPorEmpresa.FirstOrDefault(p => p.EmpresaId == e.Id);
            return new EmpresaDto
            {
                Id = e.Id, Nit = e.Nit, Nrc = e.Nrc,
                RazonSocial = e.RazonSocial, NombreComercial = e.NombreComercial,
                CodigoActividad = e.CodigoActividad, ActividadEconomica = e.ActividadEconomica,
                Departamento = e.Departamento, Municipio = e.Municipio,
                Direccion = e.Direccion, Telefono = e.Telefono, Correo = e.Correo,
                LogoUrl = e.LogoUrl, EstadoCodigo = e.EstadoCodigo, CreatedAt = e.CreatedAt,
                Sucursales = sucursalesPorEmpresa.GetValueOrDefault(e.Id),
                PuntosVenta = pvPorEmpresa.GetValueOrDefault(e.Id),
                Usuarios = usuariosPorEmpresa.GetValueOrDefault(e.Id),
                PlanActualCodigo = plan?.Plan.Codigo,
                PlanActualNombre = plan?.Plan.Nombre,
                PlanFechaFin = plan?.FechaFin,
            };
        }).ToList();

        return Result<PagedResult<EmpresaDto>>.Ok(PagedResult<EmpresaDto>.Create(items, total, page, pageSize));
    }

    public async Task<Result<EmpresaDto>> GetByIdAsync(int? scopeEmpresaId, int id, CancellationToken ct = default)
    {
        if (scopeEmpresaId is not null && scopeEmpresaId != id)
        {
            return Result<EmpresaDto>.Fail("Empresa fuera de tu alcance.", "EMPRESA_FORBIDDEN");
        }

        var page = await GetListAsync(scopeEmpresaId, new PagedQuery { Page = 1, PageSize = 1 }, ct);
        if (page.IsFailure) return Result<EmpresaDto>.Fail(page.Error!, page.ErrorCode);

        var first = page.Value!.Items.FirstOrDefault(e => e.Id == id);
        return first is null
            ? Result<EmpresaDto>.Fail("Empresa no encontrada.", "EMPRESA_NOT_FOUND")
            : Result<EmpresaDto>.Ok(first);
    }

    public async Task<Result<EmpresaDto>> CreateAsync(CreateEmpresaRequest request, string? actor, CancellationToken ct = default)
    {
        var errors = ValidateEmpresaBase(request.Nit, request.RazonSocial);
        if (errors.Count > 0) return Result<EmpresaDto>.Fail("Datos inválidos.", "VALIDATION", errors);

        var nit = request.Nit.Trim();
        if (await _db.Empresas.AnyAsync(e => e.Nit == nit, ct))
        {
            return Result<EmpresaDto>.Fail($"Ya existe una empresa con NIT {nit}.", "EMPRESA_DUPLICATE");
        }

        var empresa = new Empresa
        {
            Nit = nit, Nrc = request.Nrc?.Trim(),
            RazonSocial = request.RazonSocial.Trim(),
            NombreComercial = request.NombreComercial?.Trim(),
            CodigoActividad = request.CodigoActividad?.Trim(),
            ActividadEconomica = request.ActividadEconomica?.Trim(),
            Departamento = request.Departamento, Municipio = request.Municipio,
            Direccion = request.Direccion, Telefono = request.Telefono, Correo = request.Correo,
            EstadoCodigo = "ACTIVA",
            CreatedAt = DateTime.UtcNow, CreatedBy = actor,
        };
        _db.Empresas.Add(empresa);
        await _db.SaveChangesAsync(ct);
        await Audit(empresa.Id, actor, "CREATE", "OK", $"Empresa {empresa.RazonSocial} creada", empresa.Id);

        var dto = (await GetListAsync(null, new PagedQuery { Page = 1, PageSize = 1, Search = empresa.Nit }, ct)).Value!.Items.First();
        return Result<EmpresaDto>.Ok(dto);
    }

    public async Task<Result<EmpresaDto>> UpdateAsync(int? scopeEmpresaId, int id, UpdateEmpresaRequest request, string? actor, CancellationToken ct = default)
    {
        if (scopeEmpresaId is not null && scopeEmpresaId != id)
        {
            return Result<EmpresaDto>.Fail("Empresa fuera de tu alcance.", "EMPRESA_FORBIDDEN");
        }

        var empresa = await _db.Empresas.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (empresa is null) return Result<EmpresaDto>.Fail("Empresa no encontrada.", "EMPRESA_NOT_FOUND");

        empresa.Nrc = request.Nrc?.Trim();
        empresa.RazonSocial = request.RazonSocial.Trim();
        empresa.NombreComercial = request.NombreComercial?.Trim();
        empresa.CodigoActividad = request.CodigoActividad?.Trim();
        empresa.ActividadEconomica = request.ActividadEconomica?.Trim();
        empresa.Departamento = request.Departamento;
        empresa.Municipio = request.Municipio;
        empresa.Direccion = request.Direccion;
        empresa.Telefono = request.Telefono;
        empresa.Correo = request.Correo;
        empresa.LogoUrl = request.LogoUrl;
        if (!string.IsNullOrWhiteSpace(request.EstadoCodigo)) empresa.EstadoCodigo = request.EstadoCodigo;
        empresa.UpdatedAt = DateTime.UtcNow;
        empresa.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresa.Id, actor, "UPDATE", "OK", $"Empresa {empresa.RazonSocial} actualizada", empresa.Id);

        return await GetByIdAsync(null, id, ct);
    }

    public async Task<Result<LicenciaDto>> GetLicenciaAsync(int empresaId, CancellationToken ct = default)
    {
        var lic = await ResolveAsync(empresaId, ct);
        return lic is null
            ? Result<LicenciaDto>.Fail("Empresa no encontrada.", "EMPRESA_NOT_FOUND")
            : Result<LicenciaDto>.Ok(lic);
    }

    public async Task<Result<LicenciaDto>> AsignarPlanAsync(int empresaId, AsignarPlanRequest request, string? actor, CancellationToken ct = default)
    {
        var empresa = await _db.Empresas.FirstOrDefaultAsync(e => e.Id == empresaId, ct);
        if (empresa is null) return Result<LicenciaDto>.Fail("Empresa no encontrada.", "EMPRESA_NOT_FOUND");

        var plan = await _db.Planes
            .Include(p => p.Modulos).ThenInclude(pm => pm.Modulo)
            .FirstOrDefaultAsync(p => p.Id == request.PlanId && p.Activo, ct);
        if (plan is null) return Result<LicenciaDto>.Fail("Plan no encontrado o inactivo.", "PLAN_NOT_FOUND");

        // Cerrar el plan vigente (si existe) marcandolo VENCIDO
        var planActual = await _db.EmpresaPlanes
            .Where(ep => ep.EmpresaId == empresaId && ep.EstadoCodigo == "ACTIVO")
            .OrderByDescending(ep => ep.FechaInicio)
            .FirstOrDefaultAsync(ct);
        if (planActual is not null)
        {
            planActual.EstadoCodigo = "CANCELADO";
            planActual.FechaFin = DateTime.UtcNow;
            planActual.UpdatedAt = DateTime.UtcNow;
            planActual.UpdatedBy = actor;
        }

        var nuevo = new EmpresaPlan
        {
            EmpresaId = empresaId, PlanId = plan.Id,
            FechaInicio = request.FechaInicio ?? DateTime.UtcNow,
            FechaFin = request.FechaFin,
            EstadoCodigo = "ACTIVO",
            CreatedAt = DateTime.UtcNow, CreatedBy = actor,
        };
        _db.EmpresaPlanes.Add(nuevo);

        // Sincronizar modulos: activar los del plan que no esten ya activos
        var existentes = await _db.EmpresaModulos.Where(em => em.EmpresaId == empresaId).ToListAsync(ct);
        var existentesMap = existentes.ToDictionary(em => em.ModuloId);

        foreach (var pm in plan.Modulos)
        {
            if (existentesMap.TryGetValue(pm.ModuloId, out var em))
            {
                if (!em.Activo)
                {
                    em.Activo = true;
                    em.FechaActivacion = DateTime.UtcNow;
                    em.FechaInactivacion = null;
                }
            }
            else
            {
                _db.EmpresaModulos.Add(new EmpresaModulo
                {
                    EmpresaId = empresaId, ModuloId = pm.ModuloId,
                    Activo = true, FechaActivacion = DateTime.UtcNow,
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "ASIGNAR_PLAN", "OK", $"Plan {plan.Codigo} asignado", empresaId);

        var lic = await ResolveAsync(empresaId, ct);
        return Result<LicenciaDto>.Ok(lic!);
    }

    public async Task<Result<LicenciaDto>> ActivarModuloAsync(int empresaId, int moduloId, string? actor, CancellationToken ct = default)
        => await ToggleModuloAsync(empresaId, moduloId, true, actor, ct);

    public async Task<Result<LicenciaDto>> DesactivarModuloAsync(int empresaId, int moduloId, string? actor, CancellationToken ct = default)
        => await ToggleModuloAsync(empresaId, moduloId, false, actor, ct);

    private async Task<Result<LicenciaDto>> ToggleModuloAsync(int empresaId, int moduloId, bool activar, string? actor, CancellationToken ct)
    {
        var empresa = await _db.Empresas.AnyAsync(e => e.Id == empresaId, ct);
        if (!empresa) return Result<LicenciaDto>.Fail("Empresa no encontrada.", "EMPRESA_NOT_FOUND");

        var modulo = await _db.Modulos.FirstOrDefaultAsync(m => m.Id == moduloId, ct);
        if (modulo is null) return Result<LicenciaDto>.Fail("Módulo no encontrado.", "MODULO_NOT_FOUND");

        var em = await _db.EmpresaModulos.FirstOrDefaultAsync(x => x.EmpresaId == empresaId && x.ModuloId == moduloId, ct);
        if (em is null)
        {
            em = new EmpresaModulo
            {
                EmpresaId = empresaId, ModuloId = moduloId,
                Activo = activar,
                FechaActivacion = DateTime.UtcNow,
                FechaInactivacion = activar ? null : DateTime.UtcNow,
            };
            _db.EmpresaModulos.Add(em);
        }
        else
        {
            em.Activo = activar;
            if (activar) { em.FechaActivacion = DateTime.UtcNow; em.FechaInactivacion = null; }
            else { em.FechaInactivacion = DateTime.UtcNow; }
        }

        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, activar ? "ACTIVAR_MODULO" : "DESACTIVAR_MODULO", "OK",
            $"Modulo {modulo.Codigo} {(activar ? "activado" : "desactivado")}", empresaId);

        var lic = await ResolveAsync(empresaId, ct);
        return Result<LicenciaDto>.Ok(lic!);
    }

    // -- ILicenciaResolver --------------------------------------------

    public async Task<LicenciaDto?> ResolveAsync(int empresaId, CancellationToken ct = default)
    {
        var empresa = await _db.Empresas.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == empresaId, ct);
        if (empresa is null) return null;

        var planActivo = await _db.EmpresaPlanes.AsNoTracking()
            .Include(ep => ep.Plan).ThenInclude(p => p.Modulos)
            .Where(ep => ep.EmpresaId == empresaId && ep.EstadoCodigo == "ACTIVO")
            .OrderByDescending(ep => ep.FechaInicio)
            .FirstOrDefaultAsync(ct);

        var modulosEmpresa = await _db.EmpresaModulos.AsNoTracking()
            .Include(em => em.Modulo)
            .Where(em => em.EmpresaId == empresaId)
            .ToListAsync(ct);

        var modulosPlanIds = planActivo?.Plan.Modulos.Select(pm => pm.ModuloId).ToHashSet() ?? new HashSet<int>();

        var modulos = modulosEmpresa.Select(em => new EmpresaModuloDto
        {
            ModuloId = em.ModuloId,
            Codigo = em.Modulo.Codigo,
            Nombre = em.Modulo.Nombre,
            Activo = em.Activo,
            IncluidoEnPlan = modulosPlanIds.Contains(em.ModuloId),
            FechaActivacion = em.FechaActivacion,
        }).OrderBy(m => m.Codigo).ToList();

        var ahora = DateTime.UtcNow;
        var vigente = planActivo is not null
                      && planActivo.EstadoCodigo == "ACTIVO"
                      && (planActivo.FechaFin is null || planActivo.FechaFin > ahora)
                      && empresa.EstadoCodigo == "ACTIVA";

        var usuarios = await _db.Usuarios.CountAsync(u => u.EmpresaId == empresaId, ct);
        var sucursales = await _db.Sucursales.CountAsync(s => s.EmpresaId == empresaId, ct);
        var pv = await _db.PuntosVenta.CountAsync(p => p.Sucursal.EmpresaId == empresaId, ct);

        return new LicenciaDto
        {
            EmpresaId = empresa.Id,
            EmpresaNombre = empresa.RazonSocial,
            EmpresaEstado = empresa.EstadoCodigo,
            PlanId = planActivo?.PlanId,
            PlanCodigo = planActivo?.Plan.Codigo,
            PlanNombre = planActivo?.Plan.Nombre,
            PlanFechaInicio = planActivo?.FechaInicio,
            PlanFechaFin = planActivo?.FechaFin,
            PlanEstado = planActivo?.EstadoCodigo,
            Vigente = vigente,
            Modulos = modulos,
            LimiteUsuarios = planActivo?.Plan.LimiteUsuarios,
            LimiteSucursales = planActivo?.Plan.LimiteSucursales,
            LimitePuntosVenta = planActivo?.Plan.LimitePuntosVenta,
            LimiteDteMensual = planActivo?.Plan.LimiteDteMensual,
            UsuariosUsados = usuarios,
            SucursalesUsadas = sucursales,
            PuntosVentaUsados = pv,
        };
    }

    // -- helpers -------------------------------------------------------

    private static List<string> ValidateEmpresaBase(string? nit, string? razon)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(nit)) errors.Add("NIT es obligatorio.");
        if (string.IsNullOrWhiteSpace(razon)) errors.Add("Razón social es obligatoria.");
        return errors;
    }

    private Task Audit(int? empresaId, string? actor, string accion, string resultado, string detalle, int entidadId)
        => _auditoria.RegistrarAsync(new AuditoriaEvent
        {
            EmpresaId = empresaId,
            Username = actor,
            Modulo = AuditModule,
            Accion = accion,
            Entidad = "Empresa",
            EntidadId = entidadId.ToString(),
            Resultado = resultado,
            Detalle = detalle,
        });
}
