using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using NeoSTP.Application;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Legal;
using NeoSTP.Infrastructure;
using NeoSTP.Infrastructure.Diagnostics;
using NeoSTP.Web.Auth;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddControllersWithViews()
    .AddViewLocalization();
builder.Services.AddHttpContextAccessor();

// V2.5-S6: i18n base es/en. Español por defecto; el idioma se persiste en la cookie
// estándar de cultura (acción Home/CambiarIdioma).
builder.Services.AddLocalization(o => o.ResourcesPath = "Resources");
builder.Services.Configure<Microsoft.AspNetCore.Builder.RequestLocalizationOptions>(o =>
{
    var cultures = new[] { "es", "en" };
    o.SetDefaultCulture("es");
    o.AddSupportedCultures(cultures);
    o.AddSupportedUICultures(cultures);
});

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddNeoStpHealthChecks();
builder.Services.AddNeoStpObservability(builder.Configuration, "neostp-web");
builder.Services.Configure<LegalOptions>(builder.Configuration.GetSection("Legal"));
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.MinimumSameSitePolicy = SameSiteMode.Lax;
    options.Secure = CookieSecurePolicy.Always;
    options.HttpOnly = Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy.Always;
});

builder.Services.AddScoped<ICurrentUser, CookieCurrentUser>();
builder.Services.AddScoped<NeoSTP.Application.Empresas.IEmpresaContext, NeoSTP.Web.Auth.WebEmpresaContext>();

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
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Fail-fast: en Producción no se arranca con providers Mock (correo, billing, scan, push).
NeoSTP.Infrastructure.Diagnostics.ProductionGuards.ValidarProvidersDeProduccion(app.Configuration, app.Environment);

// Aplicar migraciones + seed al arrancar (idempotente). Garantiza que la Web tenga el
// esquema al día aunque se ejecute sin la API; EF serializa con __EFMigrationsLock.
await NeoSTP.Infrastructure.Persistence.Seed.DatabaseSeeder.SeedAsync(app.Services);
await NeoSTP.Infrastructure.Persistence.Seed.EmpresaPruebaSeeder.SeedAsync(app.Services);

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
app.UseAuthentication();
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

app.MapStaticAssets();

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
