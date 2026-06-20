using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Provisioning;
using NeoSTP.Domain.Core.Compras;
using NeoSTP.Domain.Core.Crm;
using NeoSTP.Domain.Core.Inventario;
using NeoSTP.Domain.Core.Licenciamiento;
using NeoSTP.Domain.Core.Notificaciones;
using NeoSTP.Domain.Core.Portal;
using NeoSTP.Domain.Core.Profit;
using NeoSTP.Domain.Core.Rrhh;
using NeoSTP.Domain.Core.Seguridad;
using NeoSTP.Domain.Core.Tesoreria;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Persistence.Seed;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Provisioning;

public class EmpresaPruebaSeederTests
{
    private static ServiceProvider BuildProvider(string dbName, EmpresaPruebaOptions opts)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<NeoStpDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddSingleton(Options.Create(opts));

        var hasher = Substitute.For<IPasswordHasher>();
        hasher.Hash(Arg.Any<string>()).Returns(ci => "HASHED::" + ci.Arg<string>());
        services.AddSingleton(hasher);

        return services.BuildServiceProvider();
    }

    private static EmpresaPruebaOptions DefaultOpts() => new()
    {
        Enabled = true,
        Nit = "06140000000000",
        Nrc = "000000-0",
        RazonSocial = "NeoSTP Pruebas, S.A. de C.V.",
        PlanCodigo = "ENTERPRISE",
        Admin = new() { Username = "admin.prueba", Password = "ChangeMe!2026" },
        Sucursal = new() { Codigo = "0001", Nombre = "Casa Matriz" },
        PuntoVenta = new() { Codigo = "0001", Nombre = "Principal" },
        Dte = new() { AmbienteCodigo = "PRUEBAS", UsuarioMh = "06140000000000" },
    };

    /// <summary>Siembra el plan ENTERPRISE con 2 módulos y el rol ADMIN en la BD InMemory.</summary>
    private static async Task SeedPlanYRolAsync(IServiceProvider sp)
    {
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NeoStpDbContext>();

        db.Modulos.AddRange(
            new Modulo { Id = 100, Codigo = "CORE", Nombre = "Core", Activo = true, CreatedAt = DateTime.UtcNow },
            new Modulo { Id = 101, Codigo = "NEODTE", Nombre = "NeoDTE", Activo = true, CreatedAt = DateTime.UtcNow });
        db.Planes.Add(new Plan
        {
            Id = 204, Codigo = "ENTERPRISE", Nombre = "Enterprise", PrecioMensual = 400m,
            Activo = true, CreatedAt = DateTime.UtcNow,
            Modulos = { new PlanModulo { PlanId = 204, ModuloId = 100, Activo = true },
                        new PlanModulo { PlanId = 204, ModuloId = 101, Activo = true } },
        });
        db.Roles.Add(new Rol { Id = 501, Codigo = "ADMIN", Nombre = "Administrador", EsSistema = true, Activo = true, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }

    private static async Task SeedPlanYRolesMobileAsync(IServiceProvider sp)
    {
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NeoStpDbContext>();

        db.Modulos.AddRange(
            new Modulo { Id = 100, Codigo = "CORE", Nombre = "Core", Activo = true, CreatedAt = DateTime.UtcNow },
            new Modulo { Id = 101, Codigo = "NEODTE", Nombre = "NeoDTE", Activo = true, CreatedAt = DateTime.UtcNow },
            new Modulo { Id = 102, Codigo = "NEOPOS", Nombre = "NeoPOS", Activo = true, CreatedAt = DateTime.UtcNow },
            new Modulo { Id = 103, Codigo = "NEOSCANAI", Nombre = "NeoScanAI", Activo = true, CreatedAt = DateTime.UtcNow },
            new Modulo { Id = 104, Codigo = "NEOPROFIT", Nombre = "NeoProfit", Activo = true, CreatedAt = DateTime.UtcNow },
            new Modulo { Id = 105, Codigo = "NEOBI", Nombre = "NeoBI", Activo = true, CreatedAt = DateTime.UtcNow },
            new Modulo { Id = 107, Codigo = "NEOPORTAL", Nombre = "NeoPortal", Activo = true, CreatedAt = DateTime.UtcNow },
            new Modulo { Id = 110, Codigo = "INVENTARIO", Nombre = "Inventario", Activo = true, CreatedAt = DateTime.UtcNow },
            new Modulo { Id = 111, Codigo = "COMPRAS", Nombre = "Compras", Activo = true, CreatedAt = DateTime.UtcNow },
            new Modulo { Id = 113, Codigo = "NEORRHH", Nombre = "NeoRRHH", Activo = true, CreatedAt = DateTime.UtcNow },
            new Modulo { Id = 114, Codigo = "NEOCRM", Nombre = "NeoCRM", Activo = true, CreatedAt = DateTime.UtcNow },
            new Modulo { Id = 115, Codigo = "NEOTESORERIA", Nombre = "NeoTesoreria", Activo = true, CreatedAt = DateTime.UtcNow },
            new Modulo { Id = 116, Codigo = "NEOCONTA", Nombre = "NeoConta", Activo = true, CreatedAt = DateTime.UtcNow });
        db.Planes.Add(new Plan
        {
            Id = 204, Codigo = "ENTERPRISE", Nombre = "Enterprise", PrecioMensual = 400m,
            Activo = true, CreatedAt = DateTime.UtcNow,
            Modulos =
            {
                new PlanModulo { PlanId = 204, ModuloId = 100, Activo = true },
                new PlanModulo { PlanId = 204, ModuloId = 101, Activo = true },
                new PlanModulo { PlanId = 204, ModuloId = 102, Activo = true },
                new PlanModulo { PlanId = 204, ModuloId = 103, Activo = true },
                new PlanModulo { PlanId = 204, ModuloId = 104, Activo = true },
                new PlanModulo { PlanId = 204, ModuloId = 105, Activo = true },
                new PlanModulo { PlanId = 204, ModuloId = 107, Activo = true },
                new PlanModulo { PlanId = 204, ModuloId = 110, Activo = true },
                new PlanModulo { PlanId = 204, ModuloId = 111, Activo = true },
                new PlanModulo { PlanId = 204, ModuloId = 113, Activo = true },
                new PlanModulo { PlanId = 204, ModuloId = 114, Activo = true },
                new PlanModulo { PlanId = 204, ModuloId = 115, Activo = true },
                new PlanModulo { PlanId = 204, ModuloId = 116, Activo = true },
            },
        });
        db.Roles.AddRange(
            new Rol { Id = 501, Codigo = "ADMIN", Nombre = "Administrador", EsSistema = true, Activo = true, CreatedAt = DateTime.UtcNow },
            new Rol { Id = 502, Codigo = "OPERADOR", Nombre = "Operador", EsSistema = true, Activo = true, CreatedAt = DateTime.UtcNow },
            new Rol { Id = 503, Codigo = "CONTADOR", Nombre = "Contador", EsSistema = true, Activo = true, CreatedAt = DateTime.UtcNow },
            new Rol { Id = 504, Codigo = "READONLY", Nombre = "Solo lectura", EsSistema = true, Activo = true, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Deshabilitado_NoHaceNada()
    {
        var opts = DefaultOpts();
        opts.Enabled = false;
        var sp = BuildProvider(nameof(Deshabilitado_NoHaceNada), opts);
        await SeedPlanYRolAsync(sp);

        await EmpresaPruebaSeeder.SeedAsync(sp);

        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NeoStpDbContext>();
        (await db.Empresas.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Habilitado_CreaEmpresaCompleta()
    {
        var sp = BuildProvider(nameof(Habilitado_CreaEmpresaCompleta), DefaultOpts());
        await SeedPlanYRolAsync(sp);

        await EmpresaPruebaSeeder.SeedAsync(sp);

        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NeoStpDbContext>();

        var empresa = await db.Empresas.SingleAsync();
        empresa.Nit.Should().Be("06140000000000");
        empresa.RazonSocial.Should().Be("NeoSTP Pruebas, S.A. de C.V.");

        (await db.EmpresaPlanes.CountAsync(p => p.EmpresaId == empresa.Id)).Should().Be(1);
        (await db.EmpresaModulos.CountAsync(m => m.EmpresaId == empresa.Id)).Should().Be(2);
        (await db.Sucursales.CountAsync(s => s.EmpresaId == empresa.Id)).Should().Be(1);

        var admin = await db.Usuarios.SingleAsync(u => u.EmpresaId == empresa.Id);
        admin.Username.Should().Be("admin.prueba");
        admin.TipoUsuarioCodigo.Should().Be("ADMIN");
        admin.PasswordHash.Should().StartWith("HASHED::");

        (await db.UsuarioRoles.CountAsync(ur => ur.UsuarioId == admin.Id)).Should().Be(1);

        var config = await db.DteConfiguracion.SingleAsync(c => c.EmpresaId == empresa.Id);
        config.AmbienteCodigo.Should().Be("PRUEBAS");
        config.PasswordMhCifrado.Should().BeNull("los secretos se cargan vía UI, no por el seeder");
        config.CertificadoBlob.Should().BeNull();
    }

    [Fact]
    public async Task Idempotente_NoDuplicaEnSegundaCorrida()
    {
        var sp = BuildProvider(nameof(Idempotente_NoDuplicaEnSegundaCorrida), DefaultOpts());
        await SeedPlanYRolAsync(sp);

        await EmpresaPruebaSeeder.SeedAsync(sp);
        await EmpresaPruebaSeeder.SeedAsync(sp);   // segunda corrida

        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NeoStpDbContext>();
        (await db.Empresas.CountAsync()).Should().Be(1);
        (await db.Usuarios.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task PlanInexistente_NoCreaEmpresa()
    {
        var opts = DefaultOpts();
        opts.PlanCodigo = "NO_EXISTE";
        var sp = BuildProvider(nameof(PlanInexistente_NoCreaEmpresa), opts);
        await SeedPlanYRolAsync(sp);

        await EmpresaPruebaSeeder.SeedAsync(sp);

        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NeoStpDbContext>();
        (await db.Empresas.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task MobileDemoEnabled_CreaDatosMinimosParaApiMobile()
    {
        var opts = DefaultOpts();
        opts.MobileDemo.Enabled = true;
        var sp = BuildProvider(nameof(MobileDemoEnabled_CreaDatosMinimosParaApiMobile), opts);
        await SeedPlanYRolesMobileAsync(sp);

        await EmpresaPruebaSeeder.SeedAsync(sp);
        await EmpresaPruebaSeeder.SeedAsync(sp);

        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NeoStpDbContext>();
        var empresaId = await db.Empresas.Select(e => e.Id).SingleAsync();

        (await db.Usuarios.CountAsync(u => u.EmpresaId == empresaId && u.Username.StartsWith("mobile."))).Should().Be(6);
        (await db.EmpresaModulos.CountAsync(m => m.EmpresaId == empresaId)).Should().Be(13);
        (await db.Clientes.CountAsync(c => c.EmpresaId == empresaId)).Should().Be(3);
        (await db.Productos.CountAsync(p => p.EmpresaId == empresaId && p.CodigoInterno.StartsWith("MOB-"))).Should().Be(5);
        (await db.DteDocumentos.CountAsync(d => d.EmpresaId == empresaId && d.NumeroControl.Contains("MDEMO"))).Should().Be(2);
        (await db.DteDocumentos.CountAsync(d => d.EmpresaId == empresaId && d.NumeroControl.Contains("CDEMO"))).Should().Be(2);
        (await db.PagosCliente.CountAsync(p => p.EmpresaId == empresaId && p.Referencia == "MOBILE-DEMO-PARCIAL")).Should().Be(1);
        (await db.CuentasCobro.CountAsync(c => c.EmpresaId == empresaId && c.Nombre == "Demo Transferencia Mobile")).Should().Be(1);
        (await db.VentasPos.CountAsync(v => v.EmpresaId == empresaId && v.Numero == "POS-MOB-000001")).Should().Be(1);
        (await db.SesionesCaja.CountAsync(c => c.EmpresaId == empresaId && c.Numero.StartsWith("CAJA-MOB-"))).Should().Be(2);
        (await db.ScanDocumentos.CountAsync(s => s.EmpresaId == empresaId && s.ArchivoNombre == "demo-factura-proveedor.pdf")).Should().Be(1);
        var scan = await db.ScanDocumentos.SingleAsync(s => s.EmpresaId == empresaId && s.ArchivoNombre == "demo-factura-proveedor.pdf");
        scan.ArchivoBlob.Should().NotBeNullOrEmpty();
        scan.OcrProveedor.Should().Be("Mock");
        scan.OcrIntentos.Should().Be(1);
        (await db.Alertas.CountAsync(a => a.EmpresaId == empresaId && a.Clave.StartsWith("MOBILE_DEMO:"))).Should().Be(2);
        (await db.Alertas.CountAsync(a => a.EmpresaId == empresaId && a.EstadoCodigo == AlertaEstados.Pendiente)).Should().Be(1);
        (await db.Alertas.CountAsync(a => a.EmpresaId == empresaId && a.EstadoCodigo == AlertaEstados.Resuelta)).Should().Be(1);

        (await db.Proveedores.CountAsync(p => p.EmpresaId == empresaId && p.Codigo == "DEM-PROV-01")).Should().Be(1);
        (await db.OrdenesCompra.CountAsync(o => o.EmpresaId == empresaId && o.Numero == "OC-DEMO-0001" && o.EstadoCodigo == OrdenCompraEstados.Parcial)).Should().Be(1);
        (await db.OrdenCompraRecepciones.CountAsync(r => r.EmpresaId == empresaId && r.Numero == "RC-DEMO-0001")).Should().Be(1);
        (await db.OrdenCompraRecepcionLineas.CountAsync(r => r.EmpresaId == empresaId && r.MovimientoInventarioId != null)).Should().Be(1);
        (await db.FacturasCompra.CountAsync(f => f.EmpresaId == empresaId && f.NumeroDocumento == "COMP-DEMO-0001" && f.EstadoCodigo == FacturaCompraEstados.Parcial)).Should().Be(1);
        (await db.PagosProveedor.CountAsync(p => p.EmpresaId == empresaId && p.Referencia == "PAGO-COMP-DEMO-0001" && p.EstadoCodigo == PagoProveedorEstados.Confirmado)).Should().Be(1);
        (await db.ExistenciasProducto.CountAsync(e => e.EmpresaId == empresaId && e.StockMinimo == 8m)).Should().Be(1);
        (await db.MovimientosInventario.CountAsync(m => m.EmpresaId == empresaId && (m.Referencia == "INV-DEMO-ENTRADA" || m.Referencia == "INV-DEMO-SALIDA"))).Should().Be(2);
        (await db.MovimientosInventario.CountAsync(m => m.EmpresaId == empresaId && m.Origen == OrigenesMovimientoInventario.RecepcionCompra)).Should().Be(1);

        (await db.CuentasTesoreria.CountAsync(c => c.EmpresaId == empresaId && c.Codigo == "BAC-DEMO")).Should().Be(1);
        (await db.MovimientosTesoreria.CountAsync(m => m.EmpresaId == empresaId && (m.Referencia == "TES-DEMO-COBRO-01" || m.Referencia == "TES-DEMO-PAGO-01"))).Should().Be(2);
        (await db.MovimientosBancarios.CountAsync(m => m.EmpresaId == empresaId && m.EstadoCodigo == EstadosConciliacion.Conciliado)).Should().Be(1);
        (await db.MovimientosBancarios.CountAsync(m => m.EmpresaId == empresaId && m.EstadoCodigo == EstadosConciliacion.NoConciliado)).Should().Be(1);
        (await db.ConciliacionDetalles.CountAsync(d => d.EmpresaId == empresaId)).Should().Be(1);

        (await db.PortalAccesos.CountAsync(p => p.EmpresaId == empresaId && p.Tipo == PortalAccesoTipos.Documento)).Should().Be(1);
        (await db.PortalAccesos.CountAsync(p => p.EmpresaId == empresaId && p.Tipo == PortalAccesoTipos.EstadoCuenta)).Should().Be(1);

        (await db.EtapasPipelineCrm.CountAsync(e => e.EmpresaId == empresaId && e.Codigo == "DEMO_PROPUESTA")).Should().Be(1);
        (await db.ContactosCrm.CountAsync(c => c.EmpresaId == empresaId && c.Email == "compras.demo@cliente.local")).Should().Be(1);
        (await db.OportunidadesCrm.CountAsync(o => o.EmpresaId == empresaId && o.EstadoCodigo == OportunidadCrmEstados.Abierta)).Should().Be(1);
        (await db.CotizacionesCrm.CountAsync(c => c.EmpresaId == empresaId && c.Numero == "COT-DEMO-0001" && c.EstadoCodigo == CotizacionCrmEstados.Enviada)).Should().Be(1);
        (await db.CotizacionLineasCrm.CountAsync(l => l.EmpresaId == empresaId)).Should().Be(1);
        (await db.ActividadesCrm.CountAsync(a => a.EmpresaId == empresaId && a.EstadoCodigo == ActividadCrmEstados.Pendiente)).Should().Be(1);

        (await db.Empleados.CountAsync(e => e.EmpresaId == empresaId && e.Codigo == "EMP-DEMO-001")).Should().Be(1);
        (await db.ContratosLaborales.CountAsync(c => c.EmpresaId == empresaId && c.EstadoCodigo == ContratoEstados.Vigente)).Should().Be(1);
        (await db.PlanillaPeriodos.CountAsync(p => p.EmpresaId == empresaId && p.EstadoCodigo == PlanillaEstados.Calculada)).Should().Be(1);
        (await db.PlanillaDetalles.CountAsync()).Should().Be(1);

        (await db.ProfitCompras.CountAsync(c => c.EmpresaId == empresaId && c.NumeroDocumento == "COMP-DEMO-0001")).Should().Be(1);
        (await db.ProfitGastos.CountAsync(g => g.EmpresaId == empresaId && g.Descripcion == "Alquiler local demo")).Should().Be(1);
    }
}
