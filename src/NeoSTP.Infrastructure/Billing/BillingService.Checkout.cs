using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Billing;
using NeoSTP.Application.Billing.Dtos;
using NeoSTP.Application.Common;
using NeoSTP.Domain.Core.Billing;

namespace NeoSTP.Infrastructure.Billing;

public sealed partial class BillingService
{
    private const string OpenCheckoutCode = "BILLING_CHECKOUT_PENDING";
    private const string OpenCheckoutMessage = "Hay un checkout pendiente o en conciliación. Resuélvalo antes de modificar la suscripción.";

    public async Task<Result<CheckoutSessionResult>> CreateCheckoutSessionAsync(CreateCheckoutRequest request, CancellationToken ct = default)
    {
        // Reservation/lease is committed in a separate scope, before the external effect.
        var reservation = await SingleAttemptAsync(() => ReserveCheckoutAsync(request, ct));
        if (reservation.IsFailure)
            return Result<CheckoutSessionResult>.Fail(reservation.Error!, reservation.ErrorCode);
        var (intent, dispatch) = reservation.Value!;
        if (!dispatch) return CheckoutResult(intent);

        // Resolve again against the snapshot. Never fallback to the legacy customer/checkout calls.
        var provider = P(intent.Provider);
        if (provider.ProviderName != intent.Provider || provider is not IBillingCheckoutProvider checkout)
        {
            await QuarantineCheckoutAsync(intent, "BILLING_CHECKOUT_UNAVAILABLE");
            return CheckoutUncertain(intent);
        }

        Result<ProviderCheckoutSession> response;
        try
        {
            response = await checkout.CreateCheckoutAsync(new ProviderCheckoutRequest(
                intent.CorrelationId, $"neostp:checkout:{intent.CorrelationId:N}", intent.EmpresaId,
                intent.ProviderAccountId, intent.BeneficiaryId, intent.ExternalPlanId,
                intent.ExternalCustomerId, intent.PlanName, intent.Amount, intent.Currency,
                intent.BillingInterval, intent.SuccessUrl, intent.CancelUrl), ct);
        }
        catch (Exception)
        {
            // Cancellation/timeout can occur after the provider accepted. No second POST is safe.
            await QuarantineCheckoutAsync(intent, "BILLING_RECONCILIATION_REQUIRED");
            return CheckoutUncertain(intent);
        }

        var session = response.Value;
        if (!response.IsSuccess || session is null || session.Provider != intent.Provider
            || session.ProviderAccountId != intent.ProviderAccountId
            || !ValidCheckoutIdentity(session.SessionId) || !IsHttpsCheckoutUrl(session.RedirectUrl)
            || session.ExpiresAt is DateTime expiry && expiry <= DateTime.UtcNow)
        {
            await QuarantineCheckoutAsync(intent, "BILLING_RECONCILIATION_REQUIRED");
            return CheckoutUncertain(intent);
        }

        // An ACK is a session identity, not a captured payment. Persist it before returning a URL.
        try
        {
            using var persistTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var saved = await PersistCheckoutAckAsync(intent, session, persistTimeout.Token);
            if (saved)
                return Result<CheckoutSessionResult>.Ok(new(session.SessionId, session.RedirectUrl,
                    intent.CorrelationId, BillingCheckoutStatuses.AwaitingPayment));
        }
        catch (Exception)
        {
            // The durable PROCESSING row still blocks a new operation if SQL is unavailable.
        }
        await QuarantineCheckoutAsync(intent, "BILLING_RECONCILIATION_REQUIRED");
        return CheckoutUncertain(intent);
    }

