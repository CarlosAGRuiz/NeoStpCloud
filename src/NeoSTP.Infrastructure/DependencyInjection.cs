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
using NeoSTP.Application.Cobranza;
using NeoSTP.Application.Notificaciones;
using NeoSTP.Application.Onboarding;
using NeoSTP.Application.Scan;
using NeoSTP.Application.Productos;
using NeoSTP.Application.Profit;
using NeoSTP.Application.Roles;
using NeoSTP.Application.Usuarios;
using NeoSTP.Application.Billing;
using NeoSTP.Application.Connect;
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
        services.AddScoped<IAuditoriaQueryService, AuditoriaQueryService>();
        // Seguridad (M6.2): política de contraseña + bloqueo configurables.
        services.Configure<NeoSTP.Application.Auth.SecurityOptions>(configuration.GetSection(NeoSTP.Application.Auth.SecurityOptions.SectionName));
        services.AddScoped<IPasswordPolicy, PasswordPolicy>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUsuariosService, UsuariosService>();
        services.AddScoped<IRolesService, RolesService>();
        services.AddScoped<ICatalogosService, CatalogosService>();
        // V2.5-S4: caché distribuida para lookups/catálogos. Memory por defecto (una instancia);
        // Redis para multi-instancia: Cache:Provider=Redis + Cache:Redis:ConnectionString.
        var cacheProvider = configuration["Cache:Provider"];
        if (string.Equals(cacheProvider, "Redis", StringComparison.OrdinalIgnoreCase))
        {
            services.AddStackExchangeRedisCache(o =>
            {
                o.Configuration = configuration["Cache:Redis:ConnectionString"];
                o.InstanceName = configuration["Cache:Redis:InstanceName"] ?? "neostp:";
            });
        }
        else
        {
            services.AddDistributedMemoryCache();
        }
        services.AddScoped<NeoSTP.Application.Lookups.ILookupCacheInvalidator, LookupCacheInvalidator>();
        services.AddScoped<NeoSTP.Application.Lookups.ILookupService, LookupService>();

        // V2.5-S4: storage externo para archivos de NeoScan (default: blob en BD).
        if (string.Equals(configuration["Scan:Storage:Provider"], "FileSystem", StringComparison.OrdinalIgnoreCase))
        {
            var scanRoot = configuration["Scan:Storage:Root"] ?? "scan-blobs";
            services.AddSingleton<NeoSTP.Infrastructure.Scan.IScanBlobStorage>(new NeoSTP.Infrastructure.Scan.FileSystemScanBlobStorage(scanRoot));
        }
        services.AddScoped<NeoSTP.Application.Lookups.INitVerificationService, NitVerificationService>();
        services.AddScoped<ICertificacionDteService, CertificacionDteService>();
        services.AddScoped<IDteEventoService, DteEventoService>();
        services.AddScoped<IDteEventoPdfService, NeoSTP.Infrastructure.Dte.DteEventoPdfService>();
        services.AddScoped<EmpresasService>();
        services.AddScoped<IEmpresasService>(sp => sp.GetRequiredService<EmpresasService>());
        services.AddScoped<ILicenciaResolver>(sp => sp.GetRequiredService<EmpresasService>());
        services.AddScoped<IBrandingService, BrandingService>();
        services.AddScoped<ISucursalesService, SucursalesService>();
        services.AddScoped<IPuntosVentaService, PuntosVentaService>();
        services.AddScoped<IPlanesService, PlanesService>();
        services.AddScoped<NeoSTP.Application.Licenciamiento.ILicenciaGuardService, LicenciaGuardService>();
        services.AddScoped<NeoSTP.Application.Usuarios.IUsuarioEmpresaService, UsuarioEmpresaService>();
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
        services.Configure<NeoSTP.Application.Dte.TerritorialOptions>(configuration.GetSection(NeoSTP.Application.Dte.TerritorialOptions.SectionName));
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

        // Onboarding self-service: estado de activación derivado de datos reales
        services.AddScoped<IOnboardingService, OnboardingService>();
        services.AddScoped<NeoSTP.Application.Onboarding.IVerticalTemplateService, VerticalTemplateService>();
        services.AddScoped<NeoSTP.Application.Agenda.IAgendaService, AgendaService>();

        // NeoProfit (Sprint 22): cálculo financiero sobre DTE + gastos/compras
        services.AddScoped<IProfitService, ProfitService>();

        // Cobranza / Cuentas por cobrar (B-2 NeoCloud Mobile)
        services.AddScoped<ICobranzaService, CobranzaService>();
        services.AddScoped<IRecordatorioCobroService, RecordatorioCobroService>();
        // QR / enlaces de cobro (B-5)
        services.AddScoped<ICobroQrService, CobroQrService>();

        // NeoScanAI (Sprint 23 / B-3): bandeja + extracción (mock por defecto, pluggable)
        services.AddScoped<IScanService, ScanService>();
        // DTE recibidos (registro/respaldo de proveedores, generados desde NeoScanAI) — solo lectura
        services.AddScoped<IDteRecibidoService, DteRecibidoService>();

        // RRHH / Nómina (NEORRHH — V2): empleados + parámetros de nómina ES (parametrizables).
        services.Configure<NeoSTP.Application.Rrhh.NominaOptions>(configuration.GetSection(NeoSTP.Application.Rrhh.NominaOptions.SectionName));
        services.AddScoped<NeoSTP.Application.Rrhh.IEmpleadosService, EmpleadosService>();
        services.AddScoped<NeoSTP.Application.Rrhh.IPlanillaService, PlanillaService>();
        services.AddScoped<NeoSTP.Application.Rrhh.IPrestacionesRrhhService, PrestacionesRrhhService>();
        services.AddScoped<NeoSTP.Application.Rrhh.INominaPdfService, NeoSTP.Infrastructure.Rrhh.NominaPdfService>();

        // Tesorería (NEOTESORERIA — V2): cuentas banco/caja + movimientos.
        services.AddScoped<NeoSTP.Application.Tesoreria.ITesoreriaService, TesoreriaService>();
        services.AddScoped<NeoSTP.Application.Tesoreria.IConciliacionBancariaService, ConciliacionBancariaService>();
        services.AddScoped<NeoSTP.Application.Ops.IOperacionPanelService, OperacionPanelService>();
        services.AddScoped<NeoSTP.Application.Ops.ILimpiezaAuditoriaService, LimpiezaAuditoriaService>();

        // Compras / CxP (NEOCOMPRAS — V2): proveedores + facturas de compra + pagos.
        services.AddScoped<NeoSTP.Application.Compras.ICompraService, CompraService>();
        services.AddScoped<NeoSTP.Application.Compras.IOrdenCompraService, OrdenCompraService>();
        services.AddScoped<NeoSTP.Application.Crm.ICrmService, CrmService>();

        // NeoPortal receptor (V2-C2): enlaces públicos firmados/expirables.
        services.AddScoped<NeoSTP.Application.Portal.IPortalService, PortalService>();

        // NEOBI fiscal (V2-D1): libros IVA + F-07 (solo lectura).
        services.AddScoped<NeoSTP.Application.Reportes.IReporteFiscalService, ReporteFiscalService>();

        // NEOCONTA (V2-D2): asientos automáticos + balanza.
        services.AddScoped<NeoSTP.Application.Conta.IContabilidadService, ContabilidadService>();

        // Punto de venta (NEOPOS — V2): ventas + ticket PDF térmico + impresoras/ESC-POS.
        services.Configure<NeoSTP.Application.Pos.PosOptions>(configuration.GetSection(NeoSTP.Application.Pos.PosOptions.SectionName));
        services.AddScoped<NeoSTP.Application.Pos.IPosService, PosService>();
        services.AddScoped<NeoSTP.Application.Pos.IPosConfigService, PosConfigService>();
        services.AddScoped<NeoSTP.Application.Pos.IPosCajaService, PosCajaService>();
        services.AddSingleton<NeoSTP.Application.Pos.ITicketPdfService, NeoSTP.Infrastructure.Pos.TicketPdfService>();
        services.AddSingleton<NeoSTP.Application.Pos.INetworkPrinter, NeoSTP.Infrastructure.Pos.TcpNetworkPrinter>();

        // Comunicaciones (correo por empresa — V2): SMTP por tenant + configuración cifrada.
        services.AddScoped<NeoSTP.Application.Comunicaciones.ITenantEmailSender, NeoSTP.Infrastructure.Comunicaciones.TenantEmailSender>();
        services.AddScoped<NeoSTP.Application.Comunicaciones.IConfiguracionCorreoService, ConfiguracionCorreoService>();

        // Inventario (INVENTARIO — V2): existencias + kardex + costo promedio.
        services.AddScoped<NeoSTP.Application.Inventario.IInventarioService, InventarioService>();
        // Toggle del proveedor de extracción OCR/IA (Scan:Provider). Mock por defecto; Gemini real (M2.1).
        var scanProvider = configuration["Scan:Provider"];
        if (string.Equals(scanProvider, "Gemini", StringComparison.OrdinalIgnoreCase))
        {
            services.Configure<NeoSTP.Infrastructure.Scan.GeminiScanOptions>(configuration.GetSection("Scan:Gemini"));
            services.AddHttpClient(NeoSTP.Infrastructure.Scan.GeminiScanExtractionService.HttpClientName)
                .AddStandardResilienceHandler(opts =>
                {
                    opts.Retry.MaxRetryAttempts = 2;
                    opts.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(90);
                    opts.AttemptTimeout.Timeout = TimeSpan.FromSeconds(45);
                    opts.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(90);
                });
            services.AddScoped<IScanExtractionService, NeoSTP.Infrastructure.Scan.GeminiScanExtractionService>();
        }
        else
        {
            services.AddScoped<IScanExtractionService, NeoSTP.Infrastructure.Scan.MockScanExtractionService>();
        }

        // Alertas y notificaciones push (B-4): centro de alertas + generación + push pluggable
        services.AddScoped<IAlertaService, AlertaService>();
        services.AddScoped<IAlertaGeneracionService, AlertaGeneracionService>();
        // Toggle del proveedor de push (Push:Provider). Mock por defecto; Fcm real (M2.2).
        var pushProvider = configuration["Push:Provider"];
        if (string.Equals(pushProvider, "Fcm", StringComparison.OrdinalIgnoreCase))
        {
            services.Configure<NeoSTP.Infrastructure.Notificaciones.FcmOptions>(configuration.GetSection("Push:Fcm"));
            services.AddHttpClient(NeoSTP.Infrastructure.Notificaciones.FcmPushSender.HttpClientName)
                .AddStandardResilienceHandler(opts =>
                {
                    opts.Retry.MaxRetryAttempts = 2;
                    opts.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(40);
                    opts.AttemptTimeout.Timeout = TimeSpan.FromSeconds(15);
                    opts.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(40);
                });
            services.AddSingleton<NeoSTP.Infrastructure.Notificaciones.IFcmAccessTokenProvider, NeoSTP.Infrastructure.Notificaciones.ServiceAccountTokenProvider>();
            services.AddScoped<IPushSender, NeoSTP.Infrastructure.Notificaciones.FcmPushSender>();
        }
        else
        {
            services.AddScoped<IPushSender, NeoSTP.Infrastructure.Notificaciones.MockPushSender>();
        }
        // Toggle del proveedor de WhatsApp (WhatsApp:Provider). Mock por defecto; Meta Cloud API (V2.5-S2).
        var whatsAppProvider = configuration["WhatsApp:Provider"];
        if (string.Equals(whatsAppProvider, "Meta", StringComparison.OrdinalIgnoreCase))
        {
            services.Configure<NeoSTP.Infrastructure.Notificaciones.MetaWhatsAppOptions>(configuration.GetSection("WhatsApp:Meta"));
            services.AddHttpClient(NeoSTP.Infrastructure.Notificaciones.MetaWhatsAppSender.HttpClientName)
                .AddStandardResilienceHandler(opts =>
                {
                    opts.Retry.MaxRetryAttempts = 2;
                    opts.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(40);
                    opts.AttemptTimeout.Timeout = TimeSpan.FromSeconds(15);
                    opts.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(40);
                });
            services.AddScoped<IWhatsAppSender, NeoSTP.Infrastructure.Notificaciones.MetaWhatsAppSender>();
        }
        else
        {
            services.AddScoped<IWhatsAppSender, NeoSTP.Infrastructure.Notificaciones.MockWhatsAppSender>();
        }

        // Sprint 9: Worker jobs
        services.AddScoped<IDteRetransmisionService, DteRetransmisionService>();
        services.AddScoped<ILimpiezaTokensService, LimpiezaTokensService>();

        // Sprint 16: Contingencia avanzada — lotes
        services.AddScoped<IContingenciaLoteService, ContingenciaLoteService>();

        // Sprint 17: Diagnóstico de errores Hacienda
        services.AddScoped<IDiagnosticoHaciendaService, DiagnosticoHaciendaService>();

        // Sprint 18: Legal + consentimiento
        services.AddScoped<ILegalDocumentService, LegalDocumentService>();

        // Sprint 24: NeoConnect API
        services.AddScoped<IConnectApiKeyService, ConnectApiKeyService>();
        services.AddScoped<IConnectWebhookService, ConnectWebhookService>();
        services.AddScoped<IConnectWebhookDispatcher, ConnectWebhookDispatcher>();
        services.AddScoped<IConnectDteService, ConnectDteService>();

        // Sprint 20: Hardening — cuotas / rate limiting, MFA (TOTP), IP allowlist
        services.AddScoped<NeoSTP.Application.Ops.IApiQuotaService, ApiQuotaService>();
        services.AddSingleton<NeoSTP.Application.Ops.ITotpService, TotpService>();
        services.AddScoped<NeoSTP.Application.Ops.IMfaService, MfaService>();
        services.AddScoped<NeoSTP.Application.Ops.IAdminIpAllowlistService, AdminIpAllowlistService>();

        // Sprint 20: Backups — toggle LOCAL (default) | AZURE_BLOB | S3
        services.Configure<NeoSTP.Application.Ops.BackupOptions>(configuration.GetSection(NeoSTP.Application.Ops.BackupOptions.SectionName));
        var storageProvider = configuration["Hardening:Backup:StorageProvider"];
        if (string.Equals(storageProvider, "AZURE_BLOB", StringComparison.OrdinalIgnoreCase)
            || string.Equals(storageProvider, "S3", StringComparison.OrdinalIgnoreCase))
            services.AddScoped<NeoSTP.Application.Ops.IStorageService, ExternalStorageService>();
        else
            services.AddScoped<NeoSTP.Application.Ops.IStorageService, LocalStorageService>();
        services.AddScoped<NeoSTP.Application.Ops.IBackupService, BackupService>();

        // Sprint 19 + Pagos LATAM: Billing self-service multi-proveedor.
        // Todos los proveedores se registran; el cliente elige método en el checkout
        // y IPaymentProviderResolver resuelve por nombre. Billing:Provider = default.
        services.Configure<BillingOptions>(configuration.GetSection("Billing"));
        services.AddScoped<IPaymentProvider, MockPaymentProvider>();
        services.AddScoped<IPaymentProvider, StripeBillingProvider>();
        services.AddScoped<IPaymentProvider, MercadoPagoBillingProvider>();
        // Pagos LATAM — Wompi (HttpClient resiliente)
        services.AddHttpClient(WompiBillingProvider.HttpClientName)
            .AddStandardResilienceHandler(opts =>
            {
                opts.Retry.MaxRetryAttempts = 2;
                opts.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(40);
                opts.AttemptTimeout.Timeout = TimeSpan.FromSeconds(15);
                opts.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(40);
            });
        services.AddScoped<IPaymentProvider, WompiBillingProvider>();
        // Pagos LATAM — PayPal (HttpClient resiliente)
        services.AddHttpClient(PayPalBillingProvider.HttpClientName)
            .AddStandardResilienceHandler(opts =>
            {
                opts.Retry.MaxRetryAttempts = 2;
                opts.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(40);
                opts.AttemptTimeout.Timeout = TimeSpan.FromSeconds(15);
                opts.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(40);
            });
        services.AddScoped<IPaymentProvider, PayPalBillingProvider>();
        services.AddScoped<IPaymentProvider, TransferenciaPaymentProvider>();
        services.AddScoped<IPaymentProviderResolver, PaymentProviderResolver>();
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
