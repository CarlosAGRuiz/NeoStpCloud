using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Comunicaciones;
using NeoSTP.Application.Connect;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Application.Licenciamiento;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Dte.Certificacion;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Licenciamiento;
using NeoSTP.Infrastructure.Dte;
using NeoSTP.Infrastructure.Dte.Certificacion;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;

namespace NeoSTP.Tests.Unit.Dte.Certificacion;

public class CertificationCampaignAccessTests
{
    [Fact]
    public async Task ActiveCoherentCampaignPermitsFiscalPreparationButNeverAnotherTenantOrMutatedPendingDocument()
    {
        using var f = new Fixture(); var claim = await f.Stage();
        (await CertificationCampaignAccess.ValidateAsync(f.Db, 23, claim.Document, default)).IsSuccess.Should().BeTrue();
        (await CertificationCampaignAccess.ValidateAsync(f.Db, 99, claim.Document, default)).ErrorCode.Should().Be("CERT_CAMPAIGN_FORBIDDEN");
        claim.Document.IdempotencyRequestHash = new string('F', 64);
        (await CertificationCampaignAccess.ValidateAsync(f.Db, 23, claim.Document, default)).ErrorCode.Should().Be("CERT_CAMPAIGN_FORBIDDEN");
    }

    [Fact]
    public async Task OnlyCommittedCoherentCampaignConsumptionIsExcludedAndOrdinaryNewDocumentStillNeedsQuota()
    {
        using var f = new Fixture();
        await f.Stage();
        (await f.Count()).Should().Be(0);
        (await new LicenciaGuardService(f.Db).ValidarLimiteAsync(23, RecursoLimitado.DteMensual)).IsSuccess.Should().BeTrue();
        f.Db.DteDocumentos.Add(f.NewDoc()); await f.Db.SaveChangesAsync();
        (await f.Count()).Should().Be(1);
        var result = await f.Service.CreateBorradorAsync(23, DteIdempotencyTests.Request(), "test");
        result.ErrorCode.Should().Be("LIMIT_EXCEEDED");
        (await f.Db.DteDocumentos.CountAsync()).Should().Be(2);
        f.External.ReceivedCalls().Should().BeEmpty();
    }

    [Theory]
    [InlineData("PRODUCCION", null)]
    [InlineData("PRUEBAS", null)]
    [InlineData("PRUEBAS", "CERT")]
    public async Task OrdinaryOrForgedScopeDocumentsAlwaysConsumeCommercialQuota(string environment, string? scope)
    {
        using var f = new Fixture();
        var doc = f.NewDoc(); doc.AmbienteCodigo = environment; doc.IdempotencyScope = scope;
        f.Db.DteDocumentos.Add(doc); await f.Db.SaveChangesAsync();
        (await f.Count()).Should().Be(1);
        if (scope == "CERT")
            (await CertificationCampaignAccess.ValidateAsync(f.Db, 23, doc, default)).ErrorCode.Should().Be("CERT_CAMPAIGN_FORBIDDEN");
    }

    [Theory]
    [InlineData("document-production")]
    [InlineData("document-type")]
    [InlineData("document-scope")]
    [InlineData("document-key")]
    [InlineData("document-hash")]
    [InlineData("ledger-tenant")]
    [InlineData("campaign-tenant")]
    [InlineData("campaign-nit")]
    [InlineData("campaign-production")]
    [InlineData("campaign-prepared")]
    [InlineData("campaign-zero-budget")]
    [InlineData("campaign-budget-drift")]
    [InlineData("campaign-matrix")]
    [InlineData("outside-window")]
    [InlineData("ledger-date")]
    [InlineData("malformed-matching-hashes")]
    public async Task ForgedOrIncoherentLedgerNeverReducesCommercialCount(string corruption)
    {
        using var f = new Fixture();
        var claim = await f.Stage(); var doc = claim.Document;
        switch (corruption)
        {
            case "document-production": doc.AmbienteCodigo = "PRODUCCION"; break;
            case "document-type": doc.TipoDteCodigo = "03"; break;
            case "document-scope": doc.IdempotencyScope = "DTE"; break;
            case "document-key": doc.IdempotencyKeyHash = new string('B', 64); break;
            case "document-hash": doc.IdempotencyRequestHash = new string('B', 64); break;
            case "ledger-tenant": claim.EmpresaId = 99; break;
            case "campaign-tenant": f.Campaign.EmpresaId = 99; break;
            case "campaign-nit": f.Campaign.ExpectedNit = "00000000000099"; break;
            case "campaign-production": f.Campaign.AmbienteCodigo = "PRODUCCION"; break;
            case "campaign-prepared": f.Campaign.Status = "PREPARED"; break;
            case "campaign-zero-budget": f.Campaign.TotalBudget = 0; break;
            case "campaign-budget-drift": f.Campaign.TotalBudget = 3; break;
            case "campaign-matrix": f.Campaign.MatrixReference = ""; break;
            case "outside-window": f.Campaign.StartsAtUtc = DateTimeOffset.UtcNow.AddMinutes(1); break;
            case "ledger-date": claim.CreatedAt = claim.CreatedAt.AddSeconds(1); break;
            default: doc.IdempotencyRequestHash = claim.RequestHash = new string('Z', 64); break;
        }
        await f.Db.SaveChangesAsync();
        (await f.Count()).Should().Be(1);
        (await CertificationCampaignAccess.ValidateAsync(f.Db, 23, doc, default)).ErrorCode.Should().Be("CERT_CAMPAIGN_FORBIDDEN");
    }