    private async Task<Result<(BillingCheckoutIntent Intent, bool Dispatch)>> ReserveCheckoutAsync(
        CreateCheckoutRequest request, CancellationToken ct)
    {
        if (!await CanAccessCompanyAsync(request.EmpresaId, manage: true, ct))
            return CheckoutReservationFailure("No tiene autorización para administrar esta empresa.", "BILLING_FORBIDDEN");
        await using var transaction = await BeginBillingMutationAsync(request.EmpresaId, ct);
        if (await HasCalendarAgreementAsync(request.EmpresaId, ct))
            return CheckoutReservationFailure(CalendarAgreementMessage, CalendarAgreementCode);
        if (await HasOpenCancellationAsync(request.EmpresaId, ct))
            return CheckoutReservationFailure(OpenCancellationMessage, OpenCancellationCode);
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            return CheckoutReservationFailure("La operación requiere una clave idempotente estable.", "IDEMPOTENCY_KEY_REQUIRED");
        if (request.IdempotencyKey.Length is < 8 or > 128
            || request.IdempotencyKey.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_' and not '.' and not ':'))
            return CheckoutReservationFailure("Clave idempotente inválida.", "IDEMPOTENCY_KEY_INVALID");

        // Only caller-owned identity belongs in the fingerprint. Server URLs/default provider
        // may change later, but a repeated original POST must recover its stored snapshot.
        var requestedMethod = request.Metodo?.Trim().ToUpperInvariant() ?? string.Empty;
        var keyHash = CheckoutHash(request.IdempotencyKey);
        var fingerprint = CheckoutHash(JsonSerializer.Serialize(new { request.PlanId, Method = requestedMethod }));
        var existing = await _db.BillingCheckoutIntents.AsNoTracking()
            .SingleOrDefaultAsync(x => x.EmpresaId == request.EmpresaId && x.IdempotencyKeyHash == keyHash, ct);
        if (existing is not null)
        {
            if (existing.RequestFingerprint != fingerprint)
                return CheckoutReservationFailure("La clave ya identifica otro checkout.", "IDEMPOTENCY_CONFLICT");
            // Preserve the original commercial snapshot even if the catalog/options changed.
            return Result<(BillingCheckoutIntent, bool)>.Ok((existing, false));
        }
        if (await HasOpenCheckoutAsync(request.EmpresaId, ct))
            return CheckoutReservationFailure(OpenCheckoutMessage, OpenCheckoutCode);
        if (await HasPendingTransferAsync(request.EmpresaId, ct))
            return CheckoutReservationFailure("Hay una transferencia pendiente de verificación.", "BILLING_TRANSFER_PENDING");

        var requestedProvider = string.IsNullOrWhiteSpace(request.Metodo) ? _options.Provider : request.Metodo;
        if (string.IsNullOrWhiteSpace(requestedProvider))
            return CheckoutReservationFailure("La pasarela no está configurada.", "BILLING_CHECKOUT_UNAVAILABLE");
        var options = _options.Checkout;
        var provider = P(requestedProvider);
        if (options is null || !options.Enabled || string.Equals(provider.ProviderName, "Mock", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(provider.ProviderName, requestedProvider, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(provider.ProviderName, options.Provider, StringComparison.OrdinalIgnoreCase)
            || provider is not IBillingCheckoutProvider capability || capability.ProviderName != provider.ProviderName
            || !ValidCheckoutIdentity(options.ProviderAccountId) || !ValidCheckoutIdentity(options.BeneficiaryId)
            || !IsHttpsCheckoutUrl(options.SuccessUrl) || !IsHttpsCheckoutUrl(options.CancelUrl))
            return CheckoutReservationFailure("La pasarela no tiene habilitado el contrato de checkout seguro.", "BILLING_CHECKOUT_UNAVAILABLE");
        if (!string.Equals(request.ReturnUrl, options.SuccessUrl, StringComparison.Ordinal))
            return CheckoutReservationFailure("La URL de retorno no coincide con la configurada en el servidor.", "BILLING_CHECKOUT_RETURN_URL_INVALID");

        var plan = await _db.Planes.AsNoTracking().SingleOrDefaultAsync(p => p.Id == request.PlanId && p.Activo, ct);
        if (plan is null) return CheckoutReservationFailure("Plan no encontrado.", "PLAN_NOT_FOUND");
        var mapping = await _db.BillingPlanProviderMappings.AsNoTracking()
            .SingleOrDefaultAsync(m => m.PlanId == plan.Id && m.Provider == provider.ProviderName && m.IsActive, ct);
        if (plan.PrecioMensual <= 0 || decimal.Round(plan.PrecioMensual, 2) != plan.PrecioMensual
            || plan.PrecioMensual > 9999999999999999.99m
            || string.IsNullOrEmpty(plan.MonedaCodigo) || plan.MonedaCodigo.Length != 3 || plan.MonedaCodigo.Any(c => c is < 'A' or > 'Z')
            || mapping is null || mapping.UnitAmount != plan.PrecioMensual || mapping.Currency != plan.MonedaCodigo
            || !ValidCheckoutIdentity(mapping.ExternalPlanId) || mapping.ExternalPlanId.StartsWith("mock", StringComparison.OrdinalIgnoreCase))
            return CheckoutReservationFailure("Precio, moneda o mapping del plan no válidos para la pasarela.", "BILLING_CHECKOUT_MAPPING_INVALID");

        var subscriptions = await ActiveSubscriptionQuery(request.EmpresaId).AsNoTracking().Take(2).ToListAsync(ct);
        var licenses = await _db.EmpresaPlanes.AsNoTracking()
            .Where(p => p.EmpresaId == request.EmpresaId && p.EstadoCodigo == "ACTIVO").Take(2).ToListAsync(ct);
        if (subscriptions.Count > 1 || licenses.Count > 1)
            return CheckoutReservationFailure("La suscripción o licencia requiere conciliación.", "BILLING_SUBSCRIPTION_AMBIGUOUS");
        var subscription = subscriptions.SingleOrDefault();
        var license = licenses.SingleOrDefault();
        if (subscription is not null && (subscription.Customer.Provider != provider.ProviderName
            || license is not null && license.PlanId != subscription.PlanId))
            return CheckoutReservationFailure("Proveedor, suscripción y licencia no coinciden.", "BILLING_SUBSCRIPTION_AMBIGUOUS");

        var now = DateTime.UtcNow;
        var intent = new BillingCheckoutIntent
        {
            CorrelationId = Guid.NewGuid(), EmpresaId = request.EmpresaId, PlanId = plan.Id,
            BillingCustomerId = subscription?.BillingCustomerId, BillingSubscriptionId = subscription?.Id,
            EmpresaPlanId = license?.Id, Provider = provider.ProviderName,
            IsProduction = provider.ProviderName == "Wompi" && _options.Wompi.IsProduction,
            ProviderAccountId = options.ProviderAccountId, BeneficiaryId = options.BeneficiaryId,
            IdempotencyKeyHash = keyHash, RequestFingerprint = fingerprint,
            PlanCode = plan.Codigo, PlanName = plan.Nombre, Amount = plan.PrecioMensual, Currency = plan.MonedaCodigo,
            ExternalPlanId = mapping.ExternalPlanId,
            // Legacy customer IDs are not account-scoped; never forward them to a different merchant.
            ExternalCustomerId = null, SuccessUrl = options.SuccessUrl, CancelUrl = options.CancelUrl,
            LeaseId = Guid.NewGuid().ToString("N"), LeaseExpiresAt = now.AddSeconds(Math.Clamp(options.LeaseSeconds, 30, 900)),
            Status = BillingCheckoutStatuses.Processing, CreatedAt = now, CreatedBy = _currentUser.UserId?.ToString(),
        };
        if (intent.Provider == "Wompi")
        {
            var policy = await BillingCheckoutPolicy.ValidateAsync(_db, intent, now, ct);
            if (policy.IsFailure) return CheckoutReservationFailure(policy.Error!, policy.ErrorCode!);
        }
        intent.CommercialSnapshotJson = await BillingCommercialSnapshot.CaptureAsync(_db, intent, ct);
        _db.BillingCheckoutIntents.Add(intent);
        await _db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return Result<(BillingCheckoutIntent, bool)>.Ok((intent, true));
    }

    private async Task<bool> PersistCheckoutAckAsync(BillingCheckoutIntent intent, ProviderCheckoutSession session, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        if (_db.Database.IsRelational())
            return await _db.BillingCheckoutIntents.Where(x => x.Id == intent.Id && x.LeaseId == intent.LeaseId
                && x.ProviderAcknowledgedAt == null && (x.Status == BillingCheckoutStatuses.Processing
                    || x.Status == BillingCheckoutStatuses.RequiresReconciliation))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, BillingCheckoutStatuses.AwaitingPayment)
                    .SetProperty(x => x.ExternalCheckoutId, session.SessionId).SetProperty(x => x.RedirectUrl, session.RedirectUrl)
                    .SetProperty(x => x.ExpiresAt, session.ExpiresAt).SetProperty(x => x.ProviderAcknowledgedAt, now)
                    .SetProperty(x => x.LastErrorCode, (string?)null).SetProperty(x => x.UpdatedAt, now), ct) == 1;

        var current = await _db.BillingCheckoutIntents.FindAsync([intent.Id], ct);
        if (current is null) return false;
        await _db.Entry(current).ReloadAsync(ct);
        if (current.LeaseId != intent.LeaseId || current.ProviderAcknowledgedAt is not null
            || current.Status is not (BillingCheckoutStatuses.Processing or BillingCheckoutStatuses.RequiresReconciliation)) return false;
        current.Status = BillingCheckoutStatuses.AwaitingPayment;
        current.ExternalCheckoutId = session.SessionId; current.RedirectUrl = session.RedirectUrl;
        current.ExpiresAt = session.ExpiresAt; current.ProviderAcknowledgedAt = now;
        current.LastErrorCode = null; current.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task QuarantineCheckoutAsync(BillingCheckoutIntent intent, string code)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            if (_db.Database.IsRelational())
                await _db.BillingCheckoutIntents.Where(x => x.Id == intent.Id && x.LeaseId == intent.LeaseId
                    && x.Status == BillingCheckoutStatuses.Processing && x.ProviderAcknowledgedAt == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, BillingCheckoutStatuses.RequiresReconciliation)
                        .SetProperty(x => x.LastErrorCode, code), timeout.Token);
            else
            {
                var current = await _db.BillingCheckoutIntents.FindAsync([intent.Id], timeout.Token);
                if (current is null) return;
                await _db.Entry(current).ReloadAsync(timeout.Token);
                if (current.LeaseId != intent.LeaseId || current.Status != BillingCheckoutStatuses.Processing
                    || current.ProviderAcknowledgedAt is not null) return;
                current.Status = BillingCheckoutStatuses.RequiresReconciliation; current.LastErrorCode = code;
                await _db.SaveChangesAsync(timeout.Token);
            }
        }
        catch (Exception) { /* PROCESSING remains durable and blocks redispatch after SQL failure. */ }
    }

    public async Task<Result<BillingCheckoutDto>> GetCheckoutAsync(int empresaId, Guid correlationId, CancellationToken ct = default)
    {
        if (!await CanAccessCompanyAsync(empresaId, manage: false, ct))
            return Result<BillingCheckoutDto>.Fail("No tiene autorización para consultar esta empresa.", "BILLING_FORBIDDEN");
        var intent = await _db.BillingCheckoutIntents.AsNoTracking()
            .SingleOrDefaultAsync(x => x.EmpresaId == empresaId && x.CorrelationId == correlationId, ct);
        if (intent is null) return Result<BillingCheckoutDto>.Fail("Checkout no encontrado.", "BILLING_CHECKOUT_NOT_FOUND");
        var status = CheckoutEffectiveStatus(intent);
        return Result<BillingCheckoutDto>.Ok(new(intent.CorrelationId, empresaId, intent.PlanId, intent.Provider,
            intent.Amount, intent.Currency, status, intent.ExternalCheckoutId,
            status == BillingCheckoutStatuses.AwaitingPayment ? intent.RedirectUrl : null, intent.ExpiresAt,
            status == BillingCheckoutStatuses.RequiresReconciliation ? "BILLING_RECONCILIATION_REQUIRED" : intent.LastErrorCode));
    }

    private Task<bool> HasOpenCheckoutAsync(int empresaId, CancellationToken ct)
        => _db.BillingCheckoutIntents.AsNoTracking().AnyAsync(x => x.EmpresaId == empresaId && x.Status != BillingCheckoutStatuses.Completed, ct);

    private static string CheckoutEffectiveStatus(BillingCheckoutIntent intent)
        => intent.Status == BillingCheckoutStatuses.Processing && intent.LeaseExpiresAt <= DateTime.UtcNow
            || intent.Status == BillingCheckoutStatuses.AwaitingPayment && intent.ExpiresAt <= DateTime.UtcNow
            ? BillingCheckoutStatuses.RequiresReconciliation : intent.Status;

    private static Result<CheckoutSessionResult> CheckoutResult(BillingCheckoutIntent intent)
    {
        var status = CheckoutEffectiveStatus(intent);
        if (status is BillingCheckoutStatuses.PaymentVerifiedSandbox or BillingCheckoutStatuses.Completed)
            return Result<CheckoutSessionResult>.Ok(new(intent.ExternalCheckoutId ?? string.Empty, string.Empty, intent.CorrelationId, status));
        if (status == BillingCheckoutStatuses.AwaitingPayment && intent.ProviderAcknowledgedAt is not null)
            return Result<CheckoutSessionResult>.Ok(new(intent.ExternalCheckoutId!, intent.RedirectUrl!, intent.CorrelationId, status));
        return Result<CheckoutSessionResult>.FailWithValue(new(intent.ExternalCheckoutId ?? string.Empty, string.Empty, intent.CorrelationId, status),
            "Consulte el estado de la misma operación; no se creará otra sesión.",
            status == BillingCheckoutStatuses.Processing ? OpenCheckoutCode : "BILLING_RECONCILIATION_REQUIRED");
    }

    private static Result<CheckoutSessionResult> CheckoutUncertain(BillingCheckoutIntent intent)
        => Result<CheckoutSessionResult>.FailWithValue(new(string.Empty, string.Empty, intent.CorrelationId,
            BillingCheckoutStatuses.RequiresReconciliation), "El checkout requiere conciliación. Conserve su referencia; no repita el pago.", "BILLING_RECONCILIATION_REQUIRED");
    private static Result<(BillingCheckoutIntent, bool)> CheckoutReservationFailure(string message, string code)
        => Result<(BillingCheckoutIntent, bool)>.Fail(message, code);
    private static string CheckoutHash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static bool ValidCheckoutIdentity(string? value)
        => !string.IsNullOrWhiteSpace(value) && value.Length <= 200 && value == value.Trim() && !value.Any(char.IsControl);
    private static bool IsHttpsCheckoutUrl(string? value)
        => value is { Length: <= 2000 } && Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo);
}
