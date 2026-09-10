using FluentAssertions;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Dte.Certificacion;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Licenciamiento;
using NeoSTP.Infrastructure.Dte.Certificacion;
using NeoSTP.Infrastructure.Services;
using NeoSTP.Tests.Unit.Dte;
using Xunit;

namespace NeoSTP.Tests.Unit.Dashboard;

/// <summary>
/// El dashboard debe distinguir la ACTIVIDAD total (DteMes, incluye certificación) del
/// CONSUMO COMERCIAL (DteMesComercial, excluye la certificación de campañas coherentes).
/// La barra de cupo usa el consumo comercial, para que las pruebas de certificación no
/// llenen el cupo del plan por error.
/// </summary>
public class ConsumoComercialTests
{
    private static DteDocumento Comercial(DateTime when, string numeroControl) => new()
    {
        EmpresaId = 23, TipoDteCodigo = "01", AmbienteCodigo = "PRUEBAS", EstadoCodigo = "PROCESADO",
        NumeroControl = numeroControl, CodigoGeneracion = Guid.NewGuid().ToString("D"),
        CreatedAt = when, FechaEmision = when.Date,
    };

    private static DteDocumento DraftParaStage(string numeroControl) => new()
    {
        EmpresaId = 23, TipoDteCodigo = "01", AmbienteCodigo = "PRUEBAS", EstadoCodigo = "BORRADOR",
        NumeroControl = numeroControl, CodigoGeneracion = Guid.NewGuid().ToString("D"),
        FechaEmision = DateTime.UtcNow.Date, Detalles = new List<DteDocumentoDetalle>(),
    };

    [Fact]
    public async Task Dashboard_SeparaActividadTotal_DelConsumoComercial()
    {
        using var db = DteFiscalIsolationTests.Db();
        var hoy = DateTime.UtcNow;

        var campaign = new CertificationCampaign
        {
            EmpresaId = 23, ExpectedNit = "00000000000023", Status = "ACTIVE",
            StartsAtUtc = DateTimeOffset.UtcNow.AddHours(-1), ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(1),
            TotalBudget = 5, MatrixReference = "synthetic-reviewed-matrix",
        };
        campaign.TypeBudgets.Add(new CertificationCampaignTypeBudget { TipoDteCodigo = "01", Budget = 5 });
        db.AddRange(
            new Empresa { Id = 23, Nit = "00000000000023", RazonSocial = "Synthetic", EstadoCodigo = "ACTIVA" },
            new DteConfiguracion { EmpresaId = 23, AmbienteCodigo = "PRUEBAS", TiposDteAutorizadosCsv = "01,03,11,14" },
            new Plan { Id = 700, Codigo = "STARTER_TEST", Nombre = "Synthetic", PrecioMensual = 15, LimiteDteMensual = 100 },
            new EmpresaPlan { EmpresaId = 23, PlanId = 700, EstadoCodigo = "ACTIVO", FechaInicio = DateTime.UtcNow.AddDays(-1), FechaFin = DateTime.UtcNow.AddDays(5) },
            campaign);
        foreach (var (id, code) in new[] { (801, "CORE"), (802, "NEODTE") })
        {
            db.Modulos.Add(new Modulo { Id = id, Codigo = code, Nombre = code });
            db.PlanModulos.Add(new PlanModulo { PlanId = 700, ModuloId = id });
            db.EmpresaModulos.Add(new EmpresaModulo { EmpresaId = 23, ModuloId = id });
        }
        await db.SaveChangesAsync();

        // 1 documento de certificación (consumo de campaña coherente → excluido del comercial).
        var stage = await new CertificationCampaignQuotaService(db).StageConsumptionAsync(
            new CertificationConsumptionRequest(campaign.PublicId, 23, campaign.ExpectedNit, "01",
                "cert-key", new string('A', 64), "matrix:001", "test"),
            DraftParaStage("DTE-01-M001P001-000000000000900"));
        stage.IsSuccess.Should().BeTrue(stage.Error);
        await db.SaveChangesAsync();

        // 2 documentos comerciales ordinarios del mismo mes.
        db.DteDocumentos.Add(Comercial(hoy, "DTE-01-M001P001-000000000000901"));
        db.DteDocumentos.Add(Comercial(hoy, "DTE-01-M001P001-000000000000902"));
        await db.SaveChangesAsync();

        var dash = await new DashboardService(db).GetDashboardEmpresaAsync(23);

        dash.DteMes.Should().Be(3, "la actividad total incluye el documento de certificación");
        dash.DteMesComercial.Should().Be(2, "el consumo comercial excluye la certificación coherente");
        dash.PorcentajeUsoDte.Should().Be(2, "2 comerciales sobre un límite de 100 = 2%");
    }
}
