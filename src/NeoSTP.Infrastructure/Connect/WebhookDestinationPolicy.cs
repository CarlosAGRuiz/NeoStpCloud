using System.Net;
using System.Net.Sockets;

namespace NeoSTP.Infrastructure.Connect;

/// <summary>Public HTTPS destinations only. Registration is a preflight, never a DNS authorization cache.</summary>
public sealed class WebhookDestinationPolicy
{
    public const string BlockedMessage = "Destino de webhook bloqueado: use HTTPS y una dirección pública, sin credenciales en la URL.";
    private readonly Func<string, CancellationToken, Task<IPAddress[]>> _resolve;

    public WebhookDestinationPolicy() : this((host, ct) => Dns.GetHostAddressesAsync(host, ct)) { }

    internal WebhookDestinationPolicy(Func<string, CancellationToken, Task<IPAddress[]>> resolve)
        => _resolve = resolve;

    public static Uri ValidateUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || url.Any(char.IsControl)
            || url.Contains('\\')
            || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Fragment) || uri.Port < 1)
            throw new WebhookDestinationException();

        var host = Host(uri);
        if (host.Contains('%')) // IPv6 zone identifiers must not select a local interface.
            throw new WebhookDestinationException();
        if (IPAddress.TryParse(host, out var ip))
        {
            if (!IsPublicAddress(ip)) throw new WebhookDestinationException();
        }
        else if (Uri.CheckHostName(host) != UriHostNameType.Dns || !host.Contains('.')
            || host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || new[] { ".localhost", ".local", ".internal", ".home.arpa" }
                .Any(suffix => host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)))
            throw new WebhookDestinationException();

        return uri;
    }

    internal static string Host(Uri uri) => uri.IdnHost.Trim('[', ']').TrimEnd('.');

    public async Task<IPAddress[]> ResolvePublicAsync(Uri uri, CancellationToken ct)
    {
        ValidateUrl(uri.AbsoluteUri);
        ct.ThrowIfCancellationRequested();
        var host = Host(uri);
        var addresses = IPAddress.TryParse(host, out var literal)
            ? [literal]
            : await _resolve(host, ct);
        // Fail closed for mixed A/AAAA records; no fallback can select a private address.
        if (addresses.Length == 0 || addresses.Any(ip => !IsPublicAddress(ip)))
            throw new WebhookDestinationException();
        return addresses.Select(ip => ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip).Distinct().ToArray();
    }

    public static bool IsPublicAddress(IPAddress address)
    {
        if (address.AddressFamily == AddressFamily.InterNetworkV6 && address.ScopeId != 0) return false;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        var b = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            // IANA special-purpose registry, plus multicast/reserved. Conservative protocol /24 exclusions.
            return !(b[0] is 0 or 10 or 127 || b[0] >= 224
                || (b[0] == 100 && b[1] is >= 64 and <= 127)
                || (b[0] == 169 && b[1] == 254)
                || (b[0] == 172 && b[1] is >= 16 and <= 31)
                || (b[0] == 192 && b[1] == 168)
                || (b[0] == 192 && b[1] == 0 && b[2] is 0 or 2)
                || (b[0] == 192 && b[1] == 88 && b[2] == 99)
                || (b[0] == 198 && b[1] is 18 or 19)
                || (b[0] == 198 && b[1] == 51 && b[2] == 100)
                || (b[0] == 203 && b[1] == 0 && b[2] == 113));
        }
        // Only native global unicast 2000::/3. Excludes mapped/NAT64, ULA, link/site local,
        // multicast, unspecified and reserved ranges; disallow scoped global literals as well.
        if (address.AddressFamily != AddressFamily.InterNetworkV6 || address.ScopeId != 0
            || (b[0] & 0xe0) != 0x20) return false;
        return !(b[0] == 0x20 && b[1] == 0x01 && b[2] < 2 // 2001::/23: special protocols/Teredo
            || b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x0d && b[3] == 0xb8 // documentation
            || b[0] == 0x20 && b[1] == 0x02 // 6to4 may embed private IPv4
            || b[0] == 0x3f && b[1] == 0xff && (b[2] & 0xf0) == 0); // 3fff::/20 documentation
    }

    public static bool IsBlocked(Exception error)
    {
        for (Exception? current = error; current is not null; current = current.InnerException)
            if (current is WebhookDestinationException) return true;
        return false;
    }
}

public sealed class WebhookDestinationException : HttpRequestException
{
    public WebhookDestinationException() : base(WebhookDestinationPolicy.BlockedMessage) { }
}
