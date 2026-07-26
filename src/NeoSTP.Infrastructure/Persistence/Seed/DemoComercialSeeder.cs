using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Onboarding;
using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Clientes;
using NeoSTP.Domain.Core.Cobranza;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Licenciamiento;
using NeoSTP.Domain.Core.Pos;
using NeoSTP.Domain.Core.Productos;
using NeoSTP.Domain.Core.Seguridad;

namespace NeoSTP.Infrastructure.Persistence.Seed;

/// <summary>
/// Ambiente de demostración comercial: una empresa por punto de la escalera de precios,
/// cada una con su plan, su usuario y movimiento realista (facturas del mes, ventas POS,
/// cartera con vencidas). Sirve para que alguien de ventas recorra la oferta completa
/// entrando a cada cuenta y viendo qué desbloquea cada plan.
///
/// Idempotente: se identifica cada empresa por NIT; si ya existe, no la recrea.
/// Se activa con <c>DemoComercial:Enabled=true</c>. NUNCA borra empresas con documentos.
/// </summary>
public static class DemoComercialSeeder
{
    private const string Actor = "DEMO_COMERCIAL_SEEDER";

    /// <summary>Contraseña única para todas las cuentas demo (cumple la política: mayúscula, minúscula y dígito).</summary>
    public const string Password = "Demo2026$";

    private sealed record EmpresaDemo(
        string Nit,
        string RazonSocial,
        string NombreComercial,
        string PlanCodigo,
        string Username,
        string NombreUsuario,
        string Rubro,
        string? PlantillaVertical,
        string Pitch,
        bool ConCarteraVencida = false);

    private static readonly EmpresaDemo[] Catalogo =
    [
        new("06140101011001", "Tienda La Esquina, S.A. de C.V.", "Tienda La Esquina",
            "STARTER", "demo.starter", "Marta Recinos", "TIENDA", "tienda",
            "Entrada: solo facturación electrónica. El negocio que hoy factura a mano."),

        new("06140101011002", "Restaurante El Buen Sabor, S.A. de C.V.", "El Buen Sabor",
            "PYME", "demo.pos", "Julio Menéndez", "RESTAURANTE", "tienda",
            "Facturación electrónica + punto de venta. Cobra en mostrador y factura en el acto."),

        new("06140101011003", "Contadores Asociados, S.A. de C.V.", "Contadores Asociados",
            "CONTADOR", "demo.contador", "Lucía Portillo", "SERVICIOS", null,
            "Un contador, todos sus clientes: cambia de empresa sin salir y ve el consolidado del grupo."),

        new("06140101011004", "Grupo Comercial Vertical, S.A. de C.V.", "Grupo Vertical",
            "BUSINESSFULL", "demo.negocios", "Ricardo Alvarenga", "MIXTO", "farmacia",
            "Tipos de negocio: farmacia con lotes y vencimientos, ferretería con precios por volumen, salón con citas.",
            ConCarteraVencida: true),

        new("06140101011005", "Corporación Industrial Salvadoreña, S.A. de C.V.", "Corporación Industrial",
            "ENTERPRISE", "demo.enterprise", "Andrea Bonilla", "INDUSTRIA", null,
            "Todo: SSO corporativo, API para integrar, portal de clientes, sucursales y aprobaciones.",
            ConCarteraVencida: true),
    ];

    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NeoStpDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<NeoStpDbContext>>();
        var config = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var verticales = scope.ServiceProvider.GetService<IVerticalTemplateService>();

        if (!config.GetValue<bool>("DemoComercial:Enabled")) return;

        await LimpiarResidualesAsync(db, logger, ct);

        var roles = await db.Roles
            .Where(r => r.Codigo == "ADMIN" || r.Codigo == "CONTADOR")
            .ToDictionaryAsync(r => r.Codigo, r => r.Id, StringComparer.OrdinalIgnoreCase, ct);

        var creadas = new List<Empresa>();
        foreach (var def in Catalogo)
        {
            var empresa = await CrearEmpresaAsync(db, hasher, roles, def, logger, ct);
            if (empresa is not null) creadas.Add(empresa);

            if (def.PlantillaVertical is not null && verticales is not null && empresa is not null)
                await verticales.AplicarAsync(empresa.Id, def.PlantillaVertical, Actor, ct);
        }

