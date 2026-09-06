using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Domain.Core.Dte.Certificacion;

/// <summary>Operator-provisioned finite test allowance. No seed or runtime caller activates campaigns.</summary>
public sealed class CertificationCampaign : AuditableEntity
{
    public Guid PublicId { get; set; } = Guid.NewGuid();
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;
    public string ExpectedNit { get; set; } = string.Empty;
    public string AmbienteCodigo { get; set; } = "PRUEBAS";
    public string Status { get; set; } = "PREPARED";
    public DateTimeOffset StartsAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public int TotalBudget { get; set; }
    public string MatrixReference { get; set; } = string.Empty;
    public byte[] RowVersion { get; set; } = [];
    public ICollection<CertificationCampaignTypeBudget> TypeBudgets { get; set; } = new List<CertificationCampaignTypeBudget>();
}

public sealed class CertificationCampaignTypeBudget
{
    public int CampaignId { get; set; }
    public CertificationCampaign Campaign { get; set; } = null!;
    public string TipoDteCodigo { get; set; } = string.Empty;
    public int Budget { get; set; }
}

/// <summary>A committed draft spends one allowance even if later fiscal processing fails. Never refunded automatically.</summary>
public sealed class CertificationCampaignConsumption : AuditableEntity
{
    public Guid PublicId { get; set; } = Guid.NewGuid();
    public int CampaignId { get; set; }
    public string TipoDteCodigo { get; set; } = string.Empty;
    public CertificationCampaignTypeBudget TypeBudget { get; set; } = null!;
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;
    public int DteDocumentoId { get; set; }
    public DteDocumento Document { get; set; } = null!;
    public string IdempotencyKeyHash { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public string ScenarioReference { get; set; } = string.Empty;
}