    [Fact]
    public async Task OverBudgetLedgerCannotExcludeAnyDocumentFromCommercialCount()
    {
        using var f = new Fixture(); var first = await f.Stage();
        for (var i = 0; i < 2; i++)
        {
            var doc = f.NewDoc(); doc.IdempotencyScope = "CERT";
            doc.IdempotencyKeyHash = DteIdempotency.HashKey("forged-" + i);
            doc.IdempotencyRequestHash = first.RequestHash;
            f.Db.CertificationCampaignConsumptions.Add(new()
            {
                CampaignId = f.Campaign.Id, TipoDteCodigo = "01", EmpresaId = 23,
                Document = doc, IdempotencyKeyHash = doc.IdempotencyKeyHash, RequestHash = doc.IdempotencyRequestHash,
                ScenarioReference = "forged:" + i, CreatedAt = doc.CreatedAt
            });
        }
        await f.Db.SaveChangesAsync();
        (await f.Count()).Should().Be(3);
        (await CertificationCampaignAccess.ValidateAsync(f.Db, 23, first.Document, default)).ErrorCode.Should().Be("CERT_CAMPAIGN_FORBIDDEN");
    }

    [Theory]
    [InlineData("REVOKED")]
    [InlineData("CLOSED")]
    [InlineData("expired")]
    public async Task HistoricalValidConsumptionKeepsExemptionButCannotAdvanceAfterCampaignEnds(string state)
    {
        using var f = new Fixture(); var claim = await f.Stage();
        if (state == "expired") f.Campaign.ExpiresAtUtc = DateTimeOffset.UtcNow.AddTicks(-1);
        else f.Campaign.Status = state;
        await f.Db.SaveChangesAsync();
        (await f.Count()).Should().Be(0);
        (await CertificationCampaignAccess.ValidateAsync(f.Db, 23, claim.Document, default)).ErrorCode.Should().Be("CERT_CAMPAIGN_INACTIVE");
        (await f.Service.GetByIdAsync(23, claim.DteDocumentoId)).IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("license", "LICENSE_INVALID")]
    [InlineData("module", "CERT_CAMPAIGN_MODULE_DISABLED")]
    [InlineData("global-module", "CERT_CAMPAIGN_MODULE_DISABLED")]
    [InlineData("company", "LICENSE_INVALID")]
    [InlineData("environment", "CERT_CAMPAIGN_FORBIDDEN")]
    public async Task CampaignDoesNotSubstituteCurrentLicenseModulesOrFiscalContext(string change, string expected)
    {
        using var f = new Fixture(); var claim = await f.Stage();
        switch (change)
        {
            case "license": f.License.EstadoCodigo = "CANCELADO"; break;
            case "module": (await f.Db.EmpresaModulos.FirstAsync()).FechaInactivacion = DateTime.UtcNow; break;
            case "global-module": (await f.Db.Modulos.FirstAsync()).Activo = false; break;
            case "company": (await f.Db.Empresas.SingleAsync()).EstadoCodigo = "SUSPENDIDA"; break;
            default: f.Config.AmbienteCodigo = "PRODUCCION"; break;
        }
        await f.Db.SaveChangesAsync();
        (await CertificationCampaignAccess.ValidateAsync(f.Db, 23, claim.Document, default)).ErrorCode.Should().Be(expected);
    }

