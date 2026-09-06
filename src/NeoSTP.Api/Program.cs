using Microsoft.Extensions.Hosting.WindowsServices;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using NeoSTP.Api.Auth;
using NeoSTP.Api.Authorization;
using NeoSTP.Api.Middlewares;
using NeoSTP.Application;
using NeoSTP.Application.Auth;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Legal;
using NeoSTP.Application.Ops;
using NeoSTP.Infrastructure;
using NeoSTP.Infrastructure.Auth;
using NeoSTP.Infrastructure.Diagnostics;
using NeoSTP.Infrastructure.Persistence.Seed;
using NeoSTP.Shared;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : null
});
builder.Services.AddWindowsService(options => options.ServiceName = "NeoSTP.Api");

HostConfiguration.AddLocalDevelopmentSettings(builder.Configuration, builder.Environment);
builder.Services.Configure<Microsoft.AspNetCore.HttpsPolicy.HttpsRedirectionOptions>(
    builder.Configuration.GetSection("HttpsRedirection"));

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddApiAuthRateLimiting(builder.Configuration);

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddNeoStpHealthChecks();
builder.Services.AddNeoStpObservability(builder.Configuration, "neostp-api");
builder.Services.Configure<LegalOptions>(builder.Configuration.GetSection("Legal"));
builder.Services.Configure<RateLimitOptions>(builder.Configuration.GetSection(RateLimitOptions.SectionName));

builder.Services.AddScoped<ICurrentUser, CurrentUserAccessor>();
builder.Services.AddScoped<SessionJwtEvents>();

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt section missing in configuration.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.SaveToken = true;
        options.EventsType = typeof(SessionJwtEvents);
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermisoAuthorizationHandler>();
builder.Services.AddScoped<IAuthorizationHandler, ModuloAuthorizationHandler>();
builder.Services.AddAuthorization();

var allowedCorsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader().AllowAnyMethod();

        if (allowedCorsOrigins.Length > 0)
        {
            policy.WithOrigins(allowedCorsOrigins);
        }
        else if (builder.Environment.IsDevelopment())
        {
            policy.AllowAnyOrigin();
        }
        else
        {
            policy.SetIsOriginAllowed(_ => false);
        }
    });
});

var app = builder.Build();

// Fail-fast: en Producción no se arranca con providers Mock (correo, billing, scan, whatsapp, push).
NeoSTP.Infrastructure.Diagnostics.ProductionGuards.ValidarProvidersDeProduccion(app.Configuration, app.Environment);

// Production validates the deployed schema; migrations and seed are a separate deployment operation.
await DatabaseStartup.InitializeAsync(app.Services, app.Configuration, app.Environment);

app.UseSerilogRequestLogging();

// Spec OpenAPI público (documentación de la NeoConnect API). El acceso a los
// endpoints sigue protegido por API Key / JWT; solo el esquema es público.
app.MapOpenApi();

// Scalar: explorador interactivo de la API en /scalar/v1 (lee /openapi/v1.json).
// Botón "Authorize" para pegar el Bearer JWT y probar endpoints.
app.MapScalarApiReference(options =>
{
    options
        .WithTitle("NeoSTP Cloud API")
        .WithTheme(ScalarTheme.BluePlanet)
        .WithDefaultHttpClient(ScalarTarget.Shell, ScalarClient.Curl);
});

app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.UseHttpsRedirection();
app.UseSecurityHeaders();
app.UseRouting();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<MfaChallengeMiddleware>();
app.UseMiddleware<ApiKeyAuthMiddleware>();
app.UseMiddleware<AdminIpAllowlistMiddleware>();
app.UseMiddleware<CurrentTenantMiddleware>();
app.UseMiddleware<ApiQuotaMiddleware>();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", () => ApiResponse<object>.Ok(new
{
    status = "ok",
    service = "NeoSTP.Api",
    timestamp = DateTime.UtcNow
}));
app.MapNeoStpHealthChecks();

try
{
    Log.Information("Starting NeoSTP.Api host");
    app.Run();
}
catch (Exception ex)
{
    Environment.ExitCode = 1;
    Log.Fatal(ex, "NeoSTP.Api terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

static class SecurityHeadersExtensions
{
    // CSP estricta para la API JSON (no renderiza HTML/JS).
    private const string ApiCsp = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";

    // CSP permisiva solo para la UI de Scalar (necesita cargar su bundle JS/CSS y fuentes).
    private const string ScalarCsp =
        "default-src 'self'; base-uri 'self'; " +
        "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdn.jsdelivr.net; " +
        "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com https://cdn.jsdelivr.net data:; " +
        "img-src 'self' data: https:; " +
        "connect-src 'self' https://cdn.jsdelivr.net; worker-src 'self' blob:";

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
        => app.Use(async (context, next) =>
        {
            var isScalar = context.Request.Path.StartsWithSegments("/scalar");

            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers.TryAdd("X-Content-Type-Options", "nosniff");
                if (!isScalar) headers.TryAdd("X-Frame-Options", "DENY");
                headers.TryAdd("X-XSS-Protection", "0");
                headers.TryAdd("Referrer-Policy", "no-referrer");
                headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=(), payment=()");
                headers.TryAdd("Content-Security-Policy", isScalar ? ScalarCsp : ApiCsp);
                return Task.CompletedTask;
            });

            await next();
        });
}
