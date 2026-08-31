using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Catalogos.Dtos;
using NeoSTP.Application.Clientes.Dtos;
using NeoSTP.Domain.Core.Catalogos;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NeoSTP.Web.Models;
using NSubstitute;

namespace NeoSTP.Tests.Unit.Clientes;

public class ClienteTerritorioTests
{
    private const int EmpresaId = 77;

    private static NeoStpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"cliente-territorio-{Guid.NewGuid()}")
            .Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.Add(new Empresa { Id = EmpresaId, Nit = "E", RazonSocial = "E" });

        var paises = new Catalogo { Id = 701, Codigo = "PAIS", Nombre = "Países", EsSistema = true };
        var departamentos = new Catalogo { Id = 702, Codigo = "DEPARTAMENTO_ES", Nombre = "Departamentos", EsSistema = true };
        var municipios = new Catalogo { Id = 703, Codigo = "MUNICIPIO_ES", Nombre = "Municipios", EsSistema = true };
        db.Catalogos.AddRange(paises, departamentos, municipios);
        db.CatalogoItems.AddRange(
            new CatalogoItem { CatalogoId = paises.Id, Codigo = "SV", Valor = "El Salvador", Activo = true },
            new CatalogoItem { CatalogoId = departamentos.Id, Codigo = "SAN_SALVADOR", Valor = "San Salvador", Activo = true },
            new CatalogoItem { CatalogoId = departamentos.Id, Codigo = "LA_LIBERTAD", Valor = "La Libertad", Activo = true },
            new CatalogoItem
            {
                CatalogoId = municipios.Id, Codigo = "SAN_SALVADOR_CENTRO",
                Valor = "San Salvador Centro", ParentCodigo = "SAN_SALVADOR", Activo = true,
            },
            new CatalogoItem
            {
                CatalogoId = municipios.Id, Codigo = "LA_LIBERTAD_SUR",
                Valor = "La Libertad Sur", ParentCodigo = "LA_LIBERTAD", Activo = true,
            });
        db.SaveChanges();
        return db;
    }

    private static ClientesService NewService(NeoStpDbContext db)
        => new(db, Substitute.For<IAuditoriaService>());

    private static CreateClienteRequest Request(string? departamento, string? municipio) => new()
    {
        TipoDocumentoCodigo = "DUI",
        NumeroDocumento = "12345678-9",
        Nombre = "Cliente local",
        TipoContribuyenteCodigo = "CONSUMIDOR_FINAL",
        PaisCodigo = "SV",
        DepartamentoCodigo = departamento,
        MunicipioCodigo = municipio,
    };

    [Fact]
    public async Task Create_Local_PersisteMunicipioQuePerteneceAlDepartamento()
    {
        await using var db = NewDb();

        var result = await NewService(db).CreateAsync(
            EmpresaId, Request("SAN_SALVADOR", "SAN_SALVADOR_CENTRO"), "test");

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.DepartamentoCodigo.Should().Be("SAN_SALVADOR");
        result.Value.MunicipioCodigo.Should().Be("SAN_SALVADOR_CENTRO");
    }

    [Fact]
    public async Task Create_Local_RechazaMunicipioDeOtroDepartamento()
    {
        await using var db = NewDb();

        var result = await NewService(db).CreateAsync(
            EmpresaId, Request("SAN_SALVADOR", "LA_LIBERTAD_SUR"), "test");

        result.ErrorCode.Should().Be("VALIDATION");
        result.ValidationErrors.Should().ContainSingle(e => e.Contains("no pertenece"));
    }

    [Theory]
    [InlineData("SAN_SALVADOR", null)]
    [InlineData(null, "SAN_SALVADOR_CENTRO")]
    public async Task Create_Local_RequiereDepartamentoYMunicipioJuntos(string? departamento, string? municipio)
    {
        await using var db = NewDb();

        var result = await NewService(db).CreateAsync(EmpresaId, Request(departamento, municipio), "test");

        result.ErrorCode.Should().Be("VALIDATION");
        result.ValidationErrors.Should().ContainSingle(e => e.Contains("deben seleccionarse juntos"));
    }

    [Fact]
    public void DepartamentoDe_PriorizaParentCodigoVigente()
    {
        var municipio = new CatalogoItemDto
        {
            Codigo = "SAN_SALVADOR_CENTRO",
            Valor = "San Salvador Centro",
            ParentCodigo = "SAN_SALVADOR",
            MetadataJson = "{\"departamento\":\"LA_LIBERTAD\"}",
        };

        ClienteTerritorialUi.DepartamentoDe(municipio).Should().Be("SAN_SALVADOR");
    }

    [Theory]
    [InlineData("{\"departamento\":\"SAN_SALVADOR\"}", "SAN_SALVADOR")]
    [InlineData("json-invalido", "")]
    [InlineData(null, "")]
    public void DepartamentoDe_ConservaCompatibilidadConMetadataAntigua(string? metadata, string esperado)
    {
        var municipio = new CatalogoItemDto
        {
            Codigo = "M",
            Valor = "Municipio",
            MetadataJson = metadata,
        };

        ClienteTerritorialUi.DepartamentoDe(municipio).Should().Be(esperado);
    }
}
