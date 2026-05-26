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
        return services;
    }
}
