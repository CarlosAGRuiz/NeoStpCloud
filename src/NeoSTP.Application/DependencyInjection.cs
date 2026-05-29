using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NeoSTP.Application.Auth;

namespace NeoSTP.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<SuperAdminOptions>(configuration.GetSection(SuperAdminOptions.SectionName));
        services.Configure<NeoSTP.Application.Dte.HaciendaOptions>(configuration.GetSection(NeoSTP.Application.Dte.HaciendaOptions.SectionName));
        services.Configure<NeoSTP.Application.Dte.EmailOptions>(configuration.GetSection(NeoSTP.Application.Dte.EmailOptions.SectionName));
        services.Configure<NeoSTP.Application.Provisioning.EmpresaPruebaOptions>(configuration.GetSection(NeoSTP.Application.Provisioning.EmpresaPruebaOptions.SectionName));
        return services;
    }
}
