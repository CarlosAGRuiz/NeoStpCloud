using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Provisioning;
using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Licenciamiento;
using NeoSTP.Domain.Core.Seguridad;

namespace NeoSTP.Infrastructure.Persistence.Seed;

/// <summary>
/// Provisioning idempotente de la empresa de pruebas (Sprint 11).
/// Crea — solo si está habilitado y aún no existe — una empresa completa:
/// empresa → plan → módulos → sucursal → punto de venta → usuario admin →
/// configuración DTE base (sin secretos).
///
/// Es seguro ejecutarlo en cada arranque: si la empresa (por NIT) ya existe, no hace nada.
/// </summary>
public static class EmpresaPruebaSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db      = scope.ServiceProvider.GetRequiredService<NeoStpDbContext>();
        var logger  = scope.ServiceProvider.GetRequiredService<ILogger<NeoStpDbContext>>();
        var hasher  = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<EmpresaPruebaOptions>>().Value;

        if (!options.Enabled)
            return;

        if (string.IsNullOrWhiteSpace(options.Nit) || string.IsNullOrWhiteSpace(options.RazonSocial))
        {
            logger.LogWarning("EmpresaPruebaSeeder: Enabled=true pero faltan Nit/RazonSocial. Saltando.");
            return;
        }

        var existente = await db.Empresas.FirstOrDefaultAsync(e => e.Nit == options.Nit, ct);
        if (existente is not null)
        {
            // La empresa ya existe (no re-provisionamos), pero sí sincronizamos los módulos
            // del plan que pudieron agregarse después (p. ej. NEORRHH en una versión nueva),
            // para que los módulos recién liberados queden activos sin recrear la empresa.
            await BackfillModulosDelPlanAsync(db, existente, logger, ct);
            return;
        }

        var plan = await db.Planes
            .Include(p => p.Modulos)
            .FirstOrDefaultAsync(p => p.Codigo == options.PlanCodigo, ct);
        if (plan is null)
        {
            logger.LogWarning("EmpresaPruebaSeeder: plan '{Plan}' no encontrado. Saltando.", options.PlanCodigo);
            return;
        }

        var ahora = DateTime.UtcNow;
        const string actor = "PROVISION_SEEDER";

        // 1. Empresa
        var empresa = new Empresa
        {
            Nit                = options.Nit,
            Nrc                = options.Nrc,
            RazonSocial        = options.RazonSocial,
            NombreComercial    = options.NombreComercial,
            CodigoActividad    = options.CodigoActividad,
            ActividadEconomica = options.ActividadEconomica,
            Departamento       = options.Departamento,
            Municipio          = options.Municipio,
            Direccion          = options.Direccion,
            Telefono           = options.Telefono,
            Correo             = options.Correo,
            EstadoCodigo       = EstadoCodes.Activo,
            CreatedAt          = ahora,
            CreatedBy          = actor,
        };
        db.Empresas.Add(empresa);
        await db.SaveChangesAsync(ct);

        // 2. Plan de la empresa
        db.EmpresaPlanes.Add(new EmpresaPlan
        {
            EmpresaId    = empresa.Id,
            PlanId       = plan.Id,
            FechaInicio  = ahora,
            FechaFin     = ahora.AddYears(1),
            EstadoCodigo = "ACTIVO",
            CreatedAt    = ahora,
            CreatedBy    = actor,
        });

        // 3. Módulos del plan → módulos de la empresa
        foreach (var pm in plan.Modulos.Where(m => m.Activo))
        {
            db.EmpresaModulos.Add(new EmpresaModulo
            {
                EmpresaId       = empresa.Id,
                ModuloId        = pm.ModuloId,
                Activo          = true,
                FechaActivacion = ahora,
            });
        }

        // 4. Sucursal Casa Matriz
        var sucursal = new Sucursal
        {
            EmpresaId                 = empresa.Id,
            Codigo                    = options.Sucursal.Codigo,
            Nombre                    = options.Sucursal.Nombre,
            TipoEstablecimientoCodigo = options.Sucursal.TipoEstablecimientoCodigo,
            CodigoEstablecimientoMh   = options.Sucursal.CodigoEstablecimientoMh,
            Direccion                 = options.Direccion,
            Departamento              = options.Departamento,
            Municipio                 = options.Municipio,
            Telefono                  = options.Telefono,
            EstadoCodigo              = EstadoCodes.Activo,
            CreatedAt                 = ahora,
            CreatedBy                 = actor,
        };
        db.Sucursales.Add(sucursal);
        await db.SaveChangesAsync(ct);

        // 5. Punto de venta principal
        db.PuntosVenta.Add(new PuntoVenta
        {
            SucursalId         = sucursal.Id,
            Codigo             = options.PuntoVenta.Codigo,
            Nombre             = options.PuntoVenta.Nombre,
            CodigoPuntoVentaMh = options.PuntoVenta.CodigoPuntoVentaMh,
            EstadoCodigo       = EstadoCodes.Activo,
            CreatedAt          = ahora,
            CreatedBy          = actor,
        });

        // 6. Usuario administrador + rol ADMIN
        var rolAdmin = await db.Roles.FirstOrDefaultAsync(r => r.Codigo == "ADMIN", ct);
        var admin = new Usuario
        {
            EmpresaId         = empresa.Id,
            Username          = options.Admin.Username,
            Email             = options.Admin.Email,
            PasswordHash      = hasher.Hash(options.Admin.Password),
            NombreCompleto    = options.Admin.NombreCompleto,
            TipoUsuarioCodigo = "ADMIN",
            EstadoCodigo      = EstadoCodes.Activo,
            CreatedAt         = ahora,
            CreatedBy         = actor,
        };
        db.Usuarios.Add(admin);
        await db.SaveChangesAsync(ct);

        if (rolAdmin is not null)
        {
            db.UsuarioRoles.Add(new UsuarioRol
            {
                UsuarioId = admin.Id,
                RolId     = rolAdmin.Id,
                CreatedAt = ahora,
            });
        }

        // 7. Configuración DTE base (SIN secretos: password MH y PFX se cargan vía UI)
        db.DteConfiguracion.Add(new DteConfiguracion
        {
            EmpresaId                 = empresa.Id,
            AmbienteCodigo            = options.Dte.AmbienteCodigo,
            UsuarioMh                 = options.Dte.UsuarioMh,
            TipoEstablecimientoCodigo = options.Dte.TipoEstablecimientoCodigo,
            CodigoEstablecimientoMh   = options.Dte.CodigoEstablecimientoMh,
            CodigoPuntoVentaMh        = options.Dte.CodigoPuntoVentaMh,
            CreatedAt                 = ahora,
            CreatedBy                 = actor,
        });

        await db.SaveChangesAsync(ct);

        logger.LogWarning(
            "EmpresaPruebaSeeder: empresa '{Razon}' (NIT {Nit}) creada con plan {Plan}, " +
            "{Modulos} módulos, sucursal '{Suc}', PV '{Pv}' y admin '{Admin}' (pass por defecto: '{Pass}'). " +
            "Carga el certificado PFX y el password MH desde /DteConfiguracion para completar la configuración.",
            empresa.RazonSocial, empresa.Nit, plan.Codigo, plan.Modulos.Count,
            sucursal.Nombre, options.PuntoVenta.Nombre, admin.Username, options.Admin.Password);
    }

    /// <summary>
    /// Activa en <c>EmpresaModulos</c> los módulos del plan vigente de la empresa que aún
    /// no estén presentes. Idempotente: si no falta ninguno, no hace nada. Permite que
    /// módulos liberados después de provisionar (p. ej. NEORRHH) queden activos sin recrear
    /// la empresa de pruebas.
    /// </summary>
    private static async Task BackfillModulosDelPlanAsync(
        NeoStpDbContext db, Empresa empresa, ILogger logger, CancellationToken ct)
    {
        // Plan vigente de la empresa (cae al más reciente si no hay uno marcado ACTIVO).
        var empresaPlan = await db.EmpresaPlanes
            .Where(ep => ep.EmpresaId == empresa.Id)
            .OrderByDescending(ep => ep.EstadoCodigo == "ACTIVO")
            .ThenByDescending(ep => ep.FechaInicio)
            .FirstOrDefaultAsync(ct);
        if (empresaPlan is null)
        {
            logger.LogWarning("EmpresaPruebaSeeder: empresa {Nit} sin plan asignado; no se sincronizan módulos.", empresa.Nit);
            return;
        }

        var modulosDelPlan = await db.PlanModulos
            .Where(pm => pm.PlanId == empresaPlan.PlanId && pm.Activo)
            .Select(pm => pm.ModuloId)
            .ToListAsync(ct);

        var yaActivos = await db.EmpresaModulos
            .Where(em => em.EmpresaId == empresa.Id)
            .Select(em => em.ModuloId)
            .ToListAsync(ct);

        var faltantes = modulosDelPlan.Except(yaActivos).ToList();
        if (faltantes.Count == 0)
        {
            logger.LogInformation("EmpresaPruebaSeeder: empresa {Nit} ya tiene todos los módulos de su plan.", empresa.Nit);
            return;
        }

        var ahora = DateTime.UtcNow;
        foreach (var moduloId in faltantes)
        {
            db.EmpresaModulos.Add(new EmpresaModulo
            {
                EmpresaId       = empresa.Id,
                ModuloId        = moduloId,
                Activo          = true,
                FechaActivacion = ahora,
            });
        }
        await db.SaveChangesAsync(ct);

        logger.LogWarning(
            "EmpresaPruebaSeeder: empresa {Nit} — {Count} módulo(s) del plan activados: {Modulos}.",
            empresa.Nit, faltantes.Count, string.Join(", ", faltantes));
    }
}
