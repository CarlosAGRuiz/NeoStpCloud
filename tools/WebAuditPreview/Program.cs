using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NeoSTP.Application.Catalogos.Dtos;
using NeoSTP.Application.Dte.Diagnostico;
using NeoSTP.Application.Dte.Dtos;
using NeoSTP.Application.Dte.Eventos.Dtos;
using NeoSTP.Web.Controllers;
using NeoSTP.Web.Models;

// Offline rendering only. No AddInfrastructure, SQL provider, actual Web startup, Start/Run or network.
var webhookPreview = args.Length == 2 && args[1] == "--wompi-webhook";
var checkoutPreview = webhookPreview || (args.Length == 2 && args[1] == "--checkout");
var workspace = Path.GetFullPath(checkoutPreview ? args[0] : args.Single());
var output = webhookPreview ? Path.Combine(workspace, "tmp", "gl1hb", "web") : checkoutPreview ? Path.Combine(workspace, "tmp", "gl1h", "web") : Path.Combine(workspace, "tmp", "multiagent-qa", "web");
Directory.CreateDirectory(output);
var webRoot = Path.Combine(workspace, "src", "NeoSTP.Web");
var builder = WebApplication.CreateBuilder(new WebApplicationOptions {
    ContentRootPath = webRoot, WebRootPath = Path.Combine(webRoot, "wwwroot"),
    ApplicationName = typeof(PreviewRouter).Assembly.GetName().Name, EnvironmentName = "Development"
});
builder.Configuration.Sources.Clear();
builder.Logging.ClearProviders();
builder.Services.AddControllersWithViews().AddApplicationPart(typeof(AccountController).Assembly);
builder.Services.AddHttpContextAccessor();
builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
await using var app = builder.Build();
using var scope = app.Services.CreateScope();
var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
http.Request.Scheme = "http";
http.Request.Host = new HostString("web-audit.test");
scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = http;
var engine = scope.ServiceProvider.GetRequiredService<IRazorViewEngine>();
if (checkoutPreview)
{
    var correlation = Guid.Parse("77777777-7777-4777-8777-777777777777");
    foreach (var status in new[] { "PROCESSING", "AWAITING_PAYMENT", "REQUIRES_RECONCILIATION", "PAYMENT_VERIFIED_SANDBOX", "COMPLETED" })
        await Render("Billing", "CheckoutStatus", new NeoSTP.Application.Billing.Dtos.BillingCheckoutDto(
            correlation, 999, 77, "Wompi", 25.75m, "USD", status, "77",
            status == "AWAITING_PAYMENT" ? "https://pagos.wompi.sv/fixture-no-real" : null, null, null),
            "checkout-" + status.ToLowerInvariant());
    await Render("Billing", "Checkout", new[] { new NeoSTP.Application.Licenciamiento.Dtos.PlanDto
    {
        Id = 77, Codigo = "QA", Nombre = "Plan sintético", PrecioMensual = 25.75m,
        MonedaCodigo = "USD", Activo = true,
    } }, "checkout-form", new Dictionary<string, object> { ["Metodos"] = new List<string> { "Wompi", "Transferencia" } });
    Console.WriteLine($"Rendered isolated checkout Razor fixtures at {output}");
    return;
}
await Render("Account", "Login", new LoginViewModel(), "login", layout: false);

foreach (var state in new[] { "PROCESADO", "ERROR", "INVALIDADO", "ENVIADO", "CONTINGENCIA" }) {
    var model = Document(state);
    await Render("DteDocumentos", "Details", model, "dte-" + state.ToLowerInvariant());
}
foreach (var code in new[] { "008", "096" }) {
    var model = Document("ERROR");
    model.RespuestaHacienda = code == "008"
        ? "{\"estado\":\"RECHAZADO\",\"codigoMsg\":\"008\",\"descripcionMsg\":\"[emisor.codActividad] NO CORRESPONDE A CONTRIBUYENTE\"}"
        : "{\"estado\":\"RECHAZADO\",\"codigoMsg\":\"096\",\"descripcionMsg\":\"DOCUMENTO NO CUMPLE CON NORMATIVA DE CUMPLIMIENTO\",\"observaciones\":[\"Campo #/emisor/direccion/municipio no cumple el formato requerido\",\"Campo #/emisor/direccion/departamento no cumple el formato requerido\"]}";
    model.EnviadoAt = new DateTime(2026, 9, 4);
    await Render("DteDocumentos", "Details", model, "dte-error-" + code);
}
await Render("DteDocumentos", "Details", Document("CONTINGENCIA"), "dte-readonly", readonlyUser: true);
var catalogs = new Dictionary<string, object> {
    ["Paises"] = new[] { Item("9300", "El Salvador"), Item("9599", "País extranjero QA") },
    ["TiposDoc"] = new[] { Item("DUI", "DUI"), Item("NIT", "NIT") },
    ["TiposContrib"] = new[] { Item("CONSUMIDOR_FINAL", "Consumidor final") },
    ["Departamentos"] = new[] { Item("06", "Departamento QA 06"), Item("05", "Departamento QA 05") },
    ["Municipios"] = new[] { Item("01", "Municipio QA 06-01", "06"), Item("02", "Municipio QA 06-02", "06"), Item("03", "Municipio QA 05-03", "05") }
};
await Render("Clientes", "Edit", new EditClienteViewModel {
    Id = 777, Nombre = "Cliente sintético QA", PaisCodigo = "9300", DepartamentoCodigo = "06",
    MunicipioCodigo = "01", NumeroDocumento = "00000000-0", Correo = "fixture@example.test"
}, "cliente", catalogs);
var eligible = Enumerable.Range(1, 200).Select(i => new DteDocumentoListItemDto {
    Id = i, TipoDteCodigo = i % 3 == 0 ? "14" : i % 3 == 1 ? "01" : "11",
    NumeroControl = $"DTE-QA-{i:0000}", CodigoGeneracion = $"GEN-QA-{i:0000}",
    FechaEmision = new DateTime(2026, 9, 4), ReceptorNombre = $"Cliente sintético {i}",
    TotalPagar = i, EstadoCodigo = "PROCESADO", AmbienteCodigo = "PRUEBAS"
}).ToList();
await Render("DteEventos", "CreateRetorno", new CrearEventoRetornoRequest(), "retorno",
    new Dictionary<string, object> { ["Dtes"] = eligible });
