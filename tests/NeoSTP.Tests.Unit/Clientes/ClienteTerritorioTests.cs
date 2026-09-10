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
        var distritos = new Catalogo { Id = 704, Codigo = "DISTRITO_ES", Nombre = "Distritos", EsSistema = true };
        db.Catalogos.AddRange(paises, departamentos, municipios, distritos);
        db.CatalogoItems.AddRange(
            new CatalogoItem { CatalogoId = paises.Id, Codigo = "US", Valor = "Estados Unidos", Activo = true },
            new CatalogoItem { CatalogoId = distritos.Id, Codigo = "SAN_SALVADOR", Valor = "San Salvador", ParentCodigo = "SAN_SALVADOR_CENTRO", Activo = true },
            new CatalogoItem { CatalogoId = distritos.Id, Codigo = "SANTA_TECLA", Valor = "Santa Tecla", ParentCodigo = "LA_LIBERTAD_SUR", Activo = true },
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
    public async Task District_RoundTrips_AndOldApiUpdateDoesNotEraseIt()
    {
        await using var db = NewDb();
        var service = NewService(db);
        var request = Request("SAN_SALVADOR", "SAN_SALVADOR_CENTRO");
        request.DistritoCodigo = " SAN_SALVADOR ";
        var created = await service.CreateAsync(EmpresaId, request, "test");
        created.IsSuccess.Should().BeTrue(created.Error);
        created.Value!.DistritoCodigo.Should().Be("SAN_SALVADOR");
        (await service.GetByIdAsync(EmpresaId, created.Value.Id)).Value!.DistritoCodigo.Should().Be("SAN_SALVADOR");
        var update = new UpdateClienteRequest { TipoDocumentoCodigo="DUI", NumeroDocumento="12345678-9", Nombre="Editado", PaisCodigo="SV", DepartamentoCodigo="SAN_SALVADOR", MunicipioCodigo="SAN_SALVADOR_CENTRO" };
        var result = await service.UpdateAsync(EmpresaId, created.Value.Id, update, "test");
        result.Value!.DistritoCodigo.Should().Be("SAN_SALVADOR");
        (await service.UpdateAsync(999, created.Value.Id, update, "test")).ErrorCode.Should().Be("CLIENTE_NOT_FOUND");
        update.DistritoCodigo = "SANTA_TECLA";
        (await service.UpdateAsync(EmpresaId, created.Value.Id, update, "test")).ErrorCode.Should().Be("VALIDATION");
        (await service.GetByIdAsync(EmpresaId, created.Value.Id)).Value!.DistritoCodigo.Should().Be("SAN_SALVADOR");
        update.DistritoCodigo = "";
        (await service.UpdateAsync(EmpresaId, created.Value.Id, update, "test")).Value!.DistritoCodigo.Should().BeNull();
    }

    [Theory]
    [InlineData("SANTA_TECLA")]
    [InlineData("UNKNOWN")]
    public async Task Create_RejectsDistrictOutsideSelectedMunicipality(string district)
    {
        await using var db = NewDb();
        var request = Request("SAN_SALVADOR", "SAN_SALVADOR_CENTRO");
        request.DistritoCodigo=district;
        (await NewService(db).CreateAsync(EmpresaId, request, "test")).ErrorCode.Should().Be("VALIDATION");
        (await db.Clientes.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ForeignClient_DropsAllSalvadoranTerritory()
    {
        await using var db = NewDb();
        var request = Request("SAN_SALVADOR", "SAN_SALVADOR_CENTRO");
        request.DistritoCodigo="SAN_SALVADOR";
        var service=NewService(db);
        var created=await service.CreateAsync(EmpresaId,request,"test");
        var update=new UpdateClienteRequest {Nombre="Extranjero",TipoDocumentoCodigo="DUI",NumeroDocumento="12345678-9",PaisCodigo="US",DepartamentoCodigo="SAN_SALVADOR",MunicipioCodigo="SAN_SALVADOR_CENTRO",DistritoCodigo="SAN_SALVADOR"};
        var result=await service.UpdateAsync(EmpresaId,created.Value!.Id,update,"test");
        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.DepartamentoCodigo.Should().BeNull();
        result.Value.MunicipioCodigo.Should().BeNull();
        result.Value.DistritoCodigo.Should().BeNull();
    }

    [Fact]
    public async Task ParentChangeWithoutDistrictDoesNotReuseOldDistrict()
    {
        await using var db = NewDb();
        var service=NewService(db);
        var request=Request("SAN_SALVADOR","SAN_SALVADOR_CENTRO"); request.DistritoCodigo="SAN_SALVADOR";
        var created=await service.CreateAsync(EmpresaId,request,"test");
        var update=new UpdateClienteRequest {Nombre="Traslado",TipoDocumentoCodigo="DUI",NumeroDocumento="12345678-9",PaisCodigo="SV",DepartamentoCodigo="LA_LIBERTAD",MunicipioCodigo="LA_LIBERTAD_SUR"};
        (await service.UpdateAsync(EmpresaId,created.Value!.Id,update,"test")).Value!.DistritoCodigo.Should().BeNull();
    }

    [Fact]
    public async Task Import_PersistsDistrictAndRejectsInvalidRelationship()
    {
        await using var db = NewDb();
        var csv="TipoDocumento,NumeroDocumento,Nombre,Departamento,Municipio,Distrito\nDUI,12345678-9,Correcto,SAN_SALVADOR,SAN_SALVADOR_CENTRO,SAN_SALVADOR\nDUI,87654321-0,Incorrecto,SAN_SALVADOR,SAN_SALVADOR_CENTRO,SANTA_TECLA\n";
        using var content=new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));
        var result=await NewService(db).ImportAsync(EmpresaId,new(){Format=NeoSTP.Application.Common.BulkFileFormat.Csv,Content=content},"test");
        result.Value!.Inserted.Should().Be(1);
        result.Value.ErrorCount.Should().Be(1);
        (await db.Clientes.SingleAsync()).DistritoCodigo.Should().Be("SAN_SALVADOR");
    }

    [Theory]
    [InlineData(null, 1)]
    [InlineData("SV", 1)]
    [InlineData("US", 0)]
    public void WebRequiresDistrictForLocalMunicipality(string? country, int errors)
    {
        var model=new CreateClienteViewModel {PaisCodigo=country,DepartamentoCodigo="LA_LIBERTAD",MunicipioCodigo="LA_LIBERTAD_SUR"};
        model.Validate(new System.ComponentModel.DataAnnotations.ValidationContext(model)).Count().Should().Be(errors);
    }

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