        // El contador ve las demás empresas del demo (membresías E1) → enciende el consolidado E5.
        await VincularContadorAsync(db, roles, logger, ct);

        logger.LogWarning(
            "DemoComercialSeeder: ambiente de demostración listo. {N} empresas. Usuarios: {Users}. Password: {Pass}",
            Catalogo.Length, string.Join(", ", Catalogo.Select(c => c.Username)), Password);
    }

    /// <summary>
    /// Quita el arrastre de pruebas viejas: la empresa demo vacía y los usuarios sin empresa
    /// que no son SuperAdmin. Nunca toca una empresa que tenga documentos emitidos.
    /// </summary>
    private static async Task LimpiarResidualesAsync(NeoStpDbContext db, ILogger logger, CancellationToken ct)
    {
        var huerfanos = await db.Usuarios
            .Where(u => u.EmpresaId == null && u.TipoUsuarioCodigo != "SUPERADMIN")
            .ToListAsync(ct);
        if (huerfanos.Count > 0)
        {
            var roles = await db.UsuarioRoles
                .Where(ur => huerfanos.Select(h => h.Id).Contains(ur.UsuarioId)).ToListAsync(ct);
            db.UsuarioRoles.RemoveRange(roles);
            db.Usuarios.RemoveRange(huerfanos);
            logger.LogWarning("DemoComercialSeeder: eliminados {N} usuarios sin empresa: {Users}",
                huerfanos.Count, string.Join(", ", huerfanos.Select(u => u.Username)));
        }

        // Empresa demo original: solo se elimina si está realmente vacía de documentos.
        var demoVieja = await db.Empresas.FirstOrDefaultAsync(e => e.RazonSocial.StartsWith("Demo S.A."), ct);
        if (demoVieja is not null)
        {
            var tieneDocs = await db.DteDocumentos.AnyAsync(d => d.EmpresaId == demoVieja.Id, ct);
            var tieneVentas = await db.VentasPos.AnyAsync(v => v.EmpresaId == demoVieja.Id, ct);
            if (!tieneDocs && !tieneVentas)
            {
                db.Clientes.RemoveRange(await db.Clientes.Where(c => c.EmpresaId == demoVieja.Id).ToListAsync(ct));
                db.Productos.RemoveRange(await db.Productos.Where(p => p.EmpresaId == demoVieja.Id).ToListAsync(ct));
                db.EmpresaModulos.RemoveRange(await db.EmpresaModulos.Where(m => m.EmpresaId == demoVieja.Id).ToListAsync(ct));
                db.EmpresaPlanes.RemoveRange(await db.EmpresaPlanes.Where(p => p.EmpresaId == demoVieja.Id).ToListAsync(ct));
                db.PuntosVenta.RemoveRange(await db.PuntosVenta.Where(p => p.Sucursal.EmpresaId == demoVieja.Id).ToListAsync(ct));
                db.Sucursales.RemoveRange(await db.Sucursales.Where(s => s.EmpresaId == demoVieja.Id).ToListAsync(ct));
                db.DteConfiguracion.RemoveRange(await db.DteConfiguracion.Where(c => c.EmpresaId == demoVieja.Id).ToListAsync(ct));
                db.Empresas.Remove(demoVieja);
                logger.LogWarning("DemoComercialSeeder: eliminada la empresa demo vacía '{Razon}'.", demoVieja.RazonSocial);
            }
            else
            {
                logger.LogWarning("DemoComercialSeeder: '{Razon}' tiene documentos; se conserva.", demoVieja.RazonSocial);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task<Empresa?> CrearEmpresaAsync(
        NeoStpDbContext db, IPasswordHasher hasher, IReadOnlyDictionary<string, int> roles,
        EmpresaDemo def, ILogger logger, CancellationToken ct)
    {
        var existente = await db.Empresas.FirstOrDefaultAsync(e => e.Nit == def.Nit, ct);
        if (existente is not null) return existente;

        var plan = await db.Planes.Include(p => p.Modulos)
            .FirstOrDefaultAsync(p => p.Codigo == def.PlanCodigo, ct);
        if (plan is null)
        {
            logger.LogWarning("DemoComercialSeeder: plan '{Plan}' no existe; se omite {Razon}.", def.PlanCodigo, def.RazonSocial);
            return null;
        }

        var ahora = DateTime.UtcNow;
        var empresa = new Empresa
        {
            Nit = def.Nit,
            Nrc = def.Nit[..6],
            RazonSocial = def.RazonSocial,
            NombreComercial = def.NombreComercial,
            CodigoActividad = "47190",
            ActividadEconomica = "Venta al por menor en comercios no especializados",
            Departamento = "06",
            Municipio = "SAN_SALVADOR_CENTRO",
            Direccion = "San Salvador, El Salvador",
            Telefono = "2222-0000",
            Correo = $"{def.Username}@neostp.demo",
            EstadoCodigo = EmpresaEstados.Activa,
            CreatedAt = ahora,
            CreatedBy = Actor,
        };
        db.Empresas.Add(empresa);
        await db.SaveChangesAsync(ct);

        db.EmpresaPlanes.Add(new EmpresaPlan
        {
            EmpresaId = empresa.Id, PlanId = plan.Id,
            FechaInicio = ahora, FechaFin = ahora.AddYears(1),
            EstadoCodigo = "ACTIVO", CreatedAt = ahora, CreatedBy = Actor,
        });
        foreach (var pm in plan.Modulos.Where(m => m.Activo))
        {
            db.EmpresaModulos.Add(new EmpresaModulo
            {
                EmpresaId = empresa.Id, ModuloId = pm.ModuloId,
                Activo = true, FechaActivacion = ahora,
            });
        }

        var sucursal = new Sucursal
        {
            EmpresaId = empresa.Id, Codigo = "S001", Nombre = "Casa Matriz",
            TipoEstablecimientoCodigo = "01", CodigoEstablecimientoMh = "M001",
            Direccion = empresa.Direccion, Departamento = empresa.Departamento,
            Municipio = empresa.Municipio, Telefono = empresa.Telefono,
            EstadoCodigo = EstadoCodes.Activo, CreatedAt = ahora, CreatedBy = Actor,
        };
        db.Sucursales.Add(sucursal);
        await db.SaveChangesAsync(ct);

        // Enterprise y Business Full muestran la operación multi-sucursal.
        if (def.PlanCodigo is "ENTERPRISE" or "BUSINESSFULL")
        {
            db.Sucursales.Add(new Sucursal
            {
                EmpresaId = empresa.Id, Codigo = "S002", Nombre = "Sucursal Santa Ana",
                TipoEstablecimientoCodigo = "02", CodigoEstablecimientoMh = "M002",
                Direccion = "Santa Ana", Departamento = "02", Municipio = "SANTA_ANA_CENTRO",
                EstadoCodigo = EstadoCodes.Activo, CreatedAt = ahora, CreatedBy = Actor,
            });
            await db.SaveChangesAsync(ct);
        }

        var puntoVenta = new PuntoVenta
        {
            SucursalId = sucursal.Id, Codigo = "P001", Nombre = "Caja 1",
            CodigoPuntoVentaMh = "P001", EstadoCodigo = EstadoCodes.Activo,
            CreatedAt = ahora, CreatedBy = Actor,
        };
        db.PuntosVenta.Add(puntoVenta);

        var esContador = def.PlanCodigo == "CONTADOR";
        var usuario = new Usuario
        {
            EmpresaId = empresa.Id,
            Username = def.Username,
            Email = $"{def.Username}@neostp.demo",
            PasswordHash = hasher.Hash(Password),
            NombreCompleto = def.NombreUsuario,
            TipoUsuarioCodigo = "ADMIN",
            EstadoCodigo = EstadoCodes.Activo,
            CreatedAt = ahora, CreatedBy = Actor,
        };
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync(ct);

        if (roles.TryGetValue("ADMIN", out var rolAdminId))
            db.UsuarioRoles.Add(new UsuarioRol { UsuarioId = usuario.Id, RolId = rolAdminId, CreatedAt = ahora });

        db.DteConfiguracion.Add(new DteConfiguracion
        {
            EmpresaId = empresa.Id, AmbienteCodigo = "PRUEBAS",
            UsuarioMh = def.Nit, TipoEstablecimientoCodigo = "01",
            CodigoEstablecimientoMh = "M001", CodigoPuntoVentaMh = "P001",
            CreatedAt = ahora, CreatedBy = Actor,
        });
        await db.SaveChangesAsync(ct);

        await SembrarMovimientoAsync(db, empresa, sucursal.Id, puntoVenta.Id, def, ct);

        logger.LogWarning("DemoComercialSeeder: '{Razon}' creada — plan {Plan}, usuario {User}.",
            empresa.RazonSocial, plan.Codigo, usuario.Username);
        return empresa;
    }

    /// <summary>Clientes, productos, facturas del mes, cartera (con una vencida) y ventas POS.</summary>
    private static async Task SembrarMovimientoAsync(
        NeoStpDbContext db, Empresa empresa, int sucursalId, int puntoVentaId,
        EmpresaDemo def, CancellationToken ct)
    {
        var clientes = new[]
        {
            new Cliente
            {
                EmpresaId = empresa.Id, TipoDocumentoCodigo = "DUI", NumeroDocumento = "04512345-6",
                Nombre = "Sofía Ramírez", TipoContribuyenteCodigo = "CONSUMIDOR_FINAL",
                DepartamentoCodigo = "06", MunicipioCodigo = "SAN_SALVADOR_CENTRO",
                Direccion = "Col. Escalón, San Salvador", Correo = "sofia.ramirez@cliente.demo",
                Telefono = "7788-1122", Etiqueta = "FRECUENTE", CreatedBy = Actor,
            },
            new Cliente
            {
                EmpresaId = empresa.Id, TipoDocumentoCodigo = "NIT", NumeroDocumento = "06142509881021",
                Nrc = "240588-3", Nombre = "Distribuidora Nacional, S.A. de C.V.",
                TipoContribuyenteCodigo = "GRAN_CONTRIBUYENTE", CodigoActividad = "46900",
                ActividadEconomica = "Venta al por mayor", DepartamentoCodigo = "06",
                MunicipioCodigo = "SAN_SALVADOR_CENTRO", Direccion = "Blvd. Los Próceres",
                Correo = "compras@distribuidora.demo", Telefono = "2233-4455",
                Etiqueta = "VIP", CreatedBy = Actor,
            },
            new Cliente
            {
                EmpresaId = empresa.Id, TipoDocumentoCodigo = "NIT", NumeroDocumento = "06142509881022",
                Nrc = "310977-1", Nombre = "Comercial del Pacífico, S.A.",
                TipoContribuyenteCodigo = "CONTRIBUYENTE", CodigoActividad = "47190",
                ActividadEconomica = "Comercio al por menor", DepartamentoCodigo = "05",
                MunicipioCodigo = "LA_LIBERTAD_ESTE", Direccion = "Santa Tecla",
                Correo = "pagos@pacifico.demo", Telefono = "2255-6677", CreatedBy = Actor,
            },
        };
        db.Clientes.AddRange(clientes);

        var productos = ProductosDelRubro(empresa.Id, def.Rubro);
        db.Productos.AddRange(productos);
        await db.SaveChangesAsync(ct);

        var hoy = DateTime.UtcNow.Date;
        var inicioMes = new DateTime(hoy.Year, hoy.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var n = 0;

        // Facturas de consumidor final a lo largo del mes (contado).
        for (var i = 0; i < 6; i++)
        {
            var fecha = inicioMes.AddDays(Math.Min(i * 4 + 1, (hoy - inicioMes).Days));
            await CrearDteAsync(db, empresa, sucursalId, puntoVentaId, clientes[0],
                productos[i % productos.Length], TipoDteCodigos.FacturaConsumidorFinal,
                "1", null, fecha, 1m + i % 3, ++n, ct);
        }

        // CCF a crédito 30 días — cartera vigente.
        await CrearDteAsync(db, empresa, sucursalId, puntoVentaId, clientes[1],
            productos[0], TipoDteCodigos.ComprobanteCreditoFiscal, "2", 30,
            hoy.AddDays(-10), 12m, ++n, ct);

        // Solo algunas empresas arrastran mora: así el consolidado de grupo diferencia
        // las que van al día de las que el contador debe atender.
        if (def.ConCarteraVencida)
        {
            // CCF a crédito 15 días emitido hace 45 → VENCIDO, con abono parcial.
            var vencida = await CrearDteAsync(db, empresa, sucursalId, puntoVentaId, clientes[2],
                productos[productos.Length - 1], TipoDteCodigos.ComprobanteCreditoFiscal, "2", 15,
                hoy.AddDays(-45), 8m, ++n, ct);
            db.Set<PagoCliente>().Add(new PagoCliente
            {
                EmpresaId = empresa.Id, DteDocumentoId = vencida.Id,
                Fecha = DateOnly.FromDateTime(hoy.AddDays(-30)),
                Monto = Math.Round(vencida.TotalPagar * 0.4m, 2),
                FormaPagoCodigo = "TRANSFERENCIA", Referencia = "ABONO-001",
                EstadoCodigo = PagoEstados.Confirmado, CreatedBy = Actor,
            });
        }

        // Ventas de mostrador para los planes con POS.
        if (def.PlanCodigo is not "STARTER")
        {
            for (var i = 0; i < 4; i++)
            {
                var prod = productos[i % productos.Length];
                var cantidad = 1m + i;
                var subtotal = Math.Round(prod.PrecioUnitario / 1.13m * cantidad, 2);
                var iva = Math.Round(subtotal * 0.13m, 2);
                db.VentasPos.Add(new VentaPos
                {
                    EmpresaId = empresa.Id, SucursalId = sucursalId, PuntoVentaId = puntoVentaId,
                    Numero = $"POS-{DateTime.UtcNow:yyyyMM}-{i + 1:D4}",
                    Fecha = hoy.AddDays(-i),
                    ClienteNombre = "Consumidor final",
                    FormaPagoCodigo = i % 2 == 0 ? "EFECTIVO" : "TARJETA",
                    Subtotal = subtotal, IvaTotal = iva, Total = subtotal + iva,
                    EstadoCodigo = VentaPosEstados.Completada,
                    CreatedBy = Actor,
                });
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static Producto[] ProductosDelRubro(int empresaId, string rubro)
    {
        Producto P(string codigo, string nombre, decimal precio, decimal costo, bool servicio = false) => new()
        {
            EmpresaId = empresaId, CodigoInterno = codigo, Nombre = nombre,
            TipoItem = servicio ? "SERVICIO" : "BIEN", UnidadMedidaCodigo = "59",
            PrecioUnitario = precio, CostoUnitario = costo, AplicaIva = true,
            EstadoCodigo = EstadoCodes.Activo, CreatedBy = Actor,
        };

        return rubro switch
        {
            "RESTAURANTE" =>
            [
                P("PLATO-01", "Almuerzo ejecutivo", 6.50m, 3.10m),
                P("PLATO-02", "Pupusa revuelta", 1.00m, 0.35m),
                P("BEB-01", "Refresco natural 16 oz", 1.50m, 0.45m),
                P("POST-01", "Postre del día", 2.75m, 1.10m),
            ],
            "MIXTO" =>
            [
                P("FAR-01", "Acetaminofén 500mg (caja)", 3.25m, 1.60m),
                P("FER-01", "Cemento gris 42.5 kg", 9.80m, 7.20m),
                P("FER-02", "Varilla corrugada 3/8", 7.45m, 5.60m),
                P("SAL-01", "Corte y peinado", 15.00m, 4.00m, servicio: true),
            ],
            "INDUSTRIA" =>
            [
                P("IND-01", "Lámina galvanizada calibre 26", 24.50m, 18.30m),
                P("IND-02", "Perfil estructural 2x4", 31.00m, 23.80m),
                P("IND-03", "Servicio de corte industrial", 120.00m, 45.00m, servicio: true),
                P("IND-04", "Kit de tornillería (100 u)", 12.40m, 7.90m),
            ],
            "SERVICIOS" =>
            [
                P("SRV-01", "Contabilidad mensual", 150.00m, 60.00m, servicio: true),
                P("SRV-02", "Declaración de IVA", 45.00m, 15.00m, servicio: true),
                P("SRV-03", "Asesoría tributaria (hora)", 35.00m, 12.00m, servicio: true),
                P("SRV-04", "Elaboración de planilla", 75.00m, 25.00m, servicio: true),
            ],
            _ =>
            [
                P("ABA-01", "Arroz 5 lb", 4.25m, 3.10m),
                P("ABA-02", "Aceite 1 L", 3.10m, 2.30m),
                P("ABA-03", "Azúcar 5 lb", 3.85m, 2.90m),
                P("ABA-04", "Café molido 1 lb", 5.50m, 3.80m),
            ],
        };
    }

    private static async Task<DteDocumento> CrearDteAsync(
        NeoStpDbContext db, Empresa empresa, int sucursalId, int puntoVentaId,
        Cliente cliente, Producto producto, string tipoDte, string condicion,
        int? plazoDias, DateTime fecha, decimal cantidad, int correlativo, CancellationToken ct)
    {
        var doc = new DteDocumento
        {
            EmpresaId = empresa.Id, SucursalId = sucursalId, PuntoVentaId = puntoVentaId,
            TipoDteCodigo = tipoDte, AmbienteCodigo = "PRUEBAS",
            NumeroControl = $"DTE-{tipoDte}-M001P001-{empresa.Id:D6}{correlativo:D9}",
            CodigoGeneracion = Guid.NewGuid().ToString().ToUpperInvariant(),
            SelloRecibido = $"DEMO{Guid.NewGuid():N}"[..20].ToUpperInvariant(),
            FechaEmision = fecha,
            HoraEmision = new TimeSpan(9 + correlativo % 8, 30, 0),
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
            ReceptorDireccion = cliente.Direccion,
            ReceptorCorreo = cliente.Correo,
            ReceptorTelefono = cliente.Telefono,
            CondicionOperacionCodigo = condicion,
            FormaPagoCodigo = condicion == "2" ? "CREDITO" : "EFECTIVO",
            PlazoDias = plazoDias,
            EstadoCodigo = DteEstadoCodigos.Procesado,
            GeneradoAt = fecha.AddMinutes(1),
            ValidadoAt = fecha.AddMinutes(2),
            EnviadoAt = fecha.AddMinutes(3),
            ProcesadoAt = fecha.AddMinutes(4),
            CreatedBy = Actor,
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
            PrecioUnitario = producto.PrecioUnitario,
            MontoDescuento = 0,
            NoGravado = !producto.AplicaIva,
            CreatedBy = Actor,
        });

        new DteCalculator().Recalcular(doc);
        db.DteDocumentos.Add(doc);
        await db.SaveChangesAsync(ct);
        return doc;
    }

    /// <summary>El usuario contador queda como miembro externo de las otras empresas demo (E1).</summary>
    private static async Task VincularContadorAsync(
        NeoStpDbContext db, IReadOnlyDictionary<string, int> roles, ILogger logger, CancellationToken ct)
    {
        var contador = await db.Usuarios.FirstOrDefaultAsync(u => u.Username == "demo.contador", ct);
        if (contador is null || !roles.TryGetValue("CONTADOR", out var rolContadorId)) return;

        var nits = Catalogo.Where(c => c.PlanCodigo != "CONTADOR").Select(c => c.Nit).ToList();
        var otras = await db.Empresas.Where(e => nits.Contains(e.Nit)).ToListAsync(ct);

        var vinculadas = 0;
        foreach (var empresa in otras)
        {
            if (await db.UsuarioEmpresas.AnyAsync(m => m.UsuarioId == contador.Id && m.EmpresaId == empresa.Id, ct))
                continue;
            db.UsuarioEmpresas.Add(new UsuarioEmpresa
            {
                UsuarioId = contador.Id, EmpresaId = empresa.Id,
                RolId = rolContadorId, EstadoCodigo = "ACTIVO", CreatedBy = Actor,
            });
            vinculadas++;
        }

        if (vinculadas > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogWarning("DemoComercialSeeder: 'demo.contador' vinculado a {N} empresas del grupo.", vinculadas);
        }
    }
}
