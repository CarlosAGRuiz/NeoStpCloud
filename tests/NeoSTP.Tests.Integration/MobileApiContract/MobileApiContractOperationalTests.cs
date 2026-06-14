using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Cobranza;
using NeoSTP.Application.Cobranza.Dtos;
using NeoSTP.Application.Common;
using NeoSTP.Application.Comunicaciones;
using NeoSTP.Application.Connect;
using NeoSTP.Application.Dashboard;
using NeoSTP.Application.Inventario;
using NeoSTP.Application.Notificaciones;
using NeoSTP.Application.Notificaciones.Dtos;
using NeoSTP.Application.Pos;
using NeoSTP.Application.Productos;
using NeoSTP.Application.Profit;
using NeoSTP.Application.Provisioning;
using NeoSTP.Application.Scan;
using NeoSTP.Application.Scan.Dtos;
using NeoSTP.Domain.Core.Licenciamiento;
using NeoSTP.Domain.Core.Notificaciones;
using NeoSTP.Domain.Core.Seguridad;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Persistence.Seed;
using NeoSTP.Infrastructure.Scan;
using NeoSTP.Infrastructure.Services;
using NSubstitute;

namespace NeoSTP.Tests.Integration.MobileApiContract;

public class MobileApiContractOperationalTests
{
    private static readonly (string Id, string Method, string Path, string Group)[] MobileEndpoints =
    [
        ("MAPI-01", "GET", "/health", "Health"),
        ("MAPI-02", "POST", "/api/auth/login", "Auth"),
        ("MAPI-03", "GET", "/api/auth/me", "Auth"),
        ("MAPI-04", "GET", "/api/dashboard/empresa", "Dashboard"),
        ("MAPI-05", "GET", "/api/clientes", "Clientes"),
        ("MAPI-06", "GET", "/api/productos", "Productos"),
        ("MAPI-07", "GET", "/api/dte/documentos", "DTE"),
        ("MAPI-08", "GET", "/api/dte/documentos/{id}", "DTE"),
        ("MAPI-09", "GET", "/api/dte/documentos/{id}/pdf", "DTE"),
        ("MAPI-10", "GET", "/api/dte/documentos/{id}/json", "DTE"),
        ("MAPI-11", "GET", "/api/cobros/resumen", "Cobros"),
        ("MAPI-12", "GET", "/api/cobros/pendientes", "Cobros"),
        ("MAPI-13", "POST", "/api/cobros/qr", "Cobros"),
        ("MAPI-14", "GET", "/api/pos/caja/estado", "POS"),
        ("MAPI-15", "GET", "/api/pos/ventas/{id}/ticket", "POS"),
        ("MAPI-16", "GET", "/api/scanai/documentos", "NeoScan"),
        ("MAPI-17", "GET", "/api/scanai/documentos/{id}/archivo", "NeoScan"),
        ("MAPI-18", "POST", "/api/scanai/documentos", "NeoScan"),
        ("MAPI-19", "POST", "/api/scanai/documentos/{id}/reprocesar", "NeoScan"),
        ("MAPI-20", "GET", "/api/alertas/resumen", "Alertas"),
        ("MAPI-21", "POST", "/api/alertas/dispositivos", "Alertas"),
        ("MAPI-22", "ANY", "endpoint protegido sin token", "Negativos"),
        ("MAPI-23", "ANY", "accion sin permiso/modulo", "Negativos"),
    ];

    [Fact]
    public void MobileEndpointManifest_CubreLosGruposMAPI_ParaReporteDemo()
    {
        MobileEndpoints.Should().HaveCount(23);
        MobileEndpoints.Select(e => e.Id).Should().OnlyHaveUniqueItems();
        MobileEndpoints.Select(e => e.Group).Should().Contain(["Auth", "DTE", "Cobros", "POS", "NeoScan", "Alertas", "Negativos"]);
    }

