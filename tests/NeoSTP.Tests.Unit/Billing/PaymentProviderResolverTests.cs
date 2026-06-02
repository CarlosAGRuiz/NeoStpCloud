using FluentAssertions;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Billing;
using NeoSTP.Application.Billing.Dtos;
using NeoSTP.Application.Common;
using NeoSTP.Infrastructure.Billing;
using Xunit;

namespace NeoSTP.Tests.Unit.Billing;

/// <summary>
/// Pagos LATAM PL.1 — resolución de proveedor por método de pago, con fallback al
/// proveedor por defecto y, en último caso, al primero registrado.
/// </summary>
public class PaymentProviderResolverTests
{
    private sealed class FakeProvider : IPaymentProvider
    {
        public FakeProvider(string name) => ProviderName = name;
        public string ProviderName { get; }
        public Task<Result<string>> CreateCustomerAsync(string email, int empresaId, CancellationToken ct = default) => Task.FromResult(Result<string>.Ok("c"));
        public Task<Result<CheckoutSessionResult>> CreateCheckoutSessionAsync(string c, string p, string s, string ca, CancellationToken ct = default) => Task.FromResult(Result<CheckoutSessionResult>.Ok(new("s", s)));
        public Task<Result<BillingPortalResult>> CreatePortalSessionAsync(string c, string r, CancellationToken ct = default) => Task.FromResult(Result<BillingPortalResult>.Ok(new(r)));
        public Task<Result<string>> ChangePlanAsync(string s, string p, CancellationToken ct = default) => Task.FromResult(Result<string>.Ok(s));
        public Task<Result> CancelSubscriptionAsync(string s, bool atEnd, CancellationToken ct = default) => Task.FromResult(Result.Ok());
    }

    private static PaymentProviderResolver Build(string defaultProvider, params string[] names)
    {
        var providers = names.Select(n => (IPaymentProvider)new FakeProvider(n)).ToList();
        var opts = Options.Create(new BillingOptions { Provider = defaultProvider });
        return new PaymentProviderResolver(providers, opts);
    }

    [Fact]
    public void Resolve_PorNombreExacto_DevuelveProveedor()
    {
        var r = Build("Mock", "Mock", "Wompi", "PayPal");
        r.Resolve("Wompi").ProviderName.Should().Be("Wompi");
        r.Resolve("paypal").ProviderName.Should().Be("PayPal"); // case-insensitive
    }

    [Fact]
    public void Resolve_Null_CaeAlDefault()
    {
        var r = Build("Wompi", "Mock", "Wompi");
        r.Resolve(null).ProviderName.Should().Be("Wompi");
    }

    [Fact]
    public void Resolve_Desconocido_CaeAlDefault()
    {
        var r = Build("Stripe", "Mock", "Stripe");
        r.Resolve("NoExiste").ProviderName.Should().Be("Stripe");
    }

    [Fact]
    public void Resolve_DefaultTampocoExiste_CaeAlPrimero()
    {
        var r = Build("ProveedorInexistente", "Mock", "Wompi");
        r.Resolve("OtroInexistente").ProviderName.Should().Be("Mock");
    }

    [Fact]
    public void Disponibles_ListaTodosLosMetodos()
    {
        var r = Build("Mock", "Mock", "Wompi", "PayPal", "Transferencia");
        r.Disponibles.Should().BeEquivalentTo(new[] { "Mock", "Wompi", "PayPal", "Transferencia" });
        r.DefaultProvider.Should().Be("Mock");
    }
}
