using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using NeoSTP.Application;
using NeoSTP.Application.Auth;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Legal;
using NeoSTP.Infrastructure;
using NeoSTP.Infrastructure.Auth;
using NeoSTP.Infrastructure.Diagnostics;
using NeoSTP.Web.Auth;
using Serilog;
using System.Net;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : null
});
builder.Services.AddWindowsService(options => options.ServiceName = "NeoSTP.Web");

HostConfiguration.AddLocalDevelopmentSettings(builder.Configuration, builder.Environment);
builder.Services.Configure<Microsoft.AspNetCore.HttpsPolicy.HttpsRedirectionOptions>(
    builder.Configuration.GetSection("HttpsRedirection"));

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddControllersWithViews(options =>
    {
        // Mensajes de binding en español y con el nombre del campo. Por defecto ASP.NET
        // responde "The value '' is invalid." — en inglés y sin decir qué campo, lo que
        // deja al usuario adivinando en un formulario largo como el de emisión de DTE.
        var m = options.ModelBindingMessageProvider;
        static string Campo(string? nombre) =>
            string.IsNullOrWhiteSpace(nombre) ? "un campo obligatorio" : $"el campo {nombre}";
        m.SetValueMustNotBeNullAccessor(campo => $"Falta {Campo(campo)}.");
        m.SetMissingBindRequiredValueAccessor(campo => $"Falta {Campo(campo)}.");
        m.SetAttemptedValueIsInvalidAccessor((valor, campo) =>
            string.IsNullOrEmpty(valor)
                ? $"Falta {Campo(campo)}. Revisa que las cantidades y montos no estén vacíos."
                : $"El valor '{valor}' no es válido para {Campo(campo)}.");
        m.SetUnknownValueIsInvalidAccessor(campo => $"El valor indicado en {campo} no es válido.");
        m.SetValueIsInvalidAccessor(valor => $"El valor {valor} no es válido.");
        m.SetNonPropertyAttemptedValueIsInvalidAccessor(valor => $"El valor '{valor}' no es válido.");
        m.SetMissingKeyOrValueAccessor(() => "Este campo es obligatorio.");
    })
    .AddViewLocalization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthRateLimiting(builder.Configuration);

// Cloudflare Tunnel termina TLS y reenvía la petición a Kestrel por localhost.
// Solo se confían los encabezados del proxy local para conservar el esquema HTTPS
// sin permitir que clientes externos falsifiquen X-Forwarded-*.
builder.Services.AddNeoStpForwardedHeaders();

// V2.5-S6: i18n base es/en. Español por defecto; el idioma se persiste en la cookie
// estándar de cultura (acción Home/CambiarIdioma).
//
// OJO con la cultura: debe ser es-SV (El Salvador), no el "es" genérico. El Salvador usa
// punto decimal y dólar; el "es" genérico usa coma decimal y euro. Como los <input type="number">
// del navegador SIEMPRE envían el punto decimal (lo exige el estándar HTML), con "es" el binder
// leía 3.25 como 325 — es decir, multiplicaba por 100 los precios capturados en los formularios.
// Los códigos de recursos siguen resolviendo (.es.resx) porque es-SV cae a "es" por herencia.
builder.Services.AddLocalization(o => o.ResourcesPath = "Resources");
builder.Services.Configure<Microsoft.AspNetCore.Builder.RequestLocalizationOptions>(o =>
{
    var cultures = new[] { "es-SV", "en-US" };
    o.SetDefaultCulture("es-SV");
    o.AddSupportedCultures(cultures);
    o.AddSupportedUICultures(cultures);
});

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddNeoStpHealthChecks();
builder.Services.AddNeoStpObservability(builder.Configuration, "neostp-web");
builder.Services.Configure<LegalOptions>(builder.Configuration.GetSection("Legal"));
builder.Services.Configure<CookiePolicyOptions>(WebCookiePolicy.Configure);

builder.Services.AddScoped<ICurrentUser, CookieCurrentUser>();
builder.Services.AddScoped<SessionCookieEvents>();
builder.Services.AddScoped<NeoSTP.Application.Empresas.IEmpresaContext, NeoSTP.Web.Auth.WebEmpresaContext>();

// E3: SSO OIDC. Deshabilitado por defecto; solo registra esquemas si Sso:Enabled + credenciales.
var ssoOptions = builder.Configuration.GetSection(NeoSTP.Application.Auth.SsoOptions.SectionName)
    .Get<NeoSTP.Application.Auth.SsoOptions>() ?? new NeoSTP.Application.Auth.SsoOptions();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.Name = "NeoStp.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.EventsType = typeof(SessionCookieEvents);
    })
    .AddNeoStpSso(ssoOptions);

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseForwardedHeaders();

// Fail-fast: en Producción no se arranca con providers Mock (correo, billing, scan, push).
NeoSTP.Infrastructure.Diagnostics.ProductionGuards.ValidarProvidersDeProduccion(app.Configuration, app.Environment);

// Production validates the deployed schema; migrations and seed are a separate deployment operation.
await DatabaseStartup.InitializeAsync(app.Services, app.Configuration, app.Environment);

app.UseSerilogRequestLogging();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseSecurityHeaders();
app.UseCookiePolicy();
app.UseRequestLocalization();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<MfaChallengeMiddleware>();
app.UseAuthorization();

// Enforcement comercial: una empresa no ACTIVA (suspendida/vencida) no navega la web.
// Se cierra la sesión y se regresa al login con el motivo.
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;
    var esExento = path.StartsWith("/Account", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
                || path.Contains('.');
    if (!esExento && context.User.Identity?.IsAuthenticated == true)
    {
        var currentUser = context.RequestServices.GetRequiredService<NeoSTP.Application.Auth.Abstractions.ICurrentUser>();
        if (currentUser.TipoUsuarioCodigo != "SUPERADMIN" && currentUser.EmpresaId is int empresaId)
        {
            var licencia = context.RequestServices.GetRequiredService<NeoSTP.Application.Licenciamiento.ILicenciaGuardService>();
            if (!await licencia.EmpresaOperativaAsync(empresaId, context.RequestAborted))
            {
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                context.Response.Redirect("/Account/Login?motivo=suspendida");
                return;
            }
        }
    }
    await next(context);
});

app.MapStaticAssets().WithMetadata(new AllowMfaChallengeAttribute(SessionClaims.MfaEnroll, SessionClaims.MfaVerify));

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapNeoStpHealthChecks();

try
{
    Log.Information("Starting NeoSTP.Web host");
    app.Run();
}
catch (Exception ex)
{
    Environment.ExitCode = 1;
    Log.Fatal(ex, "NeoSTP.Web terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

static class SecurityHeadersExtensions
{
    private const string WebCsp = "default-src 'self'; " +
        "base-uri 'self'; " +
        "frame-ancestors 'none'; " +
        "form-action 'self'; " +
        "img-src 'self' data:; " +
        "script-src 'self' 'unsafe-inline'; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com data:; " +
        "connect-src 'self'; " +
        "object-src 'none'; " +
        "upgrade-insecure-requests";

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
        => app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers.TryAdd("X-Content-Type-Options", "nosniff");
                headers.TryAdd("X-Frame-Options", "DENY");
                headers.TryAdd("X-XSS-Protection", "0");
                headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
                headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=(), payment=()");
                headers.TryAdd("Content-Security-Policy", WebCsp);
                return Task.CompletedTask;
            });

            await next();
        });
}
