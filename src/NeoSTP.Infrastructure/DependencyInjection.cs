using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Catalogos;
using NeoSTP.Application.Clientes;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Application.Empresas;
using NeoSTP.Application.Licenciamiento;
using NeoSTP.Application.Productos;
using NeoSTP.Application.Roles;
using NeoSTP.Application.Usuarios;
using NeoSTP.Infrastructure.Auth;
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
        var haciendaClient = configuration["Hacienda:Client"];
        if (string.Equals(haciendaClient, "Http", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient(HttpHaciendaAuthClient.HttpClientName);
            services.AddHttpClient(HttpHaciendaReceptionClient.HttpClientName);
            services.AddScoped<IHaciendaAuthClient, HttpHaciendaAuthClient>();
            services.AddScoped<IHaciendaReceptionClient, HttpHaciendaReceptionClient>();
        }
        else
        {
            services.AddScoped<IHaciendaAuthClient, MockHaciendaAuthClient>();
            services.AddScoped<IHaciendaReceptionClient, MockHaciendaReceptionClient>();
        }

        services.AddScoped<IDteConfiguracionService, DteConfiguracionService>();

        // Sprint 5: generación de documentos DTE
        services.AddScoped<IDteCalculator, DteCalculator>();
        services.AddScoped<IDteGeneratorService, DteGeneratorService>();
        services.AddScoped<IDteDocumentosService, DteDocumentosService>();

        // Sprint 6: firma DTE — toggle "Mock" (default) vs "Pkcs12" según Dte:Signer
        var dteSigner = configuration["Dte:Signer"];
        if (string.Equals(dteSigner, "Pkcs12", StringComparison.OrdinalIgnoreCase))
            services.AddScoped<IDteSignerService, Pkcs12DteSignerService>();
        else
            services.AddScoped<IDteSignerService, MockDteSignerService>();

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
