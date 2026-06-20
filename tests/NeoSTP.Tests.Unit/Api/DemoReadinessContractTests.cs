using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using NeoSTP.Api.Authorization;
using Xunit;

namespace NeoSTP.Tests.Unit.Api;

/// <summary>
/// Contract tests for demo-critical API and Web surfaces. These tests keep HB-3/HB-4 operational:
/// routes, auth metadata and Razor views must remain present before a demo build is accepted.
/// </summary>
public class DemoReadinessContractTests
{
    [Fact]
    public void Hb3_ApiDemoEndpoints_MantienenRutasPermisosYModulos()
    {
        var endpoints = new[]
        {
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.DashboardController), "api/dashboard", "GetEmpresa", "GET", "empresa"),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.DashboardController), "api/dashboard", "GetSuperAdmin", "GET", "superadmin"),

            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.DteController), "api/dte", "List", "GET", "documentos", "DTE.Consultar"),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.DteController), "api/dte", "GetById", "GET", "documentos/{id:int}", "DTE.Consultar"),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.DteController), "api/dte", "Emitir", "POST", "emitir", "DTE.Emitir"),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.DteController), "api/dte", "EmitirFactura", "POST", "emitir/factura", "DTE.Emitir"),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.DteController), "api/dte", "DescargarPdf", "GET", "documentos/{id:int}/pdf", "DTE.Consultar"),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.DteController), "api/dte", "DescargarJson", "GET", "documentos/{id:int}/json", "DTE.Consultar"),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.DteController), "api/dte", "Reenviar", "POST", "documentos/{id:int}/reenviar", "DTE.Reenviar"),

            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.CobranzaController), "api/cobros", "Resumen", "GET", "resumen", "Cobros.Ver"),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.CobranzaController), "api/cobros", "Pendientes", "GET", "pendientes", "Cobros.Ver"),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.CobranzaController), "api/cobros", "GenerarQr", "POST", "qr", "Cobros.Ver"),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.CobranzaController), "api/cobros", "RegistrarPago", "POST", "dte/{dteId:int}/pagos", "Cobros.Gestionar"),

            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.PosApiController), "api/pos", "ListVentas", "GET", "ventas", "Pos.Ver", "NEOPOS"),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.PosApiController), "api/pos", "CrearVenta", "POST", "ventas", "Pos.Vender", "NEOPOS"),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.PosApiController), "api/pos", "Ticket", "GET", "ventas/{id:int}/ticket", "Pos.Ver", "NEOPOS"),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.PosApiController), "api/pos", "PromoverVenta", "POST", "ventas/{id:int}/promover", "DTE.Emitir", "NEOPOS"),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.PosApiController), "api/pos", "EstadoCaja", "GET", "caja/estado", "Pos.Ver", "NEOPOS"),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.PosApiController), "api/pos", "AbrirCaja", "POST", "caja/abrir", "Pos.Vender", "NEOPOS"),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.PosApiController), "api/pos", "CerrarCaja", "POST", "caja/{id:int}/cerrar", "Pos.Vender", "NEOPOS"),

            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.ScanAiController), "api/scanai/documentos", "List", "GET", null, "ScanAI.Ver", "NEOSCANAI"),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.ScanAiController), "api/scanai/documentos", "Subir", "POST", null, "ScanAI.Ver", "NEOSCANAI"),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.ScanAiController), "api/scanai/documentos", "Archivo", "GET", "{id:int}/archivo", "ScanAI.Ver", "NEOSCANAI"),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.ScanAiController), "api/scanai/documentos", "Reprocesar", "POST", "{id:int}/reprocesar", "ScanAI.Ver", "NEOSCANAI"),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.ScanAiController), "api/scanai/documentos", "RegistrarDteRecibido", "POST", "{id:int}/registrar-dte-recibido", "ScanAI.Confirmar", "NEOSCANAI"),

            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.ComprasApiController), "api/compras", "Resumen", "GET", "resumen", "Compras.Ver", "COMPRAS"),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.ComprasApiController), "api/compras", "ListFacturas", "GET", "facturas", "Compras.Ver", "COMPRAS"),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.ComprasApiController), "api/compras", "CrearFactura", "POST", "facturas", "Compras.Gestionar", "COMPRAS"),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.ComprasApiController), "api/compras", "RegistrarPago", "POST", "pagos", "Compras.Gestionar", "COMPRAS"),

            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.InventarioApiController), "api/inventario", "Resumen", "GET", "resumen", "Inventario.Ver", "INVENTARIO"),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.InventarioApiController), "api/inventario", "ListExistencias", "GET", "existencias", "Inventario.Ver", "INVENTARIO"),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.InventarioApiController), "api/inventario", "Kardex", "GET", "kardex/{productoId:int}", "Inventario.Ver", "INVENTARIO"),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.InventarioApiController), "api/inventario", "Entrada", "POST", "entradas", "Inventario.Gestionar", "INVENTARIO"),

            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.TesoreriaApiController), "api/tesoreria", "Resumen", "GET", "resumen", "Tesoreria.Cuentas.Ver", "NEOTESORERIA"),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.TesoreriaApiController), "api/tesoreria", "ListCuentas", "GET", "cuentas", "Tesoreria.Cuentas.Ver", "NEOTESORERIA"),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.TesoreriaApiController), "api/tesoreria", "ListMovimientos", "GET", "movimientos", "Tesoreria.Movimientos.Ver", "NEOTESORERIA"),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.TesoreriaApiController), "api/tesoreria", "ImportarEstadoCuenta", "POST", "conciliacion/{cuentaId:int}/importar", "Tesoreria.Movimientos.Gestionar", "NEOTESORERIA"),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.TesoreriaApiController), "api/tesoreria", "Sugerencias", "GET", "conciliacion/{cuentaId:int}/sugerencias", "Tesoreria.Movimientos.Ver", "NEOTESORERIA"),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.TesoreriaApiController), "api/tesoreria", "ConciliarCombinacion", "POST", "conciliacion/movimientos/{id:int}/conciliar-combinacion", "Tesoreria.Movimientos.Gestionar", "NEOTESORERIA"),

            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.ReportesFiscalController), "api/reportes/fiscal", "ResumenF07", "GET", "f07", "Reportes.Ver", "NEOBI"),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.ReportesFiscalController), "api/reportes/fiscal", "LibroCompras", "GET", "libro-compras", "Reportes.Ver", "NEOBI"),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.ReportesFiscalController), "api/reportes/fiscal", "LibroVentasConsumidor", "GET", "libro-ventas-consumidor", "Reportes.Ver", "NEOBI"),

            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.ContaApiController), "api/conta", "Balanza", "GET", "balanza", "Conta.Ver", "NEOCONTA"),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.ContaApiController), "api/conta", "GenerarAsientos", "POST", "asientos/generar", "Conta.Gestionar", "NEOCONTA"),

            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.ProfitController), "api/profit", "Dashboard", "GET", "dashboard", "Profit.Ver", "NEOPROFIT"),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.ProfitController), "api/profit", "ListGastos", "GET", "gastos", "Profit.Ver", "NEOPROFIT"),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.ProfitController), "api/profit", "CrearGasto", "POST", "gastos", "Profit.Gestionar", "NEOPROFIT"),

            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.CrmController), "api/crm", "Resumen", "GET", "resumen", "Crm.Oportunidades.Ver", "NEOCRM"),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.CrmController), "api/crm", "ListOportunidades", "GET", "oportunidades", "Crm.Oportunidades.Ver", "NEOCRM"),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.CrmController), "api/crm", "ConvertirCotizacion", "POST", "cotizaciones/{id:int}/convertir-dte", "DTE.Emitir", "NEOCRM"),

            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.PortalApiController), "api/portal", "ListEnlaces", "GET", "enlaces", "Portal.Enlaces.Ver", "NEOPORTAL"),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.PortalApiController), "api/portal", "GenerarEnlaceDocumento", "POST", "enlaces/documento/{dteDocumentoId:int}", "Portal.Enlaces.Gestionar", "NEOPORTAL"),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.PortalApiController), "api/portal", "Revocar", "POST", "enlaces/{id:int}/revocar", "Portal.Enlaces.Gestionar", "NEOPORTAL"),
        };

        foreach (var endpoint in endpoints)
        {
            AssertApiEndpoint(endpoint);
        }
    }

    [Fact]
    public void Hb3_NeoConnectApiV1_MantieneRutasPublicasParaIntegradores()
    {
        var endpoints = new[]
        {
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.ConnectApiV1Controller), "api/v1", "Ping", "GET", "ping", AllowAnonymousController: true),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.ConnectApiV1Controller), "api/v1", "EmitirDte", "POST", "dte", AllowAnonymousController: true),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.ConnectApiV1Controller), "api/v1", "ListarDte", "GET", "dte", AllowAnonymousController: true),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.ConnectApiV1Controller), "api/v1", "ObtenerDte", "GET", "dte/{id:int}", AllowAnonymousController: true),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.ConnectApiV1Controller), "api/v1", "DescargarPdf", "GET", "dte/{id:int}/pdf", AllowAnonymousController: true),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.ConnectApiV1Controller), "api/v1", "ListarClientes", "GET", "clientes", AllowAnonymousController: true),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.ConnectApiV1Controller), "api/v1", "CrearCliente", "POST", "clientes", AllowAnonymousController: true),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.ConnectApiV1Controller), "api/v1", "ListarProductos", "GET", "productos", AllowAnonymousController: true),
            new ApiEndpoint(typeof(NeoSTP.Api.Controllers.ConnectApiV1Controller), "api/v1", "CrearProducto", "POST", "productos", AllowAnonymousController: true),
        };

        foreach (var endpoint in endpoints)
        {
            AssertApiEndpoint(endpoint);
        }
    }

    [Fact]
    public void Hb4_WebDemoRoutes_MantienenAccionGetYVistaRazor()
    {
        var routes = new[]
        {
            new WebRoute(typeof(NeoSTP.Web.Controllers.HomeController), "Index", "Home/Index.cshtml", RequiresExplicitHttpGet: false),
            new WebRoute(typeof(NeoSTP.Web.Controllers.DteDocumentosController), "Index", "DteDocumentos/Index.cshtml"),
            new WebRoute(typeof(NeoSTP.Web.Controllers.DteDocumentosController), "Create", "DteDocumentos/Create.cshtml"),
            new WebRoute(typeof(NeoSTP.Web.Controllers.DteDocumentosController), "Details", "DteDocumentos/Details.cshtml"),
            new WebRoute(typeof(NeoSTP.Web.Controllers.DteDocumentosController), "CrearRetencion", "DteDocumentos/CrearRetencion.cshtml"),
            new WebRoute(typeof(NeoSTP.Web.Controllers.PosController), "Index", "Pos/Index.cshtml"),
            new WebRoute(typeof(NeoSTP.Web.Controllers.PosController), "Nueva", "Pos/Nueva.cshtml"),
            new WebRoute(typeof(NeoSTP.Web.Controllers.PosController), "Detalle", "Pos/Detalle.cshtml"),
            new WebRoute(typeof(NeoSTP.Web.Controllers.PosController), "Ticket", "Pos/Ticket.cshtml"),
            new WebRoute(typeof(NeoSTP.Web.Controllers.CajaController), "Index", "Caja/Index.cshtml"),
            new WebRoute(typeof(NeoSTP.Web.Controllers.CobrosController), "Index", "Cobros/Index.cshtml", GetTemplate: ""),
            new WebRoute(typeof(NeoSTP.Web.Controllers.CobrosController), "Recordatorios", "Cobros/Recordatorios.cshtml", GetTemplate: "recordatorios"),
            new WebRoute(typeof(NeoSTP.Web.Controllers.CobrosController), "Cuentas", "Cobros/Cuentas.cshtml", GetTemplate: "Cuentas"),
            new WebRoute(typeof(NeoSTP.Web.Controllers.CobrosController), "Cliente", "Cobros/Cliente.cshtml", GetTemplate: "Cliente/{clienteId:int}"),
            new WebRoute(typeof(NeoSTP.Web.Controllers.ComprasController), "Index", "Compras/Index.cshtml"),
            new WebRoute(typeof(NeoSTP.Web.Controllers.ComprasController), "Crear", "Compras/Crear.cshtml"),
            new WebRoute(typeof(NeoSTP.Web.Controllers.ComprasController), "Detalle", "Compras/Detalle.cshtml"),
            new WebRoute(typeof(NeoSTP.Web.Controllers.ComprasController), "Ordenes", "Compras/Ordenes.cshtml"),
            new WebRoute(typeof(NeoSTP.Web.Controllers.ComprasController), "CrearOrden", "Compras/CrearOrden.cshtml"),
            new WebRoute(typeof(NeoSTP.Web.Controllers.ComprasController), "EditarOrden", "Compras/CrearOrden.cshtml"),
            new WebRoute(typeof(NeoSTP.Web.Controllers.ComprasController), "DetalleOrden", "Compras/DetalleOrden.cshtml"),
            new WebRoute(typeof(NeoSTP.Web.Controllers.InventarioController), "Index", "Inventario/Index.cshtml"),
            new WebRoute(typeof(NeoSTP.Web.Controllers.InventarioController), "Kardex", "Inventario/Kardex.cshtml"),
            new WebRoute(typeof(NeoSTP.Web.Controllers.ProfitController), "Index", "Profit/Index.cshtml"),
            new WebRoute(typeof(NeoSTP.Web.Controllers.ScanController), "Index", "Scan/Index.cshtml", GetTemplate: ""),
            new WebRoute(typeof(NeoSTP.Web.Controllers.ScanController), "Detalle", "Scan/Detalle.cshtml", GetTemplate: "{id:int}"),
            new WebRoute(typeof(NeoSTP.Web.Controllers.TesoreriaController), "Index", "Tesoreria/Index.cshtml"),
            new WebRoute(typeof(NeoSTP.Web.Controllers.TesoreriaController), "Movimientos", "Tesoreria/Movimientos.cshtml"),
            new WebRoute(typeof(NeoSTP.Web.Controllers.TesoreriaController), "Conciliacion", "Tesoreria/Conciliacion.cshtml"),
            new WebRoute(typeof(NeoSTP.Web.Controllers.TesoreriaController), "RegistrarMovimiento", "Tesoreria/RegistrarMovimiento.cshtml"),
            new WebRoute(typeof(NeoSTP.Web.Controllers.IntegracionesController), "Index", "Integraciones/Index.cshtml"),
            new WebRoute(typeof(NeoSTP.Web.Controllers.SoporteController), "Index", "Soporte/Index.cshtml"),
            new WebRoute(typeof(NeoSTP.Web.Controllers.SoporteController), "Operacion", "Soporte/Operacion.cshtml"),
        };

        foreach (var route in routes)
        {
            AssertWebRoute(route);
        }
    }

    [Fact]
    public void Hb4_PortalPublico_MantieneRutasAnonimasYVistasDeCliente()
    {
        typeof(NeoSTP.Web.Controllers.PortalController)
            .GetCustomAttributes<AllowAnonymousAttribute>()
            .Should().NotBeEmpty("el portal publico debe seguir disponible sin sesion interna");

        AssertControllerRoute(typeof(NeoSTP.Web.Controllers.PortalController), "portal");

        AssertWebRoute(new WebRoute(typeof(NeoSTP.Web.Controllers.PortalController), "Index", "Portal/Documento.cshtml", AllowAnonymousController: true, GetTemplate: "{token}"));
        AssertWebRoute(new WebRoute(typeof(NeoSTP.Web.Controllers.PortalController), "Pdf", "Portal/Qr.cshtml", AllowAnonymousController: true, GetTemplate: "{token}/pdf", RequiresView: false));
        AssertWebRoute(new WebRoute(typeof(NeoSTP.Web.Controllers.PortalController), "Json", "Portal/Qr.cshtml", AllowAnonymousController: true, GetTemplate: "{token}/json", RequiresView: false));
        AssertWebRoute(new WebRoute(typeof(NeoSTP.Web.Controllers.PortalController), "Qr", "Portal/Qr.cshtml", AllowAnonymousController: true, GetTemplate: "{token}/qr"));

        AssertViewExists("Portal/EstadoCuenta.cshtml");
        AssertViewExists("Portal/NoDisponible.cshtml");
    }

    private static void AssertApiEndpoint(ApiEndpoint endpoint)
    {
        AssertControllerRoute(endpoint.Controller, endpoint.ControllerRoute);
        AssertControllerAuth(endpoint.Controller, endpoint.AllowAnonymousController);

        if (endpoint.Module is not null)
        {
            endpoint.Controller
                .GetCustomAttributes<RequireModuleAttribute>()
                .Should().ContainSingle(attribute => attribute.Codigo == endpoint.Module);
        }

        var method = FindHttpAction(endpoint.Controller, endpoint.Action, endpoint.Verb, endpoint.Template);

        if (endpoint.Permiso is not null)
        {
            method.GetCustomAttributes<RequirePermisoAttribute>()
                .Should().ContainSingle(attribute => attribute.Codigo == endpoint.Permiso);
        }
    }

    private static void AssertWebRoute(WebRoute route)
    {
        AssertControllerAuth(route.Controller, route.AllowAnonymousController);

        var method = route.RequiresExplicitHttpGet
            ? FindHttpAction(route.Controller, route.Action, "GET", route.GetTemplate)
            : FindActionByName(route.Controller, route.Action);

        method.Should().NotBeNull();

        if (route.RequiresView)
        {
            AssertViewExists(route.ViewPath);
        }
    }

    private static void AssertControllerAuth(Type controller, bool allowAnonymous)
    {
        if (allowAnonymous)
        {
            controller.GetCustomAttributes<AllowAnonymousAttribute>()
                .Should().NotBeEmpty($"{controller.Name} debe permitir acceso anonimo de contrato externo");
            return;
        }

        controller.GetCustomAttributes<AuthorizeAttribute>()
            .Should().NotBeEmpty($"{controller.Name} debe protegerse por autenticacion");
    }

    private static void AssertControllerRoute(Type controller, string expectedRoute)
    {
        controller.GetCustomAttributes<RouteAttribute>()
            .Should().ContainSingle(attribute => attribute.Template == expectedRoute);
    }

    private static MethodInfo FindHttpAction(Type controller, string action, string verb, string? template)
    {
        var matches = controller
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.Name == action)
            .Where(method => method.GetCustomAttributes<HttpMethodAttribute>()
                .Any(attribute => attribute.HttpMethods.Contains(verb) && attribute.Template == template))
            .ToList();

        matches.Should().ContainSingle($"{controller.Name}.{action} debe exponer {verb} {template ?? "(sin template)"}");
        return matches[0];
    }

    private static MethodInfo FindActionByName(Type controller, string action)
    {
        var matches = controller
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.Name == action)
            .ToList();

        matches.Should().ContainSingle($"{controller.Name}.{action} debe mantenerse como accion unica");
        return matches[0];
    }

    private static void AssertViewExists(string relativeViewPath)
    {
        var viewPath = Path.Combine(FindRepoRoot(), "src", "NeoSTP.Web", "Views", relativeViewPath.Replace('/', Path.DirectorySeparatorChar));

        File.Exists(viewPath).Should().BeTrue($"la vista Razor {relativeViewPath} debe existir para demos HB-4");
        new FileInfo(viewPath).Length.Should().BeGreaterThan(100, $"la vista Razor {relativeViewPath} no debe quedar vacia");
    }

    private static string FindRepoRoot()
    {
        var current = AppContext.BaseDirectory;

        while (!string.IsNullOrWhiteSpace(current))
        {
            if (Directory.Exists(Path.Combine(current, "src", "NeoSTP.Web")))
            {
                return current;
            }

            var parent = Directory.GetParent(current);
            current = parent?.FullName ?? string.Empty;
        }

        throw new DirectoryNotFoundException("No se encontro la raiz del repositorio NeoSTP.");
    }

    private sealed record ApiEndpoint(
        Type Controller,
        string ControllerRoute,
        string Action,
        string Verb,
        string? Template,
        string? Permiso = null,
        string? Module = null,
        bool AllowAnonymousController = false);

    private sealed record WebRoute(
        Type Controller,
        string Action,
        string ViewPath,
        bool AllowAnonymousController = false,
        string? GetTemplate = null,
        bool RequiresExplicitHttpGet = true,
        bool RequiresView = true);
}
