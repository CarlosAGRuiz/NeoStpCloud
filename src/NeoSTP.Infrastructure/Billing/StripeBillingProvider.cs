using Microsoft.Extensions.Options;
using NeoSTP.Application.Billing;
using NeoSTP.Application.Billing.Dtos;
using NeoSTP.Application.Common;
using Stripe;
using Stripe.Checkout;

namespace NeoSTP.Infrastructure.Billing;

/// <summary>
/// Proveedor de pagos Stripe usando Stripe.net v47.
/// </summary>
public sealed class StripeBillingProvider : IPaymentProvider
{
    private readonly StripeOptions _opts;

    public StripeBillingProvider(IOptions<BillingOptions> options)
    {
        _opts = options.Value.Stripe;
        StripeConfiguration.ApiKey = _opts.SecretKey;
    }

    public string ProviderName => "Stripe";

    public async Task<Result<string>> CreateCustomerAsync(string email, int empresaId, CancellationToken ct = default)
    {
        try
        {
            var service = new CustomerService();
            var customer = await service.CreateAsync(new CustomerCreateOptions
            {
                Email = email,
                Metadata = new Dictionary<string, string> { ["empresaId"] = empresaId.ToString() },
            }, cancellationToken: ct);
            return Result<string>.Ok(customer.Id);
        }
        catch (StripeException ex)
        {
            return Result<string>.Fail(ex.Message, ex.StripeError?.Code);
        }
    }

    public async Task<Result<CheckoutSessionResult>> CreateCheckoutSessionAsync(
        string customerId, string externalPlanId, string successUrl, string cancelUrl, CancellationToken ct = default)
    {
        try
        {
            var service = new SessionService();
            var session = await service.CreateAsync(new SessionCreateOptions
            {
                Customer = customerId,
                Mode = "subscription",
                LineItems = new List<SessionLineItemOptions>
                {
                    new() { Price = externalPlanId, Quantity = 1 },
                },
                SuccessUrl = successUrl + "?session_id={CHECKOUT_SESSION_ID}",
                CancelUrl = cancelUrl,
            }, cancellationToken: ct);
            return Result<CheckoutSessionResult>.Ok(new CheckoutSessionResult(session.Id, session.Url));
        }
        catch (StripeException ex)
        {
            return Result<CheckoutSessionResult>.Fail(ex.Message, ex.StripeError?.Code);
        }
    }

    public async Task<Result<BillingPortalResult>> CreatePortalSessionAsync(string customerId, string returnUrl, CancellationToken ct = default)
    {
        try
        {
            var service = new Stripe.BillingPortal.SessionService();
            var session = await service.CreateAsync(new Stripe.BillingPortal.SessionCreateOptions
            {
                Customer = customerId,
                ReturnUrl = returnUrl,
            }, cancellationToken: ct);
            return Result<BillingPortalResult>.Ok(new BillingPortalResult(session.Url));
        }
        catch (StripeException ex)
        {
            return Result<BillingPortalResult>.Fail(ex.Message, ex.StripeError?.Code);
        }
    }

    public async Task<Result<string>> ChangePlanAsync(string externalSubscriptionId, string newExternalPlanId, CancellationToken ct = default)
    {
        try
        {
            var subService = new SubscriptionService();
            var sub = await subService.GetAsync(externalSubscriptionId, cancellationToken: ct);

            var itemService = new SubscriptionItemService();
            await itemService.UpdateAsync(sub.Items.Data[0].Id, new SubscriptionItemUpdateOptions
            {
                Price = newExternalPlanId,
            }, cancellationToken: ct);

            return Result<string>.Ok(externalSubscriptionId);
        }
        catch (StripeException ex)
        {
            return Result<string>.Fail(ex.Message, ex.StripeError?.Code);
        }
    }

    public async Task<Result> CancelSubscriptionAsync(string externalSubscriptionId, bool atPeriodEnd, CancellationToken ct = default)
    {
        try
        {
            var service = new SubscriptionService();
            if (atPeriodEnd)
            {
                await service.UpdateAsync(externalSubscriptionId, new SubscriptionUpdateOptions
                {
                    CancelAtPeriodEnd = true,
                }, cancellationToken: ct);
            }
            else
            {
                await service.CancelAsync(externalSubscriptionId, cancellationToken: ct);
            }
            return Result.Ok();
        }
        catch (StripeException ex)
        {
            return Result.Fail(ex.Message, ex.StripeError?.Code);
        }
    }
}