    [Fact]
    public async Task MobileDemoFixture_AlimentaFlujosOperativosDeLaApp()
    {
        await using var sp = BuildProvider(nameof(MobileDemoFixture_AlimentaFlujosOperativosDeLaApp));
        await SeedPlanYRolesMobileAsync(sp);
        await EmpresaPruebaSeeder.SeedAsync(sp);
        await EmpresaPruebaSeeder.SeedAsync(sp);

        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NeoStpDbContext>();
        var empresaId = await db.Empresas.Select(e => e.Id).SingleAsync();
        var adminUserId = await db.Usuarios.Where(u => u.Username == "mobile.admin").Select(u => u.Id).SingleAsync();
        var audit = Substitute.For<IAuditoriaService>();

        var dashboard = await new DashboardService(db).GetDashboardEmpresaAsync(empresaId);
        dashboard.Procesados.Should().BeGreaterThanOrEqualTo(2);
        dashboard.PorTipo.Should().NotBeEmpty();

        var clientes = await new ClientesService(db, audit).GetListAsync(empresaId, new PagedQuery { Page = 1, PageSize = 20 });
        clientes.Value!.Items.Should().HaveCountGreaterThanOrEqualTo(3);
        AssertCamelCase(clientes.Value);

        var productos = await new ProductosService(db, audit).GetListAsync(empresaId, new PagedQuery { Page = 1, PageSize = 20 });
        productos.Value!.Items.Should().HaveCountGreaterThanOrEqualTo(5);

        var cobranza = new CobranzaService(db, audit);
        var pendientes = await cobranza.GetPendientesAsync(empresaId, new CobranzaQuery { Page = 1, PageSize = 20 });
        pendientes.Value!.Items.Should().ContainSingle(p => p.NumeroControl.Contains("MDEMO") && p.Saldo > 0);
        var qr = await new CobroQrService(db, audit).GenerarQrAsync(empresaId, new GenerarQrCobroRequest { DteDocumentoId = pendientes.Value.Items[0].DteDocumentoId });
        qr.Value!.QrPngBase64.Should().NotBeNullOrWhiteSpace();

        var pos = NewPos(db, audit);
        var ventas = await pos.ListAsync(empresaId, null, null, new PagedQuery { Page = 1, PageSize = 20 });
        ventas.Value!.Items.Should().ContainSingle(v => v.Numero == "POS-MOB-000001");
        var ticket = await pos.GetTicketAsync(empresaId, ventas.Value.Items[0].Id);
        ticket.Value!.Lineas.Should().NotBeEmpty();
        var caja = await new PosCajaService(db, audit).GetEstadoAsync(empresaId);
        caja.Value.Should().NotBeNull();

        var scan = new ScanService(db, new MockScanExtractionService(), Substitute.For<IProfitService>(), audit);
        var scans = await scan.ListAsync(empresaId, new ScanQuery { Page = 1, PageSize = 20 });
        scans.Value!.Items.Should().ContainSingle(s => s.ArchivoNombre == "demo-factura-proveedor.pdf" && s.TieneArchivo);
        var archivo = await scan.GetArchivoAsync(empresaId, scans.Value.Items[0].Id);
        archivo!.Contenido.Should().NotBeEmpty();

        var alertas = new AlertaService(db, Substitute.For<IPushSender>(), NullLogger<AlertaService>.Instance);
        var resumenAlertas = await alertas.ResumenAsync(empresaId, adminUserId);
        resumenAlertas.Pendientes.Should().Be(1);
        var historico = await alertas.ListarAsync(empresaId, adminUserId, new AlertaQuery { EstadoCodigo = AlertaEstados.Resuelta });
        historico.Value!.Items.Should().ContainSingle(a => a.EstadoCodigo == AlertaEstados.Resuelta);
    }

    private static PosService NewPos(NeoStpDbContext db, IAuditoriaService audit)
        => new(
            db,
            audit,
            Substitute.For<ITicketPdfService>(),
            Substitute.For<ITenantEmailSender>(),
            Substitute.For<IConnectDteService>(),
            Substitute.For<IInventarioService>(),
            Options.Create(new PosOptions()));

    private static void AssertCamelCase<T>(PagedResult<T> result)
    {
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        json.Should().Contain("page");
        json.Should().Contain("pageSize");
        json.Should().Contain("total");
        json.Should().NotContain("PageSize");
    }

    private static ServiceProvider BuildProvider(string dbName)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<NeoStpDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddSingleton(Options.Create(DefaultOpts()));

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
        MobileDemo = new() { Enabled = true, Password = "MobileDemo!2026" },
    };

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
            new Modulo { Id = 105, Codigo = "NEOPORTAL", Nombre = "NeoPortal", Activo = true, CreatedAt = DateTime.UtcNow });
        db.Planes.Add(new Plan
        {
            Id = 204,
            Codigo = "ENTERPRISE",
            Nombre = "Enterprise",
            PrecioMensual = 400m,
            Activo = true,
            CreatedAt = DateTime.UtcNow,
            Modulos =
            {
                new PlanModulo { PlanId = 204, ModuloId = 100, Activo = true },
                new PlanModulo { PlanId = 204, ModuloId = 101, Activo = true },
                new PlanModulo { PlanId = 204, ModuloId = 102, Activo = true },
                new PlanModulo { PlanId = 204, ModuloId = 103, Activo = true },
                new PlanModulo { PlanId = 204, ModuloId = 104, Activo = true },
                new PlanModulo { PlanId = 204, ModuloId = 105, Activo = true },
            },
        });
        db.Roles.AddRange(
            new Rol { Id = 501, Codigo = "ADMIN", Nombre = "Administrador", EsSistema = true, Activo = true, CreatedAt = DateTime.UtcNow },
            new Rol { Id = 502, Codigo = "OPERADOR", Nombre = "Operador", EsSistema = true, Activo = true, CreatedAt = DateTime.UtcNow },
            new Rol { Id = 503, Codigo = "CONTADOR", Nombre = "Contador", EsSistema = true, Activo = true, CreatedAt = DateTime.UtcNow },
            new Rol { Id = 504, Codigo = "READONLY", Nombre = "Solo lectura", EsSistema = true, Activo = true, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }
}
