using System.Data;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NeoSTP.Application.Common;
using NeoSTP.Application.Dte;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Dte.Certificacion;
using NeoSTP.Infrastructure.Billing;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Dte.Certificacion;

internal sealed record CertificationConsumptionRequest(Guid CampaignPublicId, int EmpresaId, string ExpectedNit,
    string TipoDteCodigo, string IdempotencyKey, string PayloadHash, string ScenarioReference, string Actor);
internal sealed record CertificationConsumptionResult(CertificationCampaignConsumption Consumption, bool Replayed);

/// <summary>
/// CERT-1 foundation only: no DI registration, host caller, transmission or monthly-quota exemption.
/// Stages a fresh draft and its consumption together; never SaveChanges, commit, activate or refund.
/// Future integration must use the DTE creation transaction/execution strategy and persist both atomically.
/// A replay is a read result, never authorization to sign or transmit an existing document.
/// </summary>
internal sealed class CertificationCampaignQuotaService(NeoStpDbContext db, TimeProvider? timeProvider = null)
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private static readonly string[] AllowedTypes = ["01", "03", "11", "14"];

    internal async Task<Result<CertificationConsumptionResult>> StageConsumptionAsync(
        CertificationConsumptionRequest request, DteDocumento document, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var payloadHash = request.PayloadHash ?? "";
        if (request.CampaignPublicId == Guid.Empty || request.EmpresaId <= 0
            || !Regex.IsMatch(request.ExpectedNit ?? "", "\\A[0-9]{14}\\z")
            || !AllowedTypes.Contains(request.TipoDteCodigo)
            || string.IsNullOrEmpty(request.IdempotencyKey) || DteIdempotency.ValidateKey(request.IdempotencyKey).IsFailure
            || !Regex.IsMatch(payloadHash, "\\A[0-9a-fA-F]{64}\\z")
            || string.IsNullOrWhiteSpace(request.ScenarioReference) || request.ScenarioReference.Length > 128
            || request.ScenarioReference.Any(char.IsControl)
            || string.IsNullOrWhiteSpace(request.Actor) || request.Actor.Length > 100)
            return Fail("CERT_CAMPAIGN_REQUEST_INVALID");
        if (document.Id != 0 || db.Entry(document).State != EntityState.Detached
            || document.EmpresaId != request.EmpresaId || document.TipoDteCodigo != request.TipoDteCodigo
            || document.AmbienteCodigo != "PRUEBAS" || document.EstadoCodigo != "BORRADOR"
            || document.EnviadoAt is not null || document.SelloRecibido is not null
            || document.Json is not null || !HasSafeNewGraph(document)
            || document.IdempotencyScope is not null || document.IdempotencyKeyHash is not null
            || document.IdempotencyRequestHash is not null || string.IsNullOrWhiteSpace(document.NumeroControl)
            || !Guid.TryParse(document.CodigoGeneracion, out var generation) || generation == Guid.Empty)
            return Fail("CERT_CAMPAIGN_DRAFT_REQUIRED");

        // Nonrelational provider is solely for isolated policy tests. Every relational caller must own a transaction.
        if (db.Database.IsRelational())
        {
            if (db.Database.CurrentTransaction is null || !db.Database.IsSqlServer()
                || db.Database.CurrentTransaction.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
                return Fail("CERT_CAMPAIGN_TRANSACTION_REQUIRED");
            var resource = $"NeoSTP:DTE-LIMIT:{request.EmpresaId}";
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                DECLARE @lockResult int;
                EXEC @lockResult = sys.sp_getapplock @Resource = {resource},
                    @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000;
                IF @lockResult < 0 THROW 50001, 'Certification allowance lock unavailable.', 1;
                """, ct);
        }
        var now = clock.GetUtcNow();
        var campaign = await db.CertificationCampaigns.AsNoTracking().Include(x => x.TypeBudgets)
            .SingleOrDefaultAsync(x => x.PublicId == request.CampaignPublicId && x.EmpresaId == request.EmpresaId, ct);
        if (campaign is null || campaign.ExpectedNit != request.ExpectedNit || campaign.AmbienteCodigo != "PRUEBAS")
            return Fail("CERT_CAMPAIGN_FORBIDDEN");
        var company = await db.Empresas.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.EmpresaId, ct);
        var config = await db.DteConfiguracion.AsNoTracking().SingleOrDefaultAsync(x => x.EmpresaId == request.EmpresaId, ct);
        if (company is null || NormalizeNit(company.Nit) != request.ExpectedNit || config?.AmbienteCodigo != "PRUEBAS")
            return Fail("CERT_CAMPAIGN_FORBIDDEN");

        var keyHash = DteIdempotency.HashKey($"{campaign.PublicId:N}:{request.IdempotencyKey}");
        // Does not change the existing request DTO or its persisted idempotency fingerprint.
        var fingerprint = DteIdempotency.HashKey($"{payloadHash.ToUpperInvariant()}|{request.TipoDteCodigo}|{request.ScenarioReference}");
        var previous = db.ChangeTracker.Entries<CertificationCampaignConsumption>()
            .Where(x => x.State == EntityState.Added).Select(x => x.Entity)
            .SingleOrDefault(x => x.CampaignId == campaign.Id && x.IdempotencyKeyHash == keyHash)
            ?? await db.CertificationCampaignConsumptions.AsNoTracking().Include(x => x.Document)
                .SingleOrDefaultAsync(x => x.CampaignId == campaign.Id && x.IdempotencyKeyHash == keyHash, ct);
        if (previous is not null)
        {
            if (previous.EmpresaId != request.EmpresaId || previous.TipoDteCodigo != request.TipoDteCodigo
                || previous.RequestHash != fingerprint || previous.ScenarioReference != request.ScenarioReference
                || previous.Document is null || previous.Document.EmpresaId != request.EmpresaId
                || previous.Document.AmbienteCodigo != "PRUEBAS" || previous.Document.TipoDteCodigo != request.TipoDteCodigo
                || previous.Document.IdempotencyScope != "CERT" || previous.Document.IdempotencyKeyHash != keyHash
                || previous.Document.IdempotencyRequestHash != fingerprint)
                return Fail("IDEMPOTENCY_CONFLICT");
            return Result<CertificationConsumptionResult>.Ok(new(previous, true));
        }
        if (campaign.Status != "ACTIVE" || now < campaign.StartsAtUtc || now >= campaign.ExpiresAtUtc)
            return Fail("CERT_CAMPAIGN_INACTIVE");
        if (!DteTypeAuthorization.Resolve(config.TiposDteAutorizadosCsv).Any(x => x.Codigo == request.TipoDteCodigo))
            return Fail("DTE_TIPO_NO_AUTORIZADO");
        if (campaign.StartsAtUtc.Offset != TimeSpan.Zero || campaign.ExpiresAtUtc.Offset != TimeSpan.Zero
            || campaign.ExpiresAtUtc <= campaign.StartsAtUtc || campaign.TotalBudget <= 0
            || string.IsNullOrWhiteSpace(campaign.MatrixReference) || campaign.TypeBudgets.Count == 0
            || campaign.TypeBudgets.Any(x => x.Budget <= 0 || !AllowedTypes.Contains(x.TipoDteCodigo))
            || campaign.TypeBudgets.Select(x => x.TipoDteCodigo).Distinct().Count() != campaign.TypeBudgets.Count
            || campaign.TypeBudgets.Sum(x => (long)x.Budget) != campaign.TotalBudget)
            return Fail("CERT_CAMPAIGN_INVALID");
        var typeBudget = campaign.TypeBudgets.SingleOrDefault(x => x.TipoDteCodigo == request.TipoDteCodigo);
        if (typeBudget is null) return Fail("CERT_CAMPAIGN_FORBIDDEN");
        if (company.EstadoCodigo != "ACTIVA") return Fail("LICENSE_INVALID");
        var licenses = await db.EmpresaPlanes.AsNoTracking().Where(x => x.EmpresaId == request.EmpresaId
            && x.EstadoCodigo == "ACTIVO" && x.FechaInicio <= now.UtcDateTime
            && (x.FechaFin == null || x.FechaFin > now.UtcDateTime)).Take(2).ToListAsync(ct);
        if (licenses.Count != 1) return Fail("LICENSE_INVALID");
        var rights = await BillingEntitlementReader.ReadAsync(db, licenses[0], ct);
        if (rights.IsFailure) return Result<CertificationConsumptionResult>.Fail(rights.Error!, rights.ErrorCode);
        var required = await db.Modulos.AsNoTracking().Where(x => x.Activo && (x.Codigo == "CORE" || x.Codigo == "NEODTE"))
            .Select(x => x.Id).ToListAsync(ct);
        if (required.Count != 2 || required.Any(x => !rights.Value!.ModuleIds.Contains(x)))
            return Fail("CERT_CAMPAIGN_MODULE_DISABLED");
        var enabled = await db.EmpresaModulos.AsNoTracking().Where(x => x.EmpresaId == request.EmpresaId && required.Contains(x.ModuloId)
            && x.Activo && x.FechaInactivacion == null).Select(x => x.ModuloId).ToListAsync(ct);
        if (enabled.Count != 2) return Fail("CERT_CAMPAIGN_MODULE_DISABLED");

        if (!await ReferencesBelongToTenantAsync(document, request.EmpresaId, ct))
            return Fail("CERT_CAMPAIGN_FORBIDDEN");
        var pending = db.ChangeTracker.Entries<CertificationCampaignConsumption>()
            .Where(x => x.State == EntityState.Added && x.Entity.CampaignId == campaign.Id).Select(x => x.Entity).ToList();
        var total = await db.CertificationCampaignConsumptions.CountAsync(x => x.CampaignId == campaign.Id, ct);
        var byType = await db.CertificationCampaignConsumptions.CountAsync(x => x.CampaignId == campaign.Id && x.TipoDteCodigo == request.TipoDteCodigo, ct);
        if ((long)total + pending.Count >= campaign.TotalBudget
            || (long)byType + pending.Count(x => x.TipoDteCodigo == request.TipoDteCodigo) >= typeBudget.Budget)
            return Fail("CERT_CAMPAIGN_EXHAUSTED");
        ct.ThrowIfCancellationRequested();
        document.IdempotencyScope = "CERT";
        document.IdempotencyKeyHash = keyHash;
        document.IdempotencyRequestHash = fingerprint;
        document.CreatedAt = now.UtcDateTime;
        document.CreatedBy = request.Actor;
        var consumption = new CertificationCampaignConsumption
        {
            CampaignId = campaign.Id, EmpresaId = request.EmpresaId, TipoDteCodigo = request.TipoDteCodigo,
            Document = document, IdempotencyKeyHash = keyHash, RequestHash = fingerprint,
            ScenarioReference = request.ScenarioReference, CreatedAt = now.UtcDateTime, CreatedBy = request.Actor,
        };
        db.DteDocumentos.Add(document);
        db.CertificationCampaignConsumptions.Add(consumption);
        return Result<CertificationConsumptionResult>.Ok(new(consumption, false));
    }

    // Business references must be FK-only. Add(document) must never traverse detached companies,
    // clients or products and accidentally insert them. Only new aggregate detail rows are allowed.
    private bool HasSafeNewGraph(DteDocumento document)
    {
        if (document.Empresa is not null || document.Cliente is not null || document.DocumentoRelacionado is not null
            || document.Detalles is null) return false;
        return document.Detalles.All(line => line is not null && line.Id == 0 && line.DocumentoId == 0
            && (line.Documento is null || ReferenceEquals(line.Documento, document))
            && line.Producto is null && db.Entry(line).State == EntityState.Detached);
    }

    private async Task<bool> ReferencesBelongToTenantAsync(DteDocumento document, int tenant, CancellationToken ct)
    {
        if (document.ClienteId is int client && !await db.Clientes.AsNoTracking().AnyAsync(x => x.Id == client && x.EmpresaId == tenant, ct)) return false;
        if (document.SucursalId is int branch && !await db.Sucursales.AsNoTracking().AnyAsync(x => x.Id == branch && x.EmpresaId == tenant, ct)) return false;
        if (document.PuntoVentaId is int point && (document.SucursalId is null
            || !await db.PuntosVenta.AsNoTracking().AnyAsync(x => x.Id == point && x.SucursalId == document.SucursalId && x.Sucursal.EmpresaId == tenant, ct))) return false;
        if (document.DocumentoRelacionadoId is int related && !await db.DteDocumentos.AsNoTracking()
            .AnyAsync(x => x.Id == related && x.EmpresaId == tenant && x.AmbienteCodigo == "PRUEBAS", ct)) return false;
        var products = document.Detalles.Where(x => x.ProductoId.HasValue).Select(x => x.ProductoId!.Value).Distinct().ToArray();
        return products.Length == 0 || await db.Productos.AsNoTracking().CountAsync(x => products.Contains(x.Id) && x.EmpresaId == tenant, ct) == products.Length;
    }
    private static string NormalizeNit(string? value) => (value ?? "").Replace("-", "").Replace(" ", "");
    private static Result<CertificationConsumptionResult> Fail(string code)
        => Result<CertificationConsumptionResult>.Fail("La reserva de certificación requiere condiciones válidas y verificadas.", code);
}
