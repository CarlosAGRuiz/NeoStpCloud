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
using NeoSTP.Infrastructure.Persistence.Seed;
using NeoSTP.Shared;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.Configure<LegalOptions>(builder.Configuration.GetSection("Legal"));
builder.Services.Configure<RateLimitOptions>(builder.Configuration.GetSection(RateLimitOptions.SectionName));

builder.Services.AddScoped<ICurrentUser, CurrentUserAccessor>();

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt section missing in configuration.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.SaveToken = true;
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

// Aplicar migraciones + seed inicial al arrancar
await DatabaseSeeder.SeedAsync(app.Services);
// Provisioning idempotente de la empresa de pruebas (Sprint 11) — solo si EmpresaPrueba:Enabled=true
await EmpresaPruebaSeeder.SeedAsync(app.Services);

app.UseSerilogRequestLogging();

// Spec OpenAPI público (documentación de la NeoConnect API). El acceso a los
// endpoints sigue protegido por API Key / JWT; solo el esquema es público.
app.MapOpenApi();

app.UseHttpsRedirection();
app.UseSecurityHeaders();
app.UseCors();
app.UseAuthentication();
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

try
{
    Log.Information("Starting NeoSTP.Api host");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "NeoSTP.Api terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

static class SecurityHeadersExtensions
{
    private const string ApiCsp = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
        => app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers.TryAdd("X-Content-Type-Options", "nosniff");
                headers.TryAdd("X-Frame-Options", "DENY");
                headers.TryAdd("X-XSS-Protection", "0");
                headers.TryAdd("Referrer-Policy", "no-referrer");
                headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=(), payment=()");
                headers.TryAdd("Content-Security-Policy", ApiCsp);
                return Task.CompletedTask;
            });

            await next();
        });
}
