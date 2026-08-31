using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Comunicaciones;
using NeoSTP.Application.Connect;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Application.Dte.Dtos;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;

namespace NeoSTP.Tests.Unit.Dte;

public class DteDocumentosListFiltersTests
{
    private const int EmpresaId = 7;

    [Fact]
    public async Task GetList_FiltraTenantTiposElegiblesYRangeDeMonto()
    {
        await using var db = CreateDb();
        db.DteDocumentos.AddRange(
            Documento(EmpresaId, "01", 100m, "Cliente Alfa"),
            Documento(EmpresaId, "11", 200m, "Exportadora Beta"),
            Documento(EmpresaId, "14", 50m, "Proveedor Gamma"),
            Documento(EmpresaId, "03", 120m, "CCF no elegible"),
            Documento(99, "01", 110m, "Otra empresa"));
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetListAsync(EmpresaId, new DteListQuery
        {
            EstadoCodigo = DteEstadoCodigos.Procesado,
            TiposDteCodigo = ["01", "11", "14"],
            MontoMinimo = 80m,
            MontoMaximo = 150m,
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().ContainSingle();
        result.Value.Items[0].TipoDteCodigo.Should().Be("01");
        result.Value.Items[0].ReceptorNombre.Should().Be("Cliente Alfa");
    }

    [Theory]
    [InlineData("000000000000321")]
    [InlineData("ABCDEF12")]
    [InlineData("cliente alfa")]
    [InlineData("04210304-8")]
    public async Task GetList_BuscaPorNumeroCodigoClienteODocumento(string search)
    {
        await using var db = CreateDb();
        var documento = Documento(EmpresaId, "01", 100m, "Cliente Alfa");
        documento.NumeroControl = "DTE-01-M001P001-000000000000321";
        documento.CodigoGeneracion = "ABCDEF12-3456-4789-ABCD-123456789012";
        documento.ReceptorNumeroDocumento = "04210304-8";
        db.DteDocumentos.Add(documento);
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetListAsync(
            EmpresaId,
            new DteListQuery { Search = search });

        result.Value!.Items.Should().ContainSingle(x => x.Id == documento.Id);
    }

    private static NeoStpDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"dte-list-filters-{Guid.NewGuid()}")
            .Options;
        return new NeoStpDbContext(options);
    }

    private static DteDocumento Documento(int empresaId, string tipo, decimal total, string receptor) => new()
    {
        EmpresaId = empresaId,
        TipoDteCodigo = tipo,
        NumeroControl = $"DTE-{tipo}-M001P001-{Guid.NewGuid():N}"[..36],
        CodigoGeneracion = Guid.NewGuid().ToString().ToUpperInvariant(),
        FechaEmision = new DateTime(2026, 8, 27),
        ReceptorNombre = receptor,
        ReceptorNumeroDocumento = "0614-010101-101-1",
        TotalPagar = total,
        MontoTotalOperacion = total,
        EstadoCodigo = DteEstadoCodigos.Procesado,
        AmbienteCodigo = "PRUEBAS",
    };

    private static DteDocumentosService CreateService(NeoStpDbContext db) => new(
        db,
        new DteCalculator(),
        Substitute.For<IDteGeneratorService>(),
        Substitute.For<IDteSignerService>(),
        Substitute.For<IHaciendaReceptionClient>(),
        Substitute.For<IHaciendaContingenciaClient>(),
        Substitute.For<IHaciendaEventoClient>(),
        Substitute.For<IHaciendaAuthClient>(),
        Substitute.For<ISecretProtector>(),
        Substitute.For<IDtePdfService>(),
        Substitute.For<ITenantEmailSender>(),
        Substitute.For<IAuditoriaService>(),
        Substitute.For<IConnectWebhookDispatcher>());
}
