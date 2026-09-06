using System.Net;
using System.Net.Sockets;

namespace NeoSTP.Infrastructure.Connect;

/// <summary>Dedicated webhook egress. TLS/SNI/certificate checks remain with SocketsHttpHandler.</summary>
public static class WebhookHttpTransport
{
    public const string HttpClientName = "NeoConnectWebhooks";

    public static SocketsHttpHandler CreateHandler(WebhookDestinationPolicy policy)
        => CreateHandler(policy, ConnectSocketAsync);

    internal static SocketsHttpHandler CreateHandler(WebhookDestinationPolicy policy,
        Func<IPEndPoint, CancellationToken, ValueTask<Stream>> connect)
        => new()
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            UseCookies = false,
            ConnectTimeout = TimeSpan.FromSeconds(10),
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            ConnectCallback = (context, ct) => ConnectAsync(policy, context.InitialRequestMessage,
                context.DnsEndPoint, connect, ct),
        };

    internal static async ValueTask<Stream> ConnectAsync(WebhookDestinationPolicy policy,
        HttpRequestMessage request, DnsEndPoint endpoint,
        Func<IPEndPoint, CancellationToken, ValueTask<Stream>> connect, CancellationToken ct)
    {
        var uri = WebhookDestinationPolicy.ValidateUrl(request.RequestUri?.AbsoluteUri);
        // Never let a proxy, alternate host header or alternate connection authority bypass the policy.
        if (!string.IsNullOrEmpty(request.Headers.Host)
            || !endpoint.Host.Trim('[', ']').TrimEnd('.').Equals(WebhookDestinationPolicy.Host(uri), StringComparison.OrdinalIgnoreCase)
            || endpoint.Port != uri.Port)
            throw new WebhookDestinationException();

        var addresses = await policy.ResolvePublicAsync(uri, ct);
        Exception? last = null;
        foreach (var address in addresses)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                // The numeric endpoint is the checked snapshot: Socket does NOT resolve the hostname again.
                // Return a raw stream, retaining the original URI for normal TLS validation and SNI.
                return await connect(new IPEndPoint(address, endpoint.Port), ct);
            }
            catch (Exception ex) when (ex is SocketException or IOException)
            {
                last = ex;
            }
        }
        throw new HttpRequestException("No se pudo conectar con el destino público del webhook.", last);
    }

    private static async ValueTask<Stream> ConnectSocketAsync(IPEndPoint endpoint, CancellationToken ct)
    {
        var socket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(endpoint, ct);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
