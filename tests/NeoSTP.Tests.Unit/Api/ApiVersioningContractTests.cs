using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using NeoSTP.Api.Controllers;
using Xunit;

namespace NeoSTP.Tests.Unit.Api;

/// <summary>
/// HB-6 guardrails: versioning policy, stable API/mobile routes and binary download metadata.
/// </summary>
public class ApiVersioningContractTests
{
    [Fact]
    public void Hb6_SoloNeoConnectExponeRutaVersionadaPublica()
    {
        var controllers = typeof(ApiControllerBase).Assembly
            .GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract)
            .Select(type => new
            {
                Type = type,
                Routes = type.GetCustomAttributes<RouteAttribute>().Select(attribute => attribute.Template).ToArray(),
            })
            .Where(controller => controller.Routes.Length > 0)
            .ToList();

        controllers.Should().ContainSingle(controller =>
            controller.Type == typeof(ConnectApiV1Controller) &&
            controller.Routes.Contains("api/v1"));

        controllers
            .Where(controller => controller.Type != typeof(ConnectApiV1Controller))
            .SelectMany(controller => controller.Routes.Select(route => $"{controller.Type.Name}:{route}"))
            .Should().NotContain(route => route.Contains(":api/v", StringComparison.OrdinalIgnoreCase),
                "las rutas internas/mobile se mantienen estables en /api/*; nuevas versiones publicas deben declararse deliberadamente");
    }

    [Fact]
    public void Hb6_RutasInternasMobileYDemo_MantienenContratoSinVersionExplicita()
    {
        var stableControllers = new[]
        {
            new StableRoute(typeof(AuthController), "api/auth"),
            new StableRoute(typeof(DashboardController), "api/dashboard"),
            new StableRoute(typeof(DteConfiguracionController), "api/dte/configuracion"),
            new StableRoute(typeof(DteController), "api/dte"),
            new StableRoute(typeof(ClientesController), "api/clientes"),
            new StableRoute(typeof(ProductosController), "api/productos"),
            new StableRoute(typeof(LookupsController), "api/lookups"),
            new StableRoute(typeof(CobranzaController), "api/cobros"),
            new StableRoute(typeof(ScanAiController), "api/scanai/documentos"),
            new StableRoute(typeof(AlertasController), "api/alertas"),
            new StableRoute(typeof(PosApiController), "api/pos"),
            new StableRoute(typeof(ComprasApiController), "api/compras"),
            new StableRoute(typeof(InventarioApiController), "api/inventario"),
            new StableRoute(typeof(TesoreriaApiController), "api/tesoreria"),
            new StableRoute(typeof(ReportesFiscalController), "api/reportes/fiscal"),
            new StableRoute(typeof(ContaApiController), "api/conta"),
            new StableRoute(typeof(ProfitController), "api/profit"),
            new StableRoute(typeof(CrmController), "api/crm"),
            new StableRoute(typeof(PortalApiController), "api/portal"),
            new StableRoute(typeof(RrhhApiController), "api/rrhh"),
        };

        foreach (var stable in stableControllers)
        {
            stable.Controller.GetCustomAttributes<RouteAttribute>()
                .Should().ContainSingle(attribute => attribute.Template == stable.Route);
            stable.Route.Should().StartWith("api/");
            stable.Route.Should().NotStartWith("api/v", "la politica HB-6 reserva /api/v1 para NeoConnect externo");
        }
    }

    [Fact]
    public void Hb6_DescargasBinarias_DeclaranContentTypeParaOpenApiYNoSonEnvelopeJson()
    {
        var binaryDownloads = new[]
        {
            new BinaryDownload(typeof(CatalogosController), nameof(CatalogosController.Export), ["text/csv", "application/json", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"]),
            new BinaryDownload(typeof(ConnectApiV1Controller), nameof(ConnectApiV1Controller.DescargarPdf), ["application/pdf"]),
            new BinaryDownload(typeof(ConnectApiV1Controller), nameof(ConnectApiV1Controller.DescargarJson), ["application/json"]),
            new BinaryDownload(typeof(ContaApiController), nameof(ContaApiController.BalanzaCsv), ["text/csv"]),
            new BinaryDownload(typeof(DteController), nameof(DteController.DescargarPdf), ["application/pdf"]),
            new BinaryDownload(typeof(DteController), nameof(DteController.DescargarJson), ["application/json"]),
            new BinaryDownload(typeof(DteEventosController), nameof(DteEventosController.Pdf), ["application/pdf"]),
            new BinaryDownload(typeof(PosApiController), nameof(PosApiController.Ticket), ["application/pdf"]),
            new BinaryDownload(typeof(ReportesFiscalController), nameof(ReportesFiscalController.LibroVentasConsumidorCsv), ["text/csv"]),
            new BinaryDownload(typeof(ReportesFiscalController), nameof(ReportesFiscalController.LibroVentasContribuyentesCsv), ["text/csv"]),
            new BinaryDownload(typeof(ReportesFiscalController), nameof(ReportesFiscalController.LibroComprasCsv), ["text/csv"]),
            new BinaryDownload(typeof(RrhhApiController), nameof(RrhhApiController.Recibo), ["application/pdf"]),
            new BinaryDownload(typeof(ScanAiController), nameof(ScanAiController.Archivo), ["application/pdf", "image/jpeg", "image/png", "application/octet-stream"]),
        };

        foreach (var download in binaryDownloads)
        {
            var method = download.Controller.GetMethod(download.Method)!;
            method.Should().NotBeNull($"{download.Controller.Name}.{download.Method} debe existir");

            var contentTypes = method.GetCustomAttributes<ProducesAttribute>()
                .SelectMany(attribute => attribute.ContentTypes)
                .ToArray();

            contentTypes.Should().Contain(download.ContentTypes,
                $"{download.Controller.Name}.{download.Method} debe publicar en OpenAPI el content-type binario real");
        }
    }

    [Fact]
    public void Hb6_DocumentacionVersionado_QuedaEnlazadaYDefinePoliticaDeCambios()
    {
        var root = FindRepoRoot();
        var contractDoc = File.ReadAllText(Path.Combine(root, "docs", "API-Contratos-Versionado.md"));
        var apiReadme = File.ReadAllText(Path.Combine(root, "src", "NeoSTP.Api", "README.md"));
        var mobileDoc = File.ReadAllText(Path.Combine(root, "docs", "NeoCloud-Mobile-API.md"));
        var connectDoc = File.ReadAllText(Path.Combine(root, "docs", "NeoConnect-API-v1.md"));

        contractDoc.Should().Contain("Tier A");
        contractDoc.Should().Contain("/api/*");
        contractDoc.Should().Contain("/api/v1/*");
        contractDoc.Should().Contain("ApiResponse<T>");
        contractDoc.Should().Contain("PagedResult<T>");
        contractDoc.Should().Contain("descargas binarias");
        contractDoc.Should().Contain("breaking change");

        apiReadme.Should().Contain("API-Contratos-Versionado.md");
        apiReadme.Should().Contain("Politica de versionado");
        mobileDoc.Should().Contain("HB-6");
        mobileDoc.Should().Contain("campos nuevos");
        connectDoc.Should().Contain("Versionado");
        connectDoc.Should().Contain("/api/v2");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "NeoSTP.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("No se encontro la raiz del repo.");
    }

    private sealed record StableRoute(Type Controller, string Route);

    private sealed record BinaryDownload(Type Controller, string Method, string[] ContentTypes);
}
