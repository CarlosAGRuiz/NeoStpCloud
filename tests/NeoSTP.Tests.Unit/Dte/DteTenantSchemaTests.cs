using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Lookups;
using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Dte;
using NSubstitute;

namespace NeoSTP.Tests.Unit.Dte;

public class DteTenantSchemaTests
{
    private const int Tenant = 23;
    private const string Nit = "06140101001011";
    private static Dictionary<string, string?> Values() => new()
    {
        ["Dte:EsquemaNuevo"] = "false",
        ["Dte:TenantSchemas:23:Nit"] = Nit,
        ["Dte:TenantSchemas:23:Ambiente"] = "PRUEBAS",
        ["Dte:TenantSchemas:23:Profile"] = "MH_20260825",
    };
    private static IConfiguration Config(Dictionary<string, string?>? values = null)
        => new ConfigurationBuilder().AddInMemoryCollection(values ?? Values()).Build();

    [Theory]
    [InlineData("01", 2)]
    [InlineData("03", 4)]
    [InlineData("11", 3)]
    [InlineData("14", 2)]
    public async Task OrdinaryServiceUsesTenantProfileAndPersistsActualVersionWithResolvedDistrict(string type, int expected)
    {
        await using var db = DteEmisorSaneamientoTests.CreateDb();
        var document = Document(type);
        // These are catalog identities, so this verifies the service resolves territory with the tenant profile.
        document.Empresa.Departamento = "La Libertad";
        document.Empresa.Municipio = "La Libertad Centro";
        document.Empresa.Distrito = "Distrito emisor";
        document.ReceptorDepartamentoCodigo = "La Libertad";
        document.ReceptorMunicipioCodigo = "La Libertad Centro";
        document.ReceptorDistritoCodigo = "Distrito emisor";
        db.DteDocumentos.Add(document);
        db.DteConfiguracion.Add(new() { EmpresaId = Tenant, AmbienteCodigo = "PRUEBAS", TipoEstablecimientoCodigo = "02" });
        await db.SaveChangesAsync();
        var config = Config();
        var lookup = Lookup();
        var service = DteEmisorSaneamientoTests.CreateService(db, Generator(config), lookup, config);

        var result = await service.GenerarAsync(Tenant, document.Id, "synthetic");

        result.IsSuccess.Should().BeTrue(result.Error);
        db.ChangeTracker.Clear();
        var persisted = await db.DteDocumentos.Include(x => x.Json).SingleAsync();
        persisted.VersionDte.Should().Be(expected);
        using var json = JsonDocument.Parse(persisted.Json!.JsonDte);
        json.RootElement.GetProperty("identificacion").GetProperty("version").GetInt32().Should().Be(expected);
        var address = json.RootElement.GetProperty("emisor").GetProperty("direccion");
        address.GetProperty("municipio").GetString().Should().Be("22");
        address.GetProperty("distrito").GetString().Should().Be("15");
        (await db.Empresas.SingleAsync()).Distrito.Should().Be("Distrito emisor");
        config.GetValue<bool>("Dte:EsquemaNuevo").Should().BeFalse();
    }

    [Theory]
    [InlineData("01", 1, 2)]
    [InlineData("03", 3, 4)]
    [InlineData("11", 3, 3)]
    [InlineData("14", 1, 2)]
    public void SameGeneratorAlternatesTenantsWithoutChangingUnconfiguredTenant(string type, int legacy, int modern)
    {
        var generator = Generator(Config());
        foreach (var (tenant, expected) in new[] { (Tenant, modern), (2, legacy), (Tenant, modern), (2, legacy) })
        {
            var doc = Document(type); doc.EmpresaId = tenant; doc.Empresa.Id = tenant;
            var result = generator.Generar(doc);
            result.IsSuccess.Should().BeTrue(result.Error);
            using var json = JsonDocument.Parse(result.Value!);
            json.RootElement.GetProperty("identificacion").GetProperty("version").GetInt32().Should().Be(expected);
        }
    }

    [Theory]
    [InlineData("Nit", "00000000000000")]
    [InlineData("Nit", "invalid")]
    [InlineData("Nit", null)]
    [InlineData("Ambiente", "PRODUCCION")]
    [InlineData("Ambiente", "pruebas")]
    [InlineData("Profile", "UNKNOWN")]
    [InlineData("Profile", null)]
    [InlineData("Unexpected", "true")]
    public async Task InvalidTenantPolicyStopsOrdinaryGenerationWithoutPersistingJson(string field, string? value)
    {
        await using var db = DteEmisorSaneamientoTests.CreateDb();
        var doc = Document("01");
        db.DteDocumentos.Add(doc); db.DteConfiguracion.Add(new() { EmpresaId = Tenant }); await db.SaveChangesAsync();
        var values = Values(); values["Dte:TenantSchemas:23:" + field] = value;
        var configuration = Config(values);
        var service = DteEmisorSaneamientoTests.CreateService(db, Generator(configuration), Lookup(), configuration);
        var result = await service.GenerarAsync(Tenant, doc.Id, "synthetic");
        result.ErrorCode.Should().Be("DTE_SCHEMA_POLICY_INVALID");
        Generator(configuration).Generar(doc).ErrorCode.Should().Be("DTE_SCHEMA_POLICY_INVALID");
        db.ChangeTracker.Clear();
        (await db.DteDocumentos.Include(d => d.Json).SingleAsync()).Json.Should().BeNull();
        var unconfigured = Document("01"); unconfigured.EmpresaId = 2; unconfigured.Empresa.Id = 2;
        Generator(configuration).Generar(unconfigured).IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("Dte:TenantSchemas", "true")]
    [InlineData("Dte:TenantSchemas:023:Profile", "MH_20260825")]
    [InlineData("Dte:TenantSchemas:abc:Profile", "MH_20260825")]
    [InlineData("Dte:TenantSchemas:23:Nit:Unexpected", "value")]
    public void MalformedStructureCannotSilentlyUseGlobalFallback(string key, string value)
    {
        var values = Values(); values[key] = value;
        Generator(Config(values)).Generar(Document("01")).ErrorCode.Should().Be("DTE_SCHEMA_POLICY_INVALID");
    }