    [Theory]
    [InlineData("generar")]
    [InlineData("validar")]
    [InlineData("firmar")]
    [InlineData("enviar")]
    public async Task RealDocumentServiceRejectsRevokedCampaignWithoutExternalCalls(string operation)
    {
        using var f = new Fixture(); var claim = await f.Stage(); var doc = claim.Document;
        doc.Json = new() { JsonDte = DteFiscalIsolationTests.Payload(doc) };
        f.Campaign.Status = "REVOKED"; await f.Db.SaveChangesAsync();
        Result result = operation switch
        {
            "generar" => await f.Service.GenerarAsync(23, doc.Id, "test"),
            "validar" => await f.Service.ValidarAsync(23, doc.Id, "test"),
            "firmar" => await f.Service.FirmarAsync(23, doc.Id, "test"),
            _ => await f.Service.EnviarAsync(23, doc.Id, "test")
        };
        result.ErrorCode.Should().Be("CERT_CAMPAIGN_INACTIVE");
        f.External.ReceivedCalls().Should().BeEmpty();
        f.Generator.ReceivedCalls().Should().BeEmpty();
        f.Signer.ReceivedCalls().Should().BeEmpty();
        (await f.Db.DteDocumentos.AsNoTracking().SingleAsync()).EstadoCodigo.Should().Be("BORRADOR");
    }

    private sealed class Fixture : IDisposable
    {
        internal NeoStpDbContext Db { get; } = DteFiscalIsolationTests.Db();
        internal CertificationCampaign Campaign { get; } = new() { EmpresaId = 23, ExpectedNit = "00000000000023",
            Status = "ACTIVE", StartsAtUtc = DateTimeOffset.UtcNow.AddHours(-1), ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(1),
            TotalBudget = 2, MatrixReference = "synthetic-reviewed-matrix" };
        internal DteConfiguracion Config { get; } = new() { EmpresaId = 23, AmbienteCodigo = "PRUEBAS", TiposDteAutorizadosCsv = "01,03,11,14" };
        internal EmpresaPlan License { get; } = new() { EmpresaId = 23, PlanId = 700, FechaInicio = DateTime.UtcNow.AddDays(-1), FechaFin = DateTime.UtcNow.AddDays(5) };
        internal IHaciendaReceptionClient External { get; } = Substitute.For<IHaciendaReceptionClient>();
        internal IDteGeneratorService Generator { get; } = Substitute.For<IDteGeneratorService>();
        internal IDteSignerService Signer { get; } = Substitute.For<IDteSignerService>();
        internal DteDocumentosService Service { get; }
        internal Fixture()
        {
            Db.AddRange(new Empresa { Id = 23, Nit = "00000000000023", RazonSocial = "Synthetic" }, Config,
                new Plan { Id = 700, Codigo = "STARTER_TEST", Nombre = "Synthetic", PrecioMensual = 15, LimiteDteMensual = 1 }, License, Campaign);
            Campaign.TypeBudgets.Add(new() { TipoDteCodigo = "01", Budget = 2 });
            foreach (var (id, code) in new[] { (801, "CORE"), (802, "NEODTE") })
            {
                Db.Modulos.Add(new Modulo { Id = id, Codigo = code, Nombre = code });
                Db.PlanModulos.Add(new PlanModulo { PlanId = 700, ModuloId = id });
                Db.EmpresaModulos.Add(new EmpresaModulo { EmpresaId = 23, ModuloId = id });
            }
            Db.SaveChanges();
            Service = new(Db, new DteCalculator(), Generator, Signer, External,
                Substitute.For<IHaciendaContingenciaClient>(), Substitute.For<IHaciendaEventoClient>(), Substitute.For<IHaciendaAuthClient>(),
                DteFiscalIsolationTests.Protector(), Substitute.For<IDtePdfService>(), Substitute.For<ITenantEmailSender>(),
                Substitute.For<IAuditoriaService>(), Substitute.For<IConnectWebhookDispatcher>(), licenciaGuard: new LicenciaGuardService(Db));
        }
        internal DteDocumento NewDoc() => new() { EmpresaId = 23, TipoDteCodigo = "01", AmbienteCodigo = "PRUEBAS", EstadoCodigo = "BORRADOR",
            NumeroControl = "DTE-01-M001P001-000000000000001", CodigoGeneracion = Guid.NewGuid().ToString("D"), CreatedAt = DateTime.UtcNow };
        internal async Task<CertificationCampaignConsumption> Stage()
        {
            var result = await new CertificationCampaignQuotaService(Db).StageConsumptionAsync(
                new(Campaign.PublicId, 23, Campaign.ExpectedNit, "01", "synthetic-key", new string('A', 64), "matrix:001", "test"), NewDoc());
            result.IsSuccess.Should().BeTrue(result.Error); await Db.SaveChangesAsync(); return result.Value!.Consumption;
        }
        internal Task<int> Count() => CertificationCampaignAccess.CountCommercialDocumentsAsync(Db, 23,
            new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1), default);
        public void Dispose() => Db.Dispose();
    }
}
