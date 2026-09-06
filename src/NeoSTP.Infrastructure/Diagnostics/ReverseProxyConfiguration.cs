using Microsoft.AspNetCore.Builder;
using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;

namespace NeoSTP.Infrastructure.Diagnostics;

/// <summary>Shared policy for the current loopback Cloudflare tunnel; remote proxy trust requires explicit review.</summary>
public static class ReverseProxyConfiguration
{
    public static IServiceCollection AddNeoStpForwardedHeaders(this IServiceCollection services)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
            options.KnownProxies.Add(IPAddress.Loopback);
            options.KnownProxies.Add(IPAddress.IPv6Loopback);
        });
        return services;
    }
}