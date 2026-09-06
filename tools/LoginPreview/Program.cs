using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NeoSTP.Web.Controllers;
using NeoSTP.Web.Models;

// Solo renderiza Razor: NO AddInfrastructure, Run/Start, SQL, seed, login o llamadas de red.
var workspace = Path.GetFullPath(args.Single());
var webRoot = Path.Combine(workspace, "src", "NeoSTP.Web");
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    ContentRootPath = webRoot,
    WebRootPath = Path.Combine(webRoot, "wwwroot"),
    ApplicationName = typeof(AccountController).Assembly.GetName().Name,
    EnvironmentName = "Development"
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
http.Request.Host = new HostString("login-preview.test");
http.Request.Path = "/Account/Login";
scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = http;
var routes = new RouteData();
routes.Values["controller"] = "Account";
routes.Values["action"] = "Login";
routes.Routers.Add(new PreviewRouter());
var action = new ActionContext(http, routes, new ActionDescriptor());
var engine = scope.ServiceProvider.GetRequiredService<IRazorViewEngine>();
var output = Path.Combine(workspace, "tmp", "login-preview");
Directory.CreateDirectory(output);
await Render("Login", new LoginViewModel(), "login");
await Render("ExternalLink", new LoginViewModel { UsernameOrEmail = "fixture@example.test" }, "external-link");
await Render("MfaEnrollment", new MfaViewModel(), "mfa-enrollment-initial");
await Render("MfaEnrollment", new MfaViewModel
{
    Enrollment = new NeoSTP.Application.Ops.MfaEnrollDto { Secret = "JBSWY3DPEHPK3PXP", OtpAuthUri = "otpauth://totp/synthetic" }
}, "mfa-enrollment");
await Render("MfaVerification", new MfaViewModel(), "mfa-verification");
await Render("MfaRecovery", new NeoSTP.Application.Ops.MfaConfirmDto
{
    RecoveryCodes = Enumerable.Range(0, 10).Select(i => $"TEST0-{i:D5}").ToList()
}, "mfa-recovery");
Console.WriteLine("Razor login y MFA renderizados sin iniciar servicios: " + output);

async Task Render(string view, object model, string filename)
{
    var found = engine.GetView(null, $"/Views/Account/{view}.cshtml", true);
    if (!found.Success) throw new InvalidOperationException(string.Join(", ", found.SearchedLocations));
    using var writer = new StringWriter();
    var data = new ViewDataDictionary(scope.ServiceProvider.GetRequiredService<IModelMetadataProvider>(), new ModelStateDictionary()) { Model = model };
    var temp = new TempDataDictionary(http, scope.ServiceProvider.GetRequiredService<ITempDataProvider>());
    await found.View.RenderAsync(new ViewContext(action, found.View, data, temp, writer, new HtmlHelperOptions()));
    await File.WriteAllTextAsync(Path.Combine(output, filename + ".html"), writer.ToString());
}

sealed class PreviewRouter : IRouter
{
    public Task RouteAsync(RouteContext context) => Task.CompletedTask;
    public VirtualPathData? GetVirtualPath(VirtualPathContext context) => new(this, $"/{context.Values["controller"] ?? "Account"}/{context.Values["action"] ?? "Login"}");
}
