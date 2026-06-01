using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Catalogos;
using NeoSTP.Application.Clientes;
using NeoSTP.Application.Dashboard;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Application.Dte.Certificacion;
using NeoSTP.Application.Dte.Contingencia;
using NeoSTP.Application.Dte.Diagnostico;
using NeoSTP.Application.Dte.Eventos;
using NeoSTP.Application.Empresas;
using NeoSTP.Application.Licenciamiento;
using NeoSTP.Application.Productos;
using NeoSTP.Application.Roles;
using NeoSTP.Application.Usuarios;
using NeoSTP.Application.Billing;
using NeoSTP.Application.Legal;
using NeoSTP.Application.Workers;
using NeoSTP.Infrastructure.Auth;
using NeoSTP.Infrastructure.Billing;
using NeoSTP.Infrastructure.Dte;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;

namespace NeoSTP.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("NeoStpDb")
            ?? throw new InvalidOperationException("Connection string 'NeoStpDb' not found.");

        services.AddDbContext<NeoStpDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(NeoStpDbContext).Assembly.FullName);
                sql.EnableRetryOnFailure(maxRetryCount: 3);
            }));

        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuditoriaService, AuditoriaService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUsuariosService, UsuariosService>();
        services.AddScoped<IRolesService, RolesService>();
        services.AddScoped<ICatalogosService, CatalogosService>();
        services.AddScoped<ICertificacionDteService, CertificacionDteService>();
        services.AddScoped<IDteEventoService, DteEventoService>();
        services.AddScoped<IDteEventoPdfService, NeoSTP.Infrastructure.Dte.DteEventoPdfService>();
        services.AddScoped<EmpresasService>();
        services.AddScoped<IEmpresasService>(sp => sp.GetRequiredService<EmpresasService>());
        services.AddScoped<ILicenciaResolver>(sp => sp.GetRequiredService<EmpresasService>());
        services.AddScoped<ISucursalesService, SucursalesService>();
        services.AddScoped<IPuntosVentaService, PuntosVentaService>();
        services.AddScoped<IPlanesService, PlanesService>();
        services.AddScoped<IModulosService, ModulosService>();
        services.AddScoped<IClientesService, ClientesService>();
        services.AddScoped<IProductosService, ProductosService>();

        // Sprint 4: cifrado de secretos DTE + cliente Hacienda + servicio config
        services.AddDataProtection().SetApplicationName("NeoSTP.Cloud");
        services.AddScoped<ISecretProtector, DataProtectionSecretProtector>();

        // Cliente Hacienda: toggle "Mock" (default) vs "Http" según Hacienda:Client
        // Sprint 9: clientes Http con Polly StandardResilience (retry + circuit-breaker + timeout)
        var haciendaClient = configuration["Hacienda:Client"];
        if (string.Equals(haciendaClient, "Http", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient(HttpHaciendaAuthClient.HttpClientName)
                .AddStandardResilienceHandler(opts =>
                {
                    // Hasta 3 reintentos con backoff exponencial + jitter
                    opts.Retry.MaxRetryAttempts = 3;
                    opts.Retry.Delay = TimeSpan.FromSeconds(1);
                    // Timeout total por request (incluyendo reintentos)
                    opts.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(90);
                    // Timeout por intento individual — debe ser < SamplingDuration/2
                    opts.AttemptTimeout.Timeout = TimeSpan.FromSeconds(25);
                    // SamplingDuration debe ser >= 2 × AttemptTimeout (25s → mín 50s)
                    opts.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
                });

            services.AddHttpClient(HttpHaciendaReceptionClient.HttpClientName)
                .AddStandardResilienceHandler(opts =>
                {
                    opts.Retry.MaxRetryAttempts = 3;
                    opts.Retry.Delay = TimeSpan.FromSeconds(2);
                    opts.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(120);
                    // Timeout por intento individual — debe ser < SamplingDuration/2
                    opts.AttemptTimeout.Timeout = TimeSpan.FromSeconds(35);
                    // SamplingDuration debe ser >= 2 × AttemptTimeout (35s → mín 70s)
                    opts.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(80);
                });

            services.AddHttpClient(HttpHaciendaContingenciaClient.HttpClientName)
                .AddStandardResilienceHandler(opts =>
                {
                    opts.Retry.MaxRetryAttempts = 3;
                    opts.Retry.Delay = TimeSpan.FromSeconds(2);
                    opts.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(120);
                    opts.AttemptTimeout.Timeout = TimeSpan.FromSeconds(35);
                    opts.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(80);
                });

            services.AddHttpClient(HttpHaciendaEventoClient.HttpClientName)
                .AddStandardResilienceHandler(opts =>
                {
                    opts.Retry.MaxRetryAttempts = 3;
                    opts.Retry.Delay = TimeSpan.FromSeconds(2);
                    opts.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(120);
                    opts.AttemptTimeout.Timeout = TimeSpan.FromSeconds(35);
                    opts.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(80);
                });

            services.AddHttpClient(HttpHaciendaLoteClient.HttpClientName)
                .AddStandardResilienceHandler(opts =>
                {
                    opts.Retry.MaxRetryAttempts = 3;
                    opts.Retry.Delay = TimeSpan.FromSeconds(2);
                    opts.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(120);
                    opts.AttemptTimeout.Timeout = TimeSpan.FromSeconds(35);
                    opts.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(80);
                });

            services.AddHttpClient(HttpHaciendaConsultaLoteClient.HttpClientName)
                .AddStandardResilienceHandler(opts =>
                {
                    opts.Retry.MaxRetryAttempts = 3;
                    opts.Retry.Delay = TimeSpan.FromSeconds(2);
                    opts.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(120);
                    opts.AttemptTimeout.Timeout = TimeSpan.FromSeconds(35);
                    opts.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(80);
                });

            services.AddScoped<IHaciendaAuthClient, HttpHaciendaAuthClient>();
            services.AddScoped<IHaciendaReceptionClient, HttpHaciendaReceptionClient>();
            services.AddScoped<IHaciendaContingenciaClient, HttpHaciendaContingenciaClient>();
            services.AddScoped<IHaciendaEventoClient, HttpHaciendaEventoClient>();
            services.AddScoped<IHaciendaLoteClient, HttpHaciendaLoteClient>();
            services.AddScoped<IHaciendaConsultaLoteClient, HttpHaciendaConsultaLoteClient>();
        }
        else
        {
            services.AddScoped<IHaciendaAuthClient, MockHaciendaAuthClient>();
            services.AddScoped<IHaciendaReceptionClient, MockHaciendaReceptionClient>();
            services.AddScoped<IHaciendaContingenciaClient, MockHaciendaContingenciaClient>();
            services.AddScoped<IHaciendaEventoClient, MockHaciendaEventoClient>();
            services.AddScoped<IHaciendaLoteClient, MockHaciendaLoteClient>();
            services.AddScoped<IHaciendaConsultaLoteClient, MockHaciendaConsultaLoteClient>();
        }

        services.AddScoped<IDteConfiguracionService, DteConfiguracionService>();

        // Sprint 5: generación de documentos DTE
        services.AddScoped<IDteCalculator, DteCalculator>();
        services.AddScoped<IDteGeneratorService, DteGeneratorService>();
        services.AddScoped<IDteDocumentosService, DteDocumentosService>();

        // Sprint 6: firma DTE — toggle "Mock" (default) | "Pkcs12" | "HaciendaCert" según Dte:Signer
        var dteSigner = configuration["Dte:Signer"];
        if (string.Equals(dteSigner, "Pkcs12", StringComparison.OrdinalIgnoreCase))
            services.AddScoped<IDteSignerService, Pkcs12DteSignerService>();
        else if (string.Equals(dteSigner, "HaciendaCert", StringComparison.OrdinalIgnoreCase))
            // Certificado en formato XML CertificadoMH de Hacienda El Salvador (.crt)
            services.AddScoped<IDteSignerService, HaciendaCertMhDteSignerService>();
        else
            services.AddScoped<IDteSignerService, MockDteSignerService>();

        // Sprint 8: Dashboard
        services.AddScoped<IDashboardService, DashboardService>();

        // Sprint 9: Worker jobs
        services.AddScoped<IDteRetransmisionService, DteRetransmisionService>();
        services.AddScoped<ILimpiezaTokensService, LimpiezaTokensService>();

        // Sprint 16: Contingencia avanzada — lotes
        services.AddScoped<IContingenciaLoteService, ContingenciaLoteService>();

        // Sprint 17: Diagnóstico de errores Hacienda
        services.AddScoped<IDiagnosticoHaciendaService, DiagnosticoHaciendaService>();

        // Sprint 18: Legal + consentimiento
        services.AddScoped<ILegalDocumentService, LegalDocumentService>();

        // Sprint 19: Billing self-service — toggle "Mock" (default) | "Stripe" | "MercadoPago"
        services.Configure<BillingOptions>(configuration.GetSection("Billing"));
        var billingProvider = configuration["Billing:Provider"];
        if (string.Equals(billingProvider, "Stripe", StringComparison.OrdinalIgnoreCase))
            services.AddScoped<IPaymentProvider, StripeBillingProvider>();
        else if (string.Equals(billingProvider, "MercadoPago", StringComparison.OrdinalIgnoreCase))
            services.AddScoped<IPaymentProvider, MercadoPagoBillingProvider>();
        else
            services.AddScoped<IPaymentProvider, MockPaymentProvider>();
        services.AddScoped<IBillingService, BillingService>();
        services.AddScoped<IBillingWebhookHandler, BillingWebhookHandler>();

        // Sprint 7: PDF + correo
        services.AddScoped<IDtePdfService, DtePdfService>();
        var emailProvider = configuration["Email:Provider"];
        if (string.Equals(emailProvider, "Smtp", StringComparison.OrdinalIgnoreCase))
            services.AddScoped<IEmailSender, SmtpEmailSender>();
        else
            services.AddScoped<IEmailSender, MockEmailSender>();

        return services;
    }
}
