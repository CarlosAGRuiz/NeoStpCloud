using NeoSTP.Infrastructure.Auth;
using NeoSTP.Infrastructure.Diagnostics;

namespace NeoSTP.Api.Auth;

public static class ApiAuthRateLimiting
{
    public static IServiceCollection AddApiAuthRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthRateLimiting(configuration);
        services.AddNeoStpForwardedHeaders();
        return services;
    }
}