using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Pos;
using NeoSTP.Application.Provisioning;
using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Clientes;
using NeoSTP.Domain.Core.Cobranza;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Licenciamiento;
using NeoSTP.Domain.Core.Notificaciones;
using NeoSTP.Domain.Core.Pos;
using NeoSTP.Domain.Core.Productos;
using NeoSTP.Domain.Core.Scan;
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
            if (options.MobileDemo.Enabled)
                await EnsureMobileDemoDataAsync(db, existente, hasher, options, logger, ct);
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

        if (options.MobileDemo.Enabled)
            await EnsureMobileDemoDataAsync(db, empresa, hasher, options, logger, ct);

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
    // Datos demo mobile opt-in e idempotentes.
    private static async Task EnsureMobileDemoDataAsync(
        NeoStpDbContext db,
        Empresa empresa,
        IPasswordHasher hasher,
        EmpresaPruebaOptions options,
        ILogger logger,
        CancellationToken ct)
    {
        const string actor = "MOBILE_DEMO_SEEDER";

        await EnsureMobileDemoUsersAsync(db, empresa.Id, hasher, options.MobileDemo.Password, actor, ct);

        var sucursal = await db.Sucursales
            .FirstOrDefaultAsync(s => s.EmpresaId == empresa.Id && s.Codigo == options.Sucursal.Codigo, ct)
            ?? await db.Sucursales.FirstOrDefaultAsync(s => s.EmpresaId == empresa.Id, ct);
        var puntoVenta = sucursal is null
            ? null
            : await db.PuntosVenta.FirstOrDefaultAsync(p => p.SucursalId == sucursal.Id && p.Codigo == options.PuntoVenta.Codigo, ct)
              ?? await db.PuntosVenta.FirstOrDefaultAsync(p => p.SucursalId == sucursal.Id, ct);

        var clientes = await EnsureMobileClientesAsync(db, empresa.Id, actor, ct);
        var productos = await EnsureMobileProductosAsync(db, empresa.Id, actor, ct);
        var (_, credito) = await EnsureMobileDtesAsync(db, empresa, sucursal?.Id, puntoVenta?.Id, clientes, productos, actor, ct);

        await EnsureMobileCobrosAsync(db, empresa.Id, credito, actor, ct);
        await EnsureMobilePosAsync(db, empresa.Id, sucursal?.Id, puntoVenta?.Id, clientes[0], productos[0], actor, ct);
        await EnsureMobileScanAsync(db, empresa.Id, actor, ct);
        await EnsureMobileAlertasAsync(db, empresa.Id, credito.Id, actor, ct);

        logger.LogWarning(
            "EmpresaPruebaSeeder: datos demo mobile asegurados para empresa {Nit}. Usuarios: mobile.admin, mobile.dte.consulta, mobile.pos, mobile.cobros, mobile.scan, mobile.limitado.",
            empresa.Nit);
    }

    private static async Task EnsureMobileDemoUsersAsync(
        NeoStpDbContext db,
        int empresaId,
        IPasswordHasher hasher,
        string password,
        string actor,
        CancellationToken ct)
    {
        var roles = (await db.Roles
                .Where(r => r.Codigo == "ADMIN" || r.Codigo == "OPERADOR" || r.Codigo == "CONTADOR" || r.Codigo == "READONLY")
                .ToListAsync(ct))
            .ToDictionary(r => r.Codigo, r => r.Id, StringComparer.OrdinalIgnoreCase);

        await EnsureMobileUserAsync(db, empresaId, "mobile.admin", "mobile.admin@demo.local", "Mobile Admin", "ADMIN", "ADMIN", password, hasher, roles, actor, ct);
        await EnsureMobileUserAsync(db, empresaId, "mobile.dte.consulta", "mobile.dte.consulta@demo.local", "Mobile Consulta DTE", "CONTADOR", "CONTADOR", password, hasher, roles, actor, ct);
        await EnsureMobileUserAsync(db, empresaId, "mobile.pos", "mobile.pos@demo.local", "Mobile POS", "OPERADOR", "OPERADOR", password, hasher, roles, actor, ct);
        await EnsureMobileUserAsync(db, empresaId, "mobile.cobros", "mobile.cobros@demo.local", "Mobile Cobros", "OPERADOR", "OPERADOR", password, hasher, roles, actor, ct);
        await EnsureMobileUserAsync(db, empresaId, "mobile.scan", "mobile.scan@demo.local", "Mobile Scan", "OPERADOR", "OPERADOR", password, hasher, roles, actor, ct);
        await EnsureMobileUserAsync(db, empresaId, "mobile.limitado", "mobile.limitado@demo.local", "Mobile Limitado", "READONLY", "READONLY", password, hasher, roles, actor, ct);
    }

    private static async Task EnsureMobileUserAsync(
        NeoStpDbContext db,
        int empresaId,
        string username,
        string email,
        string nombre,
        string tipoUsuario,
        string roleCode,
        string password,
        IPasswordHasher hasher,
        IReadOnlyDictionary<string, int> roles,
        string actor,
        CancellationToken ct)
    {
        var user = await db.Usuarios.FirstOrDefaultAsync(u => u.EmpresaId == empresaId && u.Username == username, ct);
        if (user is null)
        {
            user = new Usuario
            {
                EmpresaId = empresaId,
                Username = username,
                Email = email,
                PasswordHash = hasher.Hash(password),
                NombreCompleto = nombre,
                TipoUsuarioCodigo = tipoUsuario,
                EstadoCodigo = EstadoCodes.Activo,
                CreatedBy = actor,
            };
            db.Usuarios.Add(user);
            await db.SaveChangesAsync(ct);
        }

        if (roles.TryGetValue(roleCode, out var roleId) &&
            !await db.UsuarioRoles.AnyAsync(ur => ur.UsuarioId == user.Id && ur.RolId == roleId, ct))
        {
            db.UsuarioRoles.Add(new UsuarioRol { UsuarioId = user.Id, RolId = roleId, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync(ct);
        }
    }

    private static async Task<List<Cliente>> EnsureMobileClientesAsync(NeoStpDbContext db, int empresaId, string actor, CancellationToken ct)
    {
        var defs = new[]
        {
            new Cliente { EmpresaId = empresaId, TipoDocumentoCodigo = "DUI", NumeroDocumento = "01010101-1", Nombre = "Consumidor Demo Mobile", TipoContribuyenteCodigo = "CONSUMIDOR_FINAL", DepartamentoCodigo = "06", MunicipioCodigo = "SAN_SALVADOR_CENTRO", Direccion = "San Salvador", Correo = "cliente.mobile@demo.local", Telefono = "7000-0001", Etiqueta = "FRECUENTE", CreatedBy = actor },
            new Cliente { EmpresaId = empresaId, TipoDocumentoCodigo = "NIT", NumeroDocumento = "0614-010101-101-1", Nrc = "123456-7", Nombre = "Comercial Demo Mobile SA de CV", NombreComercial = "Comercial Mobile", TipoContribuyenteCodigo = "CONTRIBUYENTE", CodigoActividad = "62010", ActividadEconomica = "Servicios de tecnologia", DepartamentoCodigo = "06", MunicipioCodigo = "SAN_SALVADOR_CENTRO", Direccion = "Colonia Escalon", Correo = "cxc.mobile@demo.local", Telefono = "7000-0002", Etiqueta = "VIP", CreatedBy = actor },
            new Cliente { EmpresaId = empresaId, TipoDocumentoCodigo = "DUI", NumeroDocumento = "02020202-2", Nombre = "Cliente POS Demo", TipoContribuyenteCodigo = "CONSUMIDOR_FINAL", DepartamentoCodigo = "05", MunicipioCodigo = "LA_LIBERTAD_ESTE", Direccion = "Santa Tecla", Correo = "pos.mobile@demo.local", Telefono = "7000-0003", CreatedBy = actor },
        };

        foreach (var cliente in defs)
        {
            if (!await db.Clientes.AnyAsync(c => c.EmpresaId == empresaId &&
                                                 c.TipoDocumentoCodigo == cliente.TipoDocumentoCodigo &&
                                                 c.NumeroDocumento == cliente.NumeroDocumento, ct))
            {
                db.Clientes.Add(cliente);
            }
        }
        await db.SaveChangesAsync(ct);

        return await db.Clientes
            .Where(c => c.EmpresaId == empresaId &&
                        (c.NumeroDocumento == "01010101-1" ||
                         c.NumeroDocumento == "0614-010101-101-1" ||
                         c.NumeroDocumento == "02020202-2"))
            .OrderBy(c => c.Id)
            .ToListAsync(ct);
    }

    private static async Task<List<Producto>> EnsureMobileProductosAsync(NeoStpDbContext db, int empresaId, string actor, CancellationToken ct)
    {
        var defs = new[]
        {
            new Producto { EmpresaId = empresaId, CodigoInterno = "MOB-CAFE", CodigoBarra = "770000000001", Nombre = "Cafe demo 12 oz", TipoItem = "BIEN", UnidadMedidaCodigo = "59", PrecioUnitario = 11.30m, CostoUnitario = 6.50m, AplicaIva = true, EstadoCodigo = EstadoCodes.Activo, CreatedBy = actor },
            new Producto { EmpresaId = empresaId, CodigoInterno = "MOB-PAPEL", CodigoBarra = "770000000002", Nombre = "Resma papel carta", TipoItem = "BIEN", UnidadMedidaCodigo = "59", PrecioUnitario = 6.75m, CostoUnitario = 4.20m, AplicaIva = true, EstadoCodigo = EstadoCodes.Activo, CreatedBy = actor },
            new Producto { EmpresaId = empresaId, CodigoInterno = "MOB-SOPORTE", Nombre = "Soporte tecnico remoto", TipoItem = "SERVICIO", UnidadMedidaCodigo = "59", PrecioUnitario = 100m, CostoUnitario = 35m, AplicaIva = true, EstadoCodigo = EstadoCodes.Activo, CreatedBy = actor },
            new Producto { EmpresaId = empresaId, CodigoInterno = "MOB-USB", CodigoBarra = "770000000003", Nombre = "Memoria USB 64GB", TipoItem = "BIEN", UnidadMedidaCodigo = "59", PrecioUnitario = 15.99m, CostoUnitario = 9.80m, AplicaIva = true, EstadoCodigo = EstadoCodes.Activo, CreatedBy = actor },
            new Producto { EmpresaId = empresaId, CodigoInterno = "MOB-ENVIO", Nombre = "Envio local", TipoItem = "SERVICIO", UnidadMedidaCodigo = "59", PrecioUnitario = 4.00m, CostoUnitario = 2.00m, AplicaIva = false, EstadoCodigo = EstadoCodes.Activo, CreatedBy = actor },
        };

        foreach (var producto in defs)
        {
            if (!await db.Productos.AnyAsync(p => p.EmpresaId == empresaId && p.CodigoInterno == producto.CodigoInterno, ct))
                db.Productos.Add(producto);
        }
        await db.SaveChangesAsync(ct);

        var codigos = defs.Select(p => p.CodigoInterno).ToArray();
        return await db.Productos
            .Where(p => p.EmpresaId == empresaId && codigos.Contains(p.CodigoInterno))
            .OrderBy(p => p.CodigoInterno)
            .ToListAsync(ct);
    }

    private static async Task<(DteDocumento Factura, DteDocumento Credito)> EnsureMobileDtesAsync(
        NeoStpDbContext db,
        Empresa empresa,
        int? sucursalId,
        int? puntoVentaId,
        IReadOnlyList<Cliente> clientes,
        IReadOnlyList<Producto> productos,
        string actor,
        CancellationToken ct)
    {
        var factura = await EnsureMobileDteAsync(db, empresa.Id, sucursalId, puntoVentaId, clientes[0], productos[0], TipoDteCodigos.FacturaConsumidorFinal, "1", null, "DTE-01-MDEMO-000000000000001", DemoCodigoGeneracion("11111111", "4111", "8111", empresa.Id), "Sello demo factura mobile", 2m, 11.30m, actor, ct);
        var credito = await EnsureMobileDteAsync(db, empresa.Id, sucursalId, puntoVentaId, clientes[1], productos[2], TipoDteCodigos.ComprobanteCreditoFiscal, "2", 15, "DTE-03-MDEMO-000000000000002", DemoCodigoGeneracion("22222222", "4222", "8222", empresa.Id), "Sello demo credito mobile", 1m, 100m, actor, ct);
        return (factura, credito);
    }

    private static async Task<DteDocumento> EnsureMobileDteAsync(
        NeoStpDbContext db,
        int empresaId,
        int? sucursalId,
        int? puntoVentaId,
        Cliente cliente,
        Producto producto,
        string tipoDte,
        string condicion,
        int? plazoDias,
        string numeroControl,
        string codigoGeneracion,
        string sello,
        decimal cantidad,
        decimal precioUnitario,
        string actor,
        CancellationToken ct)
    {
        var existing = await db.DteDocumentos.FirstOrDefaultAsync(d => d.EmpresaId == empresaId && d.NumeroControl == numeroControl, ct);
        if (existing is not null) return existing;

        var now = DateTime.UtcNow;
        var doc = new DteDocumento
        {
            EmpresaId = empresaId,
            SucursalId = sucursalId,
            PuntoVentaId = puntoVentaId,
            TipoDteCodigo = tipoDte,
            AmbienteCodigo = "PRUEBAS",
            NumeroControl = numeroControl,
            CodigoGeneracion = codigoGeneracion,
            SelloRecibido = sello,
            FechaEmision = now.AddDays(plazoDias is null ? -1 : -20),
            HoraEmision = now.TimeOfDay,
            TipoMonedaCodigo = "USD",
            ClienteId = cliente.Id,
            ReceptorTipoDocumento = cliente.TipoDocumentoCodigo,
            ReceptorNumeroDocumento = cliente.NumeroDocumento,
            ReceptorNrc = cliente.Nrc,
            ReceptorNombre = cliente.Nombre,
            ReceptorTipoContribuyente = cliente.TipoContribuyenteCodigo,
            ReceptorCodigoActividad = cliente.CodigoActividad,
            ReceptorActividadEconomica = cliente.ActividadEconomica,
            ReceptorDepartamentoCodigo = cliente.DepartamentoCodigo,
            ReceptorMunicipioCodigo = cliente.MunicipioCodigo,
            ReceptorDistritoCodigo = cliente.DistritoCodigo,
            ReceptorDireccion = cliente.Direccion,
            ReceptorCorreo = cliente.Correo,
            ReceptorTelefono = cliente.Telefono,
            CondicionOperacionCodigo = condicion,
            FormaPagoCodigo = condicion == "2" ? "CREDITO" : "EFECTIVO",
            PlazoDias = plazoDias,
            EstadoCodigo = DteEstadoCodigos.Procesado,
            GeneradoAt = now.AddMinutes(-5),
            ValidadoAt = now.AddMinutes(-4),
            EnviadoAt = now.AddMinutes(-3),
            ProcesadoAt = now.AddMinutes(-2),
            Observaciones = "DTE demo mobile generado por seeder.",
            CreatedBy = actor,
        };
        doc.Detalles.Add(new DteDocumentoDetalle
        {
            NumeroLinea = 1,
            ProductoId = producto.Id,
            TipoItem = producto.EsServicio ? 2 : 1,
            Codigo = producto.CodigoInterno,
            Descripcion = producto.Nombre,
            UnidadMedidaCodigo = producto.UnidadMedidaCodigo,
            Cantidad = cantidad,
            PrecioUnitario = precioUnitario,
            MontoDescuento = 0,
            NoGravado = !producto.AplicaIva,
            CreatedBy = actor,
        });

        new DteCalculator().Recalcular(doc);
        doc.Json = new DteDocumentoJson
        {
            JsonDte = DemoJson(numeroControl, doc.TotalPagar),
            JsonFirmado = $"demo-jws-{codigoGeneracion}",
            RespuestaHacienda = "{\"estado\":\"PROCESADO\",\"descripcion\":\"Demo mobile\"}",
            GeneradoAt = doc.GeneradoAt ?? now,
            FirmadoAt = doc.ValidadoAt,
            RespuestaAt = doc.ProcesadoAt,
            CreatedBy = actor,
        };

        db.DteDocumentos.Add(doc);
        await db.SaveChangesAsync(ct);
        return doc;
    }

    private static async Task EnsureMobileCobrosAsync(NeoStpDbContext db, int empresaId, DteDocumento credito, string actor, CancellationToken ct)
    {
        if (!await db.CuentasCobro.AnyAsync(c => c.EmpresaId == empresaId && c.Nombre == "Demo Transferencia Mobile", ct))
        {
            db.CuentasCobro.Add(new CuentaCobro
            {
                EmpresaId = empresaId,
                Tipo = CuentaCobroTipos.Transferencia,
                Nombre = "Demo Transferencia Mobile",
                Banco = "Banco Demo",
                NumeroCuenta = "000-000000-0",
                Titular = "NeoSTP Demo",
                EstadoCodigo = EstadoCodes.Activo,
                CreatedBy = actor,
            });
        }

        if (!await db.PagosCliente.AnyAsync(p => p.EmpresaId == empresaId && p.DteDocumentoId == credito.Id && p.Referencia == "MOBILE-DEMO-PARCIAL", ct))
        {
            db.PagosCliente.Add(new PagoCliente
            {
                EmpresaId = empresaId,
                DteDocumentoId = credito.Id,
                Fecha = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3)),
                Monto = 40m,
                FormaPagoCodigo = "TRANSFERENCIA",
                Referencia = "MOBILE-DEMO-PARCIAL",
                Nota = "Pago parcial demo mobile",
                EstadoCodigo = PagoEstados.Confirmado,
                CreatedBy = actor,
            });
        }
        await db.SaveChangesAsync(ct);
    }

    private static async Task EnsureMobilePosAsync(NeoStpDbContext db, int empresaId, int? sucursalId, int? puntoVentaId, Cliente cliente, Producto producto, string actor, CancellationToken ct)
    {
        var closed = await db.SesionesCaja.FirstOrDefaultAsync(c => c.EmpresaId == empresaId && c.Numero == "CAJA-MOB-000001", ct);
        if (closed is null)
        {
            closed = new SesionCaja
            {
                EmpresaId = empresaId,
                SucursalId = sucursalId,
                PuntoVentaId = puntoVentaId,
                Numero = "CAJA-MOB-000001",
                EstadoCodigo = SesionCajaEstados.Cerrada,
                AbiertaAt = DateTime.UtcNow.AddHours(-8),
                MontoInicial = 100m,
                AbiertaPor = "mobile.pos",
                CerradaAt = DateTime.UtcNow.AddHours(-1),
                MontoEsperado = 122.60m,
                MontoContado = 122.60m,
                Diferencia = 0m,
                CerradaPor = "mobile.pos",
                Nota = "Corte demo mobile cerrado",
                CreatedBy = actor,
            };
            db.SesionesCaja.Add(closed);
            await db.SaveChangesAsync(ct);
        }

        if (!await db.SesionesCaja.AnyAsync(c => c.EmpresaId == empresaId && c.Numero == "CAJA-MOB-000002", ct))
        {
            db.SesionesCaja.Add(new SesionCaja
            {
                EmpresaId = empresaId,
                SucursalId = sucursalId,
                PuntoVentaId = puntoVentaId,
                Numero = "CAJA-MOB-000002",
                EstadoCodigo = SesionCajaEstados.Abierta,
                AbiertaAt = DateTime.UtcNow.AddMinutes(-30),
                MontoInicial = 75m,
                AbiertaPor = "mobile.pos",
                Nota = "Caja abierta para demo mobile",
                CreatedBy = actor,
            });
        }

        if (!await db.VentasPos.AnyAsync(v => v.EmpresaId == empresaId && v.Numero == "POS-MOB-000001", ct))
        {
            var input = new PosCalculator.LineaInput(2m, producto.PrecioUnitario, 0m, producto.AplicaIva);
            var linea = PosCalculator.CalcularLinea(input, 0.13m);
            var venta = PosCalculator.CalcularVenta([input], 0.13m);
            db.VentasPos.Add(new VentaPos
            {
                EmpresaId = empresaId,
                SucursalId = sucursalId,
                PuntoVentaId = puntoVentaId,
                Numero = "POS-MOB-000001",
                Fecha = DateTime.UtcNow.AddHours(-2),
                ClienteId = cliente.Id,
                ClienteNombre = cliente.Nombre,
                FormaPagoCodigo = FormasPagoPos.Efectivo,
                EfectivoRecibido = 25m,
                Cambio = 2.40m,
                Subtotal = venta.Subtotal,
                IvaTotal = venta.IvaTotal,
                TotalDescuento = venta.TotalDescuento,
                Total = venta.Total,
                EstadoCodigo = VentaPosEstados.Completada,
                EstadoFacturacion = VentaPosFacturacion.NoFacturada,
                SesionCajaId = closed.Id,
                CreatedBy = actor,
                Lineas =
                {
                    new VentaPosLinea
                    {
                        ProductoId = producto.Id,
                        Codigo = producto.CodigoInterno,
                        Descripcion = producto.Nombre,
                        Cantidad = 2m,
                        PrecioUnitario = producto.PrecioUnitario,
                        Descuento = 0m,
                        AplicaIva = producto.AplicaIva,
                        IvaLinea = linea.IvaLinea,
                        Total = linea.Total,
                        CreatedBy = actor,
                    },
                },
            });
        }
        await db.SaveChangesAsync(ct);
    }

    private static async Task EnsureMobileScanAsync(NeoStpDbContext db, int empresaId, string actor, CancellationToken ct)
    {
        if (await db.ScanDocumentos.AnyAsync(s => s.EmpresaId == empresaId && s.ArchivoNombre == "demo-factura-proveedor.pdf", ct))
            return;

        db.ScanDocumentos.Add(new ScanDocumento
        {
            EmpresaId = empresaId,
            EstadoCodigo = ScanEstados.RequiereRevision,
            TipoClasificacion = ScanTipos.DteRecibido,
            Origen = "MOBILE",
            ArchivoBlob = "%PDF-1.4 demo mobile scan"u8.ToArray(),
            ArchivoContentType = "application/pdf",
            ArchivoNombre = "demo-factura-proveedor.pdf",
            EmisorNombre = "Proveedor Demo Mobile",
            EmisorNit = "0614-020202-102-2",
            EmisorNrc = "765432-1",
            Fecha = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)),
            TipoDocumento = "03",
            NumeroControl = "DTE-03-PROV-000000000000123",
            Subtotal = 100m,
            Iva = 13m,
            Total = 113m,
            Confianza = 0.62m,
            Notas = "Demo mobile: revisar y confirmar.",
            CreatedBy = actor,
        });
        await db.SaveChangesAsync(ct);
    }

    private static async Task EnsureMobileAlertasAsync(NeoStpDbContext db, int empresaId, int dteCreditoId, string actor, CancellationToken ct)
    {
        var clave = $"MOBILE_DEMO:FACTURA_VENCIDA:{dteCreditoId}";
        if (await db.Alertas.AnyAsync(a => a.EmpresaId == empresaId && a.Clave == clave, ct))
            return;

        db.Alertas.Add(new Alerta
        {
            EmpresaId = empresaId,
            TipoCodigo = AlertaTipos.FacturaVencida,
            Severidad = AlertaSeveridades.Advertencia,
            Titulo = "Factura demo vencida",
            Mensaje = "La factura de credito demo mobile tiene saldo pendiente.",
            EntidadTipo = "DteDocumento",
            EntidadId = dteCreditoId,
            EstadoCodigo = AlertaEstados.Pendiente,
            Clave = clave,
            CreatedBy = actor,
        });
        await db.SaveChangesAsync(ct);
    }

    private static string DemoJson(string numeroControl, decimal total)
        => "{\"identificacion\":{\"numeroControl\":\"" + numeroControl + "\"},\"resumen\":{\"totalPagar\":" +
           total.ToString(CultureInfo.InvariantCulture) + "}}";

    private static string DemoCodigoGeneracion(string prefix, string versionSegment, string variantSegment, int empresaId)
        => $"{prefix}-{prefix[..4]}-{versionSegment}-{variantSegment}-{empresaId:000000000000}";

    /// <summary>
    /// Activa en EmpresaModulos los modulos del plan vigente de la empresa que aun no esten presentes.
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
