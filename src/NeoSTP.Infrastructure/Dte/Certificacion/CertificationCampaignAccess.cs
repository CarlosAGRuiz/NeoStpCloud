using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Common;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Dte.Certificacion;
using NeoSTP.Infrastructure.Billing;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Dte.Certificacion;

/// <summary>Recognizes committed campaign evidence; a CERT string alone grants no exemption or fiscal access.</summary>
internal static class CertificationCampaignAccess
{
    private static readonly string[] Types = ["01", "03", "11", "14"];

    internal static async Task<int> CountCommercialDocumentsAsync(NeoStpDbContext db, int tenant, DateTime monthStart, CancellationToken ct)
    {
        var evidence = await CoherentAsync(db, tenant, monthStart, null, ct);
        var excludedIds = evidence.Select(c => c.DteDocumentoId).ToArray();
        return await db.DteDocumentos.CountAsync(d => d.EmpresaId == tenant && d.CreatedAt >= monthStart
            && !excludedIds.Contains(d.Id), ct);
    }

    internal static async Task<Result> ValidateAsync(NeoStpDbContext db, int tenant, DteDocumento document,
        CancellationToken ct, TimeProvider? clock = null)
    {
        var hasConsumption = await db.CertificationCampaignConsumptions.AsNoTracking()
            .AnyAsync(c => c.DteDocumentoId == document.Id, ct);
        if (document.IdempotencyScope != "CERT" && !hasConsumption) return Result.Ok();
        if (document.EmpresaId != tenant || document.Id <= 0) return Fail("CERT_CAMPAIGN_FORBIDDEN");
        var evidence = await CoherentAsync(db, tenant, null, document.Id, ct);
        if (evidence.Count != 1) return Fail("CERT_CAMPAIGN_FORBIDDEN");
        var claim = evidence[0];
        // Compare the pending aggregate as well: changing scope, type or hashes cannot borrow an older valid row.
        if (!MatchesDocument(claim, document)) return Fail("CERT_CAMPAIGN_FORBIDDEN");
        var campaign = claim.TypeBudget.Campaign;
        var now = (clock ?? TimeProvider.System).GetUtcNow();
        if (campaign.Status != "ACTIVE" || now < campaign.StartsAtUtc || now >= campaign.ExpiresAtUtc)
            return Fail("CERT_CAMPAIGN_INACTIVE");
        var config = await db.DteConfiguracion.AsNoTracking().SingleOrDefaultAsync(c => c.EmpresaId == tenant, ct);
        if (config?.AmbienteCodigo != "PRUEBAS") return Fail("CERT_CAMPAIGN_FORBIDDEN");
        if (!DteTypeAuthorization.Resolve(config.TiposDteAutorizadosCsv).Any(t => t.Codigo == document.TipoDteCodigo))
            return Fail("DTE_TIPO_NO_AUTORIZADO");
        if (campaign.Empresa.EstadoCodigo != "ACTIVA") return Fail("LICENSE_INVALID");
        var licenses = await db.EmpresaPlanes.AsNoTracking().Where(l => l.EmpresaId == tenant && l.EstadoCodigo == "ACTIVO"
            && l.FechaInicio <= now.UtcDateTime && (l.FechaFin == null || l.FechaFin > now.UtcDateTime)).Take(2).ToListAsync(ct);
        if (licenses.Count != 1) return Fail("LICENSE_INVALID");
        var rights = await BillingEntitlementReader.ReadAsync(db, licenses[0], ct);
        if (rights.IsFailure) return Result.Fail(rights.Error!, rights.ErrorCode);
        var required = await db.Modulos.AsNoTracking().Where(m => m.Activo && (m.Codigo == "CORE" || m.Codigo == "NEODTE"))
            .Select(m => m.Id).ToListAsync(ct);
        if (required.Count != 2 || required.Any(id => !rights.Value!.ModuleIds.Contains(id))) return Fail("CERT_CAMPAIGN_MODULE_DISABLED");
        var enabled = await db.EmpresaModulos.AsNoTracking().CountAsync(m => m.EmpresaId == tenant && required.Contains(m.ModuloId)
            && m.Activo && m.FechaInactivacion == null, ct);
        return enabled == 2 ? Result.Ok() : Fail("CERT_CAMPAIGN_MODULE_DISABLED");
    }

