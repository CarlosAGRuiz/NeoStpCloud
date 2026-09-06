using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Dtos;
using NeoSTP.Application.Licenciamiento;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Dte.Certificacion;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Licenciamiento;
using NeoSTP.Infrastructure.Dte.Certificacion;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;

namespace NeoSTP.Tests.Unit.Dte.Certificacion;

/// <summary>Isolated policy/staging tests. InMemory does not establish SQL lock, rollback or race guarantees.</summary>
public sealed class CertificationCampaignQuotaTests
{
    [Theory]
    [InlineData("")]
    [InlineData("03")]
    [InlineData("01,INVALID")]
    public async Task New_consumption_respects_current_tenant_fiscal_type_authorization(string types)
    {
        using var f = new Fixture();
        f.Configuration.TiposDteAutorizadosCsv = types;
        await f.Db.SaveChangesAsync();
        var result = await f.Service.StageConsumptionAsync(f.Request(), f.Document());
        result.ErrorCode.Should().Be("DTE_TIPO_NO_AUTORIZADO");
        f.AssertNothingStaged();
    }
    [Fact]
    public async Task Stages_document_and_consumption_without_saving_or_changing_commercial_entitlements()
    {
        using var f = new Fixture();
        var result = await f.Service.StageConsumptionAsync(f.Request(), f.Document());
        result.IsSuccess.Should().BeTrue();
        result.Value!.Replayed.Should().BeFalse();
        f.Db.ChangeTracker.Entries<DteDocumento>().Count(x => x.State == EntityState.Added).Should().Be(1);
        f.Db.ChangeTracker.Entries<CertificationCampaignConsumption>().Count(x => x.State == EntityState.Added).Should().Be(1);
        using (var observer = f.NewContext())
        {
            (await observer.DteDocumentos.CountAsync()).Should().Be(0);
            (await observer.CertificationCampaignConsumptions.CountAsync()).Should().Be(0);
        }
        await f.Db.SaveChangesAsync();
        using var saved = f.NewContext();
        var consumption = await saved.CertificationCampaignConsumptions.Include(x => x.Document).SingleAsync();
        consumption.Document.Id.Should().Be(consumption.DteDocumentoId);
        consumption.Document.EmpresaId.Should().Be(Fixture.Tenant);
        consumption.Document.IdempotencyScope.Should().Be("CERT");
        consumption.CreatedBy.Should().Be("synthetic-operator");
        (await saved.Planes.SingleAsync()).LimiteDteMensual.Should().Be(100);
        (await saved.BillingPayments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Type_budget_is_enforced_independently_of_remaining_campaign_total()
    {
        using var f = new Fixture(budget01: 1, budget03: 2);
        (await f.Service.StageConsumptionAsync(f.Request("first"), f.Document())).IsSuccess.Should().BeTrue();
        await f.Db.SaveChangesAsync();
        var denied = await f.Service.StageConsumptionAsync(f.Request("second"), f.Document());
        denied.ErrorCode.Should().Be("CERT_CAMPAIGN_EXHAUSTED");
        (await f.Service.StageConsumptionAsync(f.Request("third", "03"), f.Document("03"))).IsSuccess.Should().BeTrue();
        await f.Db.SaveChangesAsync();
        (await f.Db.CertificationCampaignConsumptions.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Pending_staged_consumption_spends_budget_before_caller_saves()
    {
        using var f = new Fixture(budget01: 1);
        (await f.Service.StageConsumptionAsync(f.Request("first"), f.Document())).IsSuccess.Should().BeTrue();
        (await f.Service.StageConsumptionAsync(f.Request("second"), f.Document())).ErrorCode.Should().Be("CERT_CAMPAIGN_EXHAUSTED");
        f.Db.ChangeTracker.Entries<CertificationCampaignConsumption>().Count(x => x.State == EntityState.Added).Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Same_key_replays_before_or_after_commit_without_tracking_another_document(bool save)
    {
        using var f = new Fixture(budget01: 1);
        var original = await f.Service.StageConsumptionAsync(f.Request(), f.Document());
        if (save) { await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear(); }
        var extra = f.Document();
        var replay = await f.Service.StageConsumptionAsync(f.Request(), extra);
        replay.IsSuccess.Should().BeTrue();
        replay.Value!.Replayed.Should().BeTrue();
        replay.Value.Consumption.PublicId.Should().Be(original.Value!.Consumption.PublicId);
        f.Db.Entry(extra).State.Should().Be(EntityState.Detached);
        extra.IdempotencyScope.Should().BeNull();
        await f.Db.SaveChangesAsync();
        (await f.Db.DteDocumentos.CountAsync()).Should().Be(1);
        (await f.Db.CertificationCampaignConsumptions.CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData("payload")]
    [InlineData("scenario")]
    [InlineData("type")]
    public async Task Reusing_key_with_different_request_fails_without_consuming_more(string change)
    {
        using var f = new Fixture(budget03: 1);
        await f.Service.StageConsumptionAsync(f.Request(), f.Document());
        await f.Db.SaveChangesAsync();
        var request = change switch
        {
            "payload" => f.Request() with { PayloadHash = new string('B', 64) },
            "scenario" => f.Request() with { ScenarioReference = "official-fixture:002" },
            _ => f.Request() with { TipoDteCodigo = "03" },
        };
        var candidate = f.Document(request.TipoDteCodigo);
        var result = await f.Service.StageConsumptionAsync(request, candidate);
        result.ErrorCode.Should().Be("IDEMPOTENCY_CONFLICT");
        f.Db.Entry(candidate).State.Should().Be(EntityState.Detached);
        (await f.Db.CertificationCampaignConsumptions.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Expired_campaign_allows_read_replay_but_no_new_reservation()
    {
        using var f = new Fixture();
        await f.Service.StageConsumptionAsync(f.Request(), f.Document());
        await f.Db.SaveChangesAsync();
        f.Clock.Now = f.Campaign.ExpiresAtUtc;
        var replay = await f.Service.StageConsumptionAsync(f.Request(), f.Document());
        replay.IsSuccess.Should().BeTrue();
        replay.Value!.Replayed.Should().BeTrue();
        (await f.Service.StageConsumptionAsync(f.Request("new"), f.Document())).ErrorCode.Should().Be("CERT_CAMPAIGN_INACTIVE");
    }

    [Theory]
    [InlineData("PREPARED")]
    [InlineData("REVOKED")]
    [InlineData("CLOSED")]
    [InlineData("unknown")]
    public async Task Campaign_must_be_explicitly_active(string status)
    {
        using var f = new Fixture();
        f.Campaign.Status = status;
        await f.Db.SaveChangesAsync();
        (await f.Service.StageConsumptionAsync(f.Request(), f.Document())).ErrorCode.Should().Be("CERT_CAMPAIGN_INACTIVE");
        f.AssertNothingStaged();
    }

    [Theory]
    [InlineData("before-start")]
    [InlineData("at-expiry")]
    [InlineData("after-expiry")]
    public async Task Validity_window_is_inclusive_start_exclusive_expiry(string time)
    {
        using var f = new Fixture();
        f.Clock.Now = time switch
        {
            "before-start" => f.Campaign.StartsAtUtc.AddTicks(-1),
            "at-expiry" => f.Campaign.ExpiresAtUtc,
            _ => f.Campaign.ExpiresAtUtc.AddTicks(1),
        };
        (await f.Service.StageConsumptionAsync(f.Request(), f.Document())).ErrorCode.Should().Be("CERT_CAMPAIGN_INACTIVE");
        f.AssertNothingStaged();
    }

    [Theory]
    [InlineData("zero")]
    [InlineData("negative-type")]
    [InlineData("mismatched-total")]
    [InlineData("offset")]
    [InlineData("missing-matrix")]
    public async Task Malformed_persisted_campaign_is_not_an_unlimited_allowance(string defect)
    {
        using var f = new Fixture();
        switch (defect)
        {
            case "zero": f.Campaign.TotalBudget = 0; break;
            case "negative-type": f.Campaign.TypeBudgets.Single().Budget = -1; break;
            case "mismatched-total": f.Campaign.TotalBudget++; break;
            case "offset": f.Campaign.StartsAtUtc = f.Campaign.StartsAtUtc.ToOffset(TimeSpan.FromHours(-6)); break;
            case "missing-matrix": f.Campaign.MatrixReference = " "; break;
        }
        await f.Db.SaveChangesAsync();
        (await f.Service.StageConsumptionAsync(f.Request(), f.Document())).ErrorCode.Should().Be("CERT_CAMPAIGN_INVALID");
        f.AssertNothingStaged();
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("request-nit")]
    [InlineData("stored-nit")]
    [InlineData("campaign-nit")]
    [InlineData("configuration-production")]
    [InlineData("campaign-production")]
    [InlineData("different-campaign")]
    public async Task Tenant_nit_and_fiscal_context_must_match_persisted_authority(string defect)
    {
        using var f = new Fixture();
        var request = f.Request();
        switch (defect)
        {
            case "tenant": request = request with { EmpresaId = 2 }; break;
            case "request-nit": request = request with { ExpectedNit = "11111111111111" }; break;
            case "stored-nit": f.Company.Nit = "11111111111111"; break;
            case "campaign-nit": f.Campaign.ExpectedNit = "11111111111111"; break;
            case "configuration-production": f.Configuration.AmbienteCodigo = "PRODUCCION"; break;
            case "campaign-production": f.Campaign.AmbienteCodigo = "PRODUCCION"; break;
            case "different-campaign": request = request with { CampaignPublicId = Guid.NewGuid() }; break;
        }
        await f.Db.SaveChangesAsync();
        var document = f.Document(); document.EmpresaId = request.EmpresaId;
        (await f.Service.StageConsumptionAsync(request, document)).ErrorCode.Should().Be("CERT_CAMPAIGN_FORBIDDEN");
        f.AssertNothingStaged();
    }

    [Theory]
    [InlineData("missing-key")]
    [InlineData("invalid-key")]
    [InlineData("unsupported-type")]
    [InlineData("missing-payload")]
    [InlineData("missing-scenario")]
    [InlineData("missing-actor")]
    public async Task Missing_or_invalid_identifiers_do_not_create_an_attempt(string defect)
    {
        using var f = new Fixture();
        var request = defect switch
        {
            "missing-key" => f.Request() with { IdempotencyKey = null! },
            "invalid-key" => f.Request() with { IdempotencyKey = "contains spaces" },
            "unsupported-type" => f.Request() with { TipoDteCodigo = "05" },
            "missing-payload" => f.Request() with { PayloadHash = "" },
            "missing-scenario" => f.Request() with { ScenarioReference = "" },
            _ => f.Request() with { Actor = "" },
        };
        (await f.Service.StageConsumptionAsync(request, f.Document())).ErrorCode.Should().Be("CERT_CAMPAIGN_REQUEST_INVALID");
        f.AssertNothingStaged();
    }

    [Theory]
    [InlineData("production")]
    [InlineData("processed")]
    [InlineData("sent")]
    [InlineData("existing-id")]
    [InlineData("wrong-tenant")]
    [InlineData("tracked")]
    public async Task Only_a_fresh_test_draft_can_be_staged(string defect)
    {
        using var f = new Fixture();
        var document = f.Document();
        switch (defect)
        {
            case "production": document.AmbienteCodigo = "PRODUCCION"; break;
            case "processed": document.EstadoCodigo = "PROCESADO"; break;
            case "sent": document.EnviadoAt = DateTime.UtcNow; break;
            case "existing-id": document.Id = 99; break;
            case "wrong-tenant": document.EmpresaId = 2; break;
            case "tracked": f.Db.DteDocumentos.Add(document); break;
        }
        (await f.Service.StageConsumptionAsync(f.Request(), document)).ErrorCode.Should().Be("CERT_CAMPAIGN_DRAFT_REQUIRED");
        f.Db.ChangeTracker.Entries<CertificationCampaignConsumption>().Should().BeEmpty();
    }

    [Theory]
    [InlineData("expired-license", "LICENSE_INVALID")]
    [InlineData("duplicate-license", "LICENSE_INVALID")]
    [InlineData("suspended-company", "LICENSE_INVALID")]
    [InlineData("negative-entitlement", "LICENSE_SNAPSHOT_INVALID")]
    [InlineData("global-module", "CERT_CAMPAIGN_MODULE_DISABLED")]
    [InlineData("company-module", "CERT_CAMPAIGN_MODULE_DISABLED")]
    [InlineData("revoked-module", "CERT_CAMPAIGN_MODULE_DISABLED")]
    [InlineData("unlicensed-module", "CERT_CAMPAIGN_MODULE_DISABLED")]
    public async Task Campaign_does_not_replace_license_or_module_validation(string defect, string code)
    {
        using var f = new Fixture();
        switch (defect)
        {
            case "expired-license": f.License.FechaFin = f.Clock.Now.UtcDateTime; break;
            case "duplicate-license": f.Db.EmpresaPlanes.Add(new EmpresaPlan { EmpresaId = Fixture.Tenant, PlanId = f.Plan.Id, FechaInicio = f.License.FechaInicio }); break;
            case "suspended-company": f.Company.EstadoCodigo = "SUSPENDIDA"; break;
            case "negative-entitlement": f.Plan.LimiteDteMensual = -1; break;
            case "global-module": (await f.Db.Modulos.SingleAsync(x => x.Codigo == "NEODTE")).Activo = false; break;
            case "company-module": (await f.Db.EmpresaModulos.SingleAsync(x => x.ModuloId == 802)).Activo = false; break;
            case "revoked-module": (await f.Db.EmpresaModulos.SingleAsync(x => x.ModuloId == 802)).FechaInactivacion = f.Clock.Now.UtcDateTime; break;
            case "unlicensed-module": (await f.Db.PlanModulos.SingleAsync(x => x.ModuloId == 802)).Activo = false; break;
        }
        await f.Db.SaveChangesAsync();
        (await f.Service.StageConsumptionAsync(f.Request(), f.Document())).ErrorCode.Should().Be(code);
        f.AssertNothingStaged();
    }

    [Fact]
    public async Task Committed_campaign_consumption_is_excluded_without_changing_plan_limit()
    {
        using var f = new Fixture();
        f.Plan.LimiteDteMensual = 1;
        await f.Service.StageConsumptionAsync(f.Request(), f.Document());
        await f.Db.SaveChangesAsync();
        (await new LicenciaGuardService(f.Db).ValidarLimiteAsync(Fixture.Tenant, RecursoLimitado.DteMensual))
            .IsSuccess.Should().BeTrue();
        f.Plan.LimiteDteMensual.Should().Be(1);
    }

    [Fact]
    public async Task Precancelled_operation_never_stages_or_persists_entities()
    {
        using var f = new Fixture();
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Func<Task> action = () => f.Service.StageConsumptionAsync(f.Request(), f.Document(), cancellation.Token);
        await action.Should().ThrowAsync<OperationCanceledException>();
        f.AssertNothingStaged();
    }

    [Fact]
    public async Task Sql_server_without_caller_transaction_fails_before_opening_connection()
    {
        using var f = new Fixture();
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseSqlServer("Server=synthetic.invalid;Database=NeverOpened;Integrated Security=true;TrustServerCertificate=true").Options;
        await using var db = new NeoStpDbContext(options);
        var result = await new CertificationCampaignQuotaService(db).StageConsumptionAsync(f.Request(), f.Document());
        result.ErrorCode.Should().Be("CERT_CAMPAIGN_TRANSACTION_REQUIRED");
        db.Database.GetDbConnection().State.Should().Be(System.Data.ConnectionState.Closed);
        db.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Theory]
    [InlineData("detached-company")]
    [InlineData("foreign-company")]
    [InlineData("tracked-company")]
    [InlineData("client-navigation")]
    [InlineData("related-navigation")]
    [InlineData("existing-json")]
    [InlineData("detail-product-navigation")]
    [InlineData("existing-detail")]
    public async Task Staging_never_inserts_business_navigation_graphs_or_reuses_persisted_children(string defect)
    {
        using var f = new Fixture();
        var document = f.Document();
        switch (defect)
        {
            case "detached-company": document.Empresa = new Empresa { Id = Fixture.Tenant, Nit = "00000000000023", RazonSocial = "Unexpected clone" }; break;
            case "foreign-company": document.Empresa = new Empresa { Id = 2, Nit = "00000000000002", RazonSocial = "Foreign clone" }; break;
            case "tracked-company": document.Empresa = f.Company; break;
            case "client-navigation": document.Cliente = new NeoSTP.Domain.Core.Clientes.Cliente { EmpresaId = 2, Nombre = "Unexpected receiver" }; break;
            case "related-navigation": document.DocumentoRelacionado = f.Document(); break;
            case "existing-json": document.Json = new DteDocumentoJson { Id = 123, DocumentoId = 456, JsonDte = "{}" }; break;
            case "detail-product-navigation": document.Detalles.Add(new DteDocumentoDetalle { Codigo = "fixture", Descripcion = "fixture", Producto = new NeoSTP.Domain.Core.Productos.Producto { EmpresaId = 2, CodigoInterno = "foreign", Nombre = "foreign" } }); break;
            case "existing-detail": document.Detalles.Add(new DteDocumentoDetalle { Id = 123, DocumentoId = 456, Codigo = "fixture", Descripcion = "fixture" }); break;
        }
        var result = await f.Service.StageConsumptionAsync(f.Request(), document);
        result.ErrorCode.Should().Be("CERT_CAMPAIGN_DRAFT_REQUIRED");
        f.Db.ChangeTracker.Entries().Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).Should().BeEmpty();
        (await f.Db.Empresas.CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData("client")]
    [InlineData("branch")]
    [InlineData("point")]
    [InlineData("product")]
    [InlineData("related")]
    public async Task Foreign_tenant_foreign_keys_are_rejected_without_staging(string reference)
    {
        using var f = new Fixture();
        f.Db.Empresas.Add(new Empresa { Id = 2, Nit = "00000000000002", RazonSocial = "Foreign test company" });
        f.Db.Clientes.Add(new NeoSTP.Domain.Core.Clientes.Cliente { Id = 9001, EmpresaId = 2, Nombre = "Foreign receiver" });
        f.Db.Sucursales.Add(new Sucursal { Id = 9001, EmpresaId = 2, Codigo = "foreign", Nombre = "Foreign branch" });
        f.Db.PuntosVenta.Add(new PuntoVenta { Id = 9001, SucursalId = 9001, Codigo = "foreign", Nombre = "Foreign point" });
        f.Db.Productos.Add(new NeoSTP.Domain.Core.Productos.Producto { Id = 9001, EmpresaId = 2, CodigoInterno = "foreign", Nombre = "Foreign product" });
        var foreign = f.Document(); foreign.EmpresaId = 2; f.Db.DteDocumentos.Add(foreign);
        await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        var document = f.Document();
        switch (reference)
        {
            case "client": document.ClienteId = 9001; break;
            case "branch": document.SucursalId = 9001; break;
            case "point": document.PuntoVentaId = 9001; break;
            case "product": document.Detalles.Add(new DteDocumentoDetalle { Codigo = "fixture", Descripcion = "fixture", ProductoId = 9001 }); break;
            case "related": document.DocumentoRelacionadoId = foreign.Id; break;
        }
        (await f.Service.StageConsumptionAsync(f.Request(), document)).ErrorCode.Should().Be("CERT_CAMPAIGN_FORBIDDEN");
        f.AssertNothingStaged();
    }

    [Fact]
    public async Task New_detail_rows_are_staged_only_with_their_new_parent()
    {
        using var f = new Fixture();
        var document = f.Document();
        document.Detalles.Add(new DteDocumentoDetalle { Documento = document, NumeroLinea = 1, Codigo = "fixture", Descripcion = "Synthetic item", Cantidad = 1 });
        (await f.Service.StageConsumptionAsync(f.Request(), document)).IsSuccess.Should().BeTrue();
        await f.Db.SaveChangesAsync();
        (await f.Db.DteDocumentoDetalles.SingleAsync()).DocumentoId.Should().Be(document.Id);
        (await f.Db.Empresas.CountAsync()).Should().Be(1);
    }
    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public async Task Actor_length_respects_the_existing_document_column(int length, bool accepted)
    {
        using var f = new Fixture();
        var result = await f.Service.StageConsumptionAsync(f.Request() with { Actor = new string('x', length) }, f.Document());
        result.IsSuccess.Should().Be(accepted);
        if (accepted)
        {
            await f.Db.SaveChangesAsync();
            (await f.Db.DteDocumentos.SingleAsync()).CreatedBy.Should().HaveLength(100);
        }
        else
        {
            result.ErrorCode.Should().Be("CERT_CAMPAIGN_REQUEST_INVALID");
            f.AssertNothingStaged();
        }
    }
    [Theory]
    [InlineData(System.Data.IsolationLevel.Snapshot)]
    [InlineData(System.Data.IsolationLevel.ReadUncommitted)]
    [InlineData(System.Data.IsolationLevel.ReadCommitted)]
    public async Task Caller_transaction_with_wrong_isolation_is_rejected_before_sql(System.Data.IsolationLevel isolation)
    {
        using var f = new Fixture();
        var transaction = Substitute.For<IDbContextTransaction, IInfrastructure<System.Data.Common.DbTransaction>>();
        ((IInfrastructure<System.Data.Common.DbTransaction>)transaction).Instance.Returns(new IsolationProbeTransaction(isolation));
        var manager = Substitute.For<IDbContextTransactionManager>();
        manager.CurrentTransaction.Returns(transaction);
        var registrations = new ServiceCollection();
        registrations.AddEntityFrameworkSqlServer();
        registrations.AddScoped<IDbContextTransactionManager>(_ => manager);
        using var provider = registrations.BuildServiceProvider();
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseSqlServer("Server=synthetic.invalid;Database=NeverOpened;Integrated Security=true;TrustServerCertificate=true")
            .UseInternalServiceProvider(provider).Options;
        await using var db = new NeoStpDbContext(options);

        var result = await new CertificationCampaignQuotaService(db).StageConsumptionAsync(f.Request(), f.Document());

        result.ErrorCode.Should().Be("CERT_CAMPAIGN_TRANSACTION_REQUIRED");
        db.Database.GetDbConnection().State.Should().Be(System.Data.ConnectionState.Closed);
        db.ChangeTracker.Entries().Should().BeEmpty();
    }

    private sealed class IsolationProbeTransaction(System.Data.IsolationLevel isolation) : System.Data.Common.DbTransaction
    {
        public override System.Data.IsolationLevel IsolationLevel => isolation;
        protected override System.Data.Common.DbConnection? DbConnection => null;
        public override void Commit() => throw new InvalidOperationException("The policy probe must never commit.");
        public override void Rollback() => throw new InvalidOperationException("The policy probe must never roll back.");
    }
    private sealed class FixedClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Fixture : IDisposable
    {
        public const int Tenant = 23;
        private const string Nit = "00000000000023";
        private readonly DbContextOptions<NeoStpDbContext> options;
        public NeoStpDbContext Db { get; }
        public FixedClock Clock { get; } = new();
        public CertificationCampaignQuotaService Service { get; }
        public CertificationCampaign Campaign { get; }
        public Empresa Company { get; }
        public DteConfiguracion Configuration { get; }
        public Plan Plan { get; }
        public EmpresaPlan License { get; }

        public Fixture(int budget01 = 2, int budget03 = 0)
        {
            options = new DbContextOptionsBuilder<NeoStpDbContext>().UseInMemoryDatabase("cert-foundation-" + Guid.NewGuid()).Options;
            Db = new NeoStpDbContext(options);
            Company = new Empresa { Id = Tenant, Nit = Nit, RazonSocial = "Synthetic campaign company" };
            Configuration = new DteConfiguracion { EmpresaId = Tenant, AmbienteCodigo = "PRUEBAS" };
            Plan = new Plan { Id = 700, Codigo = "STARTER_TEST", Nombre = "Synthetic starter", PrecioMensual = 15, LimiteDteMensual = 100 };
            License = new EmpresaPlan { EmpresaId = Tenant, PlanId = Plan.Id, FechaInicio = Clock.Now.UtcDateTime.AddDays(-1), FechaFin = Clock.Now.UtcDateTime.AddDays(5) };
            Campaign = new CertificationCampaign
            {
                EmpresaId = Tenant, ExpectedNit = Nit, Status = "ACTIVE", StartsAtUtc = Clock.Now.AddHours(-1),
                ExpiresAtUtc = Clock.Now.AddDays(1), TotalBudget = budget01 + budget03, MatrixReference = "synthetic-reviewed-matrix",
                CreatedBy = "synthetic-operator",
            };
            Campaign.TypeBudgets.Add(new CertificationCampaignTypeBudget { TipoDteCodigo = "01", Budget = budget01 });
            if (budget03 > 0) Campaign.TypeBudgets.Add(new CertificationCampaignTypeBudget { TipoDteCodigo = "03", Budget = budget03 });
            Db.AddRange(Company, Configuration, Plan, License, Campaign);
            foreach (var (id, code) in new[] { (801, "CORE"), (802, "NEODTE") })
            {
                Db.Modulos.Add(new Modulo { Id = id, Codigo = code, Nombre = code });
                Db.PlanModulos.Add(new PlanModulo { PlanId = Plan.Id, ModuloId = id });
                Db.EmpresaModulos.Add(new EmpresaModulo { EmpresaId = Tenant, ModuloId = id });
            }
            Db.SaveChanges();
            Service = new CertificationCampaignQuotaService(Db, Clock);
        }

        public CertificationConsumptionRequest Request(string key = "attempt-1", string type = "01")
            => new(Campaign.PublicId, Tenant, Nit, type, key, new string('A', 64), "official-fixture:001", "synthetic-operator");
        public DteDocumento Document(string type = "01") => new()
        {
            EmpresaId = Tenant, TipoDteCodigo = type, AmbienteCodigo = "PRUEBAS", EstadoCodigo = "BORRADOR",
            NumeroControl = "DTE-" + Guid.NewGuid().ToString("N"), CodigoGeneracion = Guid.NewGuid().ToString("D"),
        };
        public NeoStpDbContext NewContext() => new(options);
        public void AssertNothingStaged()
        {
            Db.ChangeTracker.Entries<DteDocumento>().Should().BeEmpty();
            Db.ChangeTracker.Entries<CertificationCampaignConsumption>().Should().BeEmpty();
        }
        public void Dispose() => Db.Dispose();
    }
}