    [Fact]
    public void PolicySnapshotDoesNotChangeMidRequestAndAcceptsStoredFormattedNit()
    {
        var config = Config(); var generator = Generator(config);
        config["Dte:TenantSchemas:23:Profile"] = "invalid";
        var document = Document("01"); document.Empresa.Nit = "0614-010100-101-1";
        generator.Generar(document).IsSuccess.Should().BeTrue();
        Generator(config).Generar(document).ErrorCode.Should().Be("DTE_SCHEMA_POLICY_INVALID");
    }

    [Fact]
    public async Task TenantProfileRequiresDistrictInOrdinaryGeneration()
    {
        await using var db = DteEmisorSaneamientoTests.CreateDb();
        var doc = Document("01"); doc.Empresa.Distrito = null;
        db.DteDocumentos.Add(doc); db.DteConfiguracion.Add(new() { EmpresaId = Tenant }); await db.SaveChangesAsync();
        var configuration = Config();
        var result = await DteEmisorSaneamientoTests.CreateService(db, Generator(configuration), Lookup(), configuration)
            .GenerarAsync(Tenant, doc.Id, "synthetic");
        result.ErrorCode.Should().Be("DTE_TERRITORIO_DISTRITO");
        (await db.DteDocumentos.Include(x => x.Json).SingleAsync()).Json.Should().BeNull();
    }

    private static DteGeneratorService Generator(IConfiguration configuration)
        => new(Options.Create(new TerritorialOptions()), configuration);

    [Fact]
    public async Task InvalidPolicyBlocksOrdinaryCreateBeforeAllocatingOrPersisting()
    {
        await using var db = DteEmisorSaneamientoTests.CreateDb();
        db.Empresas.Add(Document("01").Empresa); db.DteConfiguracion.Add(new() { EmpresaId = Tenant });
        await db.SaveChangesAsync();
        var values = Values(); values["Dte:TenantSchemas:23:Profile"] = "UNKNOWN";
        var config = Config(values);
        var result = await DteEmisorSaneamientoTests.CreateService(db, Generator(config), Lookup(), config)
            .CreateBorradorAsync(Tenant, DteIdempotencyTests.Request(), "synthetic");
        result.ErrorCode.Should().Be("DTE_SCHEMA_POLICY_INVALID");
        (await db.DteDocumentos.CountAsync()).Should().Be(0);
        (await db.DteCorrelativos.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task OrdinaryCreateUsesTenantProfileToRequireReceiverDistrict()
    {
        await using var db = DteEmisorSaneamientoTests.CreateDb();
        db.Empresas.Add(Document("01").Empresa); db.DteConfiguracion.Add(new() { EmpresaId = Tenant });
        await db.SaveChangesAsync();
        var config = Config(); var request = DteIdempotencyTests.Request();
        request.ReceptorManual = new() { Nombre = "Synthetic receiver", DepartamentoCodigo = "05", MunicipioCodigo = "22" };
        var result = await DteEmisorSaneamientoTests.CreateService(db, Generator(config), Lookup(), config)
            .CreateBorradorAsync(Tenant, request, "synthetic");
        result.ErrorCode.Should().Be("DTE_TERRITORIO_DISTRITO");
        (await db.DteDocumentos.CountAsync()).Should().Be(0);
    }
    private static ILookupService Lookup()
    {
        var lookup = DteEmisorSaneamientoTests.Lookup();
        lookup.GetCatalogoAsync(CatalogCodes.DistritoEs, Tenant, null, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<LookupItem>>([new("DISTRICT", "Distrito emisor", "LA_LIBERTAD_CENTRO", "{\"codigoMH\":\"15\"}")]));
        return lookup;
    }
    private static DteDocumento Document(string type)
    {
        var doc = DteEmisorSaneamientoTests.Documento(); doc.TipoDteCodigo = type;
        doc.NumeroControl = doc.NumeroControl.Replace("DTE-01-", $"DTE-{type}-");
        doc.Empresa = new Empresa { Id = Tenant, Nit = Nit, RazonSocial = "Synthetic issuer", Departamento = "05", Municipio = "22", Distrito = "15",
            Direccion = "Synthetic address", Telefono = "22220000", Correo = "synthetic@example.invalid" };
        doc.ReceptorDepartamentoCodigo = "05"; doc.ReceptorMunicipioCodigo = "22"; doc.ReceptorDistritoCodigo = "15";
        doc.ReceptorNumeroDocumento = "00000000000000"; doc.ReceptorPaisCodigo = "US"; doc.ReceptorPaisNombre = "United States"; doc.ReceptorTipoPersona = 2;
        return doc;
    }
}