    private static async Task<List<CertificationCampaignConsumption>> CoherentAsync(NeoStpDbContext db, int tenant,
        DateTime? monthStart, int? documentId, CancellationToken ct)
    {
        var query = db.CertificationCampaignConsumptions.AsNoTracking()
            .Where(c => c.EmpresaId == tenant && c.Document.EmpresaId == tenant);
        if (monthStart.HasValue) query = query.Where(c => c.Document.CreatedAt >= monthStart.Value);
        if (documentId.HasValue) query = query.Where(c => c.DteDocumentoId == documentId.Value);
        var claims = await query.Include(c => c.Document)
            .Include(c => c.TypeBudget).ThenInclude(b => b.Campaign).ThenInclude(c => c.Empresa).ToListAsync(ct);
        if (claims.Count == 0) return [];
        var campaignIds = claims.Select(c => c.CampaignId).Distinct().ToArray();
        var budgets = await db.CertificationCampaignTypeBudgets.AsNoTracking().Where(b => campaignIds.Contains(b.CampaignId)).ToListAsync(ct);
        foreach (var claim in claims)
            if (claim.TypeBudget?.Campaign is { } campaign)
                campaign.TypeBudgets = budgets.Where(b => b.CampaignId == campaign.Id).ToList();
        var counts = await db.CertificationCampaignConsumptions.AsNoTracking().Where(c => campaignIds.Contains(c.CampaignId))
            .GroupBy(c => new { c.CampaignId, c.TipoDteCodigo })
            .Select(g => new { g.Key.CampaignId, g.Key.TipoDteCodigo, Count = g.Count() }).ToListAsync(ct);
        return claims.Where(c => IsCoherent(c, tenant)
            && claims.Count(other => other.DteDocumentoId == c.DteDocumentoId) == 1
            && counts.Where(x => x.CampaignId == c.CampaignId).Sum(x => (long)x.Count) <= c.TypeBudget.Campaign.TotalBudget
            && counts.Where(x => x.CampaignId == c.CampaignId).All(x => c.TypeBudget.Campaign.TypeBudgets
                .Any(b => b.TipoDteCodigo == x.TipoDteCodigo && x.Count <= b.Budget))).ToList();
    }

    private static bool IsCoherent(CertificationCampaignConsumption claim, int tenant)
    {
        var campaign = claim.TypeBudget?.Campaign;
        if (campaign is null || campaign.Empresa is null || claim.Document is null || claim.PublicId == Guid.Empty
            || campaign.PublicId == Guid.Empty || claim.EmpresaId != tenant || campaign.EmpresaId != tenant
            || campaign.Empresa.Id != tenant || campaign.AmbienteCodigo != "PRUEBAS"
            || campaign.Status is not ("ACTIVE" or "CLOSED" or "REVOKED")
            || !Regex.IsMatch(campaign.ExpectedNit ?? "", "\\A[0-9]{14}\\z")
            || campaign.ExpectedNit != (campaign.Empresa.Nit ?? "").Replace("-", "").Replace(" ", "")
            || campaign.StartsAtUtc.Offset != TimeSpan.Zero || campaign.ExpiresAtUtc.Offset != TimeSpan.Zero
            || campaign.ExpiresAtUtc <= campaign.StartsAtUtc || campaign.TotalBudget <= 0
            || string.IsNullOrWhiteSpace(campaign.MatrixReference)
            || campaign.TypeBudgets.Count == 0 || campaign.TypeBudgets.Any(b => b.CampaignId != campaign.Id || b.Budget <= 0 || !Types.Contains(b.TipoDteCodigo))
            || campaign.TypeBudgets.Select(b => b.TipoDteCodigo).Distinct().Count() != campaign.TypeBudgets.Count
            || campaign.TypeBudgets.Sum(b => (long)b.Budget) != campaign.TotalBudget
            || claim.CampaignId != campaign.Id || claim.TypeBudget!.CampaignId != campaign.Id
            || claim.TypeBudget.TipoDteCodigo != claim.TipoDteCodigo || !Types.Contains(claim.TipoDteCodigo)
            || claim.CreatedAt != claim.Document.CreatedAt || claim.CreatedAt < campaign.StartsAtUtc.UtcDateTime
            || claim.CreatedAt >= campaign.ExpiresAtUtc.UtcDateTime
            || !Regex.IsMatch(claim.IdempotencyKeyHash ?? "", "\\A[0-9A-F]{64}\\z")
            || !Regex.IsMatch(claim.RequestHash ?? "", "\\A[0-9A-F]{64}\\z")
            || string.IsNullOrWhiteSpace(claim.ScenarioReference) || claim.ScenarioReference.Length > 128
            || claim.ScenarioReference.Any(char.IsControl)) return false;
        return MatchesDocument(claim, claim.Document);
    }

    private static bool MatchesDocument(CertificationCampaignConsumption claim, DteDocumento document)
        => claim.DteDocumentoId == document.Id && claim.EmpresaId == document.EmpresaId
            && document.AmbienteCodigo == "PRUEBAS" && document.TipoDteCodigo == claim.TipoDteCodigo
            && document.IdempotencyScope == "CERT" && document.IdempotencyKeyHash == claim.IdempotencyKeyHash
            && document.IdempotencyRequestHash == claim.RequestHash && document.CreatedAt == claim.CreatedAt;

    private static Result Fail(string code) => Result.Fail("El documento requiere una campaña de certificación válida y vigente.", code);
}
