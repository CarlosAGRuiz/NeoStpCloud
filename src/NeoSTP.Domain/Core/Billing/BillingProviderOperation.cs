using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Licenciamiento;

namespace NeoSTP.Domain.Core.Billing;

/// <summary>
/// Journal durable de una operación solicitada a un proveedor de billing.
/// Toda correlación parte de <see cref="EmpresaId"/> y de una clave idempotente estable.
/// </summary>
public class BillingProviderOperation : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    public int? BillingSubscriptionId { get; set; }
    public BillingSubscription? BillingSubscription { get; set; }

    public int? EmpresaPlanId { get; set; }
    public EmpresaPlan? EmpresaPlan { get; set; }

    /// <summary>
    /// Plan que tenía la suscripción al confirmar la intención. Evita que una
    /// cancelación antigua alcance un plan adquirido mientras se procesaba.
    /// </summary>
    public int PlanId { get; set; }
    public Plan Plan { get; set; } = null!;

    public string Provider { get; set; } = string.Empty;
    public string OperationType { get; set; } = BillingProviderOperationTypes.CancelSubscription;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string? ExternalResourceId { get; set; }
    public bool CancelAtPeriodEnd { get; set; }

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AccessEndsAt { get; set; }
    public string Status { get; set; } = BillingProviderOperationStatuses.Pending;
    public int Attempts { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public string? LeaseId { get; set; }
    public DateTime? LeaseExpiresAt { get; set; }
    public DateTime? ProviderConfirmedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? LastErrorCode { get; set; }
    public string? LastError { get; set; }

    public byte[] RowVersion { get; set; } = [];
}

public static class BillingProviderOperationTypes
{
    public const string CancelSubscription = "CANCEL_SUBSCRIPTION";
}

public static class BillingProviderOperationStatuses
{
    public const string Pending = "PENDING";
    public const string Processing = "PROCESSING";
    public const string Completed = "COMPLETED";
    public const string RequiresReconciliation = "REQUIRES_RECONCILIATION";
}
