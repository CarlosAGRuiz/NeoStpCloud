using Microsoft.Extensions.Options;
using NeoSTP.Application.Billing;

namespace NeoSTP.Infrastructure.Billing;

/// <summary>Registry de proveedores de pago indexados por <see cref="IPaymentProvider.ProviderName"/>.</summary>
public sealed class PaymentProviderResolver : IPaymentProviderResolver
{
    private readonly Dictionary<string, IPaymentProvider> _byName;
    private readonly BillingOptions _opts;

    public PaymentProviderResolver(IEnumerable<IPaymentProvider> providers, IOptions<BillingOptions> options)
    {
        _opts = options.Value;
        _byName = providers.ToDictionary(p => p.ProviderName, StringComparer.OrdinalIgnoreCase);
    }

    public string DefaultProvider => _opts.Provider;

    public IReadOnlyList<string> Disponibles => _byName.Keys.OrderBy(k => k).ToList();

    public IPaymentProvider Resolve(string? metodo = null)
    {
        var name = string.IsNullOrWhiteSpace(metodo) ? _opts.Provider : metodo;
        if (name is not null && _byName.TryGetValue(name, out var p))
            return p;
        if (_byName.TryGetValue(_opts.Provider, out var def))
            return def;
        return _byName.Values.First(); // Mock siempre está registrado
    }
}
