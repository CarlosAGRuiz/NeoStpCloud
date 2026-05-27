using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Catalogos;
using NeoSTP.Application.Empresas;
using NeoSTP.Application.Licenciamiento;
using NeoSTP.Application.Roles;
using NeoSTP.Application.Usuarios;
using NeoSTP.Infrastructure.Auth;
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

        return services;
    }
}