Console.WriteLine($"Rendered isolated Razor fixtures at {output}");

async Task Render(string controller, string view, object model, string filename,
    Dictionary<string, object>? values = null, bool layout = true, bool readonlyUser = false) {
    if (model is DteDocumentoDto dte) {
        dte.Diagnostico = DteDiagnosticoGuia.Crear(dte.EstadoCodigo, dte.SelloRecibido, dte.EnviadoAt, dte.RespuestaHacienda);
        await File.WriteAllTextAsync(Path.Combine(output, filename + ".model.json"), System.Text.Json.JsonSerializer.Serialize(dte, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }
    http.Request.Path = $"/{controller}/{view}";
    http.User = new ClaimsPrincipal(new ClaimsIdentity(readonlyUser
        ? new[] { new Claim("permiso", "DTE.Consultar") }
        : new[] { new Claim("permiso", "DTE.Emitir"), new Claim("permiso", "DTE.Diagnostico") }, "fixture"));
    var routes = new RouteData();
    routes.Values["controller"] = controller; routes.Values["action"] = view;
    routes.Routers.Add(new PreviewRouter());
    var action = new ActionContext(http, routes, new ActionDescriptor());
    var found = engine.GetView(null, $"/Views/{controller}/{view}.cshtml", false);
    if (!found.Success) throw new InvalidOperationException(string.Join(", ", found.SearchedLocations));
    if (layout) ((RazorView)found.View).RazorPage.Layout = "/Views/Shared/_AuditLayout.cshtml";
    using var writer = new StringWriter();
    var data = new ViewDataDictionary(scope.ServiceProvider.GetRequiredService<IModelMetadataProvider>(), new ModelStateDictionary()) { Model = model };
    if (values != null) foreach (var pair in values) data[pair.Key] = pair.Value;
    var temp = new TempDataDictionary(http, scope.ServiceProvider.GetRequiredService<ITempDataProvider>());
    await found.View.RenderAsync(new ViewContext(action, found.View, data, temp, writer, new HtmlHelperOptions()));
    await File.WriteAllTextAsync(Path.Combine(output, filename + ".html"), writer.ToString());
}
static CatalogoItemDto Item(string code, string name, string? parent = null) => new() { Codigo = code, Valor = name, ParentCodigo = parent, Activo = true };
static DteDocumentoDto Document(string state) => new() {
    Id = 777, EmpresaId = 999, TipoDteCodigo = "01", VersionDte = 1, AmbienteCodigo = "PRUEBAS",
    NumeroControl = "DTE-01-M001P001-000000000000777", CodigoGeneracion = "00000000-0000-0000-0000-000000000777",
    EstadoCodigo = state, ReceptorNombre = "Cliente sintético QA", ReceptorCorreo = "fixture@example.test",
    FechaEmision = new DateTime(2026, 9, 4), TotalGravada = 100, SubTotalVentas = 100, TotalPagar = 100,
    SelloRecibido = state == "PROCESADO" ? "SELLO-SINTETICO-NO-VALIDO" : null,
    ProcesadoAt = state == "PROCESADO" ? new DateTime(2026, 9, 4) : null,
    EnviadoAt = state is "ENVIADO" or "CONTINGENCIA" or "PROCESADO" ? new DateTime(2026, 9, 4) : null,
    RespuestaHacienda = state is "ENVIADO" or "CONTINGENCIA"
        ? "{\"estado\":\"CONTINGENCIA\",\"clasificaMsg\":\"TIMEOUT\",\"codigoHttp\":0,\"descripcionMsg\":\"Respuesta perdida (fixture sintética)\"}" : null,
    Detalles = [new() { NumeroLinea = 1, Codigo = "QA", Descripcion = "Producto sintético", Cantidad = 1, PrecioUnitario = 100, VentaGravada = 100 }]
};
sealed class PreviewRouter : IRouter {
    public Task RouteAsync(RouteContext context) => Task.CompletedTask;
    public VirtualPathData? GetVirtualPath(VirtualPathContext context) => new(this, $"/{context.Values["controller"] ?? "Account"}/{context.Values["action"] ?? "Login"}");
}
