using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Application.Dte.Dtos;
using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Dte;

public sealed class DteConfiguracionHistoryTests
{
    private static NeoStpDbContext Db(string name) => new(
        new DbContextOptionsBuilder<NeoStpDbContext>().UseInMemoryDatabase(name).Options);

    private static Empresa Empresa(int id) => new()
    {
        Id = id,
        Nit = $"0614{id:D10}",
        RazonSocial = $"Empresa {id}",
        EstadoCodigo = EstadoCodes.Activo,
    };

    private static DteConfiguracionService Service(NeoStpDbContext db) => new(
        db,
        DteFiscalIsolationTests.Protector(),
        Substitute.For<IHaciendaAuthClient>(),
        Substitute.For<IAuditoriaService>());

    [Fact]
    public async Task Guardar_CreaVersionConSecretosCifrados_YDtoNoLosExpone()
    {
        await using var db = Db(nameof(Guardar_CreaVersionConSecretosCifrados_YDtoNoLosExpone));
        db.Empresas.Add(Empresa(2));
        db.DteConfiguracion.Add(new DteConfiguracion
        {
            EmpresaId = 2,
            AmbienteCodigo = "PRODUCCION",
            UsuarioMh = "06140000000000",
            PasswordMhCifrado = "CIFRADO-MH",
            TipoEstablecimientoCodigo = "02",
            CodigoEstablecimientoMh = "M001",
            CodigoPuntoVentaMh = "P001",
            CertificadoBlob = [1, 2, 3],
            CertificadoNombre = "actual.crt",
            PasswordCertificadoCifrado = "CIFRADO-CERT",
        });
        await db.SaveChangesAsync();

        var result = await Service(db).SaveAsync(2, new SaveDteConfiguracionRequest
        {
            AmbienteCodigo = "PRODUCCION",
            UsuarioMh = "06140000000000",
            TipoEstablecimientoCodigo = "02",
            CodigoEstablecimientoMh = "M002",
            CodigoPuntoVentaMh = "P002",
        }, "admin");

        result.IsSuccess.Should().BeTrue();
        var version = await db.DteConfiguracionVersiones.SingleAsync();
        version.EmpresaId.Should().Be(2);
        version.PasswordMhCifrado.Should().Be("CIFRADO-MH");
        version.PasswordCertificadoCifrado.Should().Be("CIFRADO-CERT");
        version.CertificadoBlob.Should().Equal(1, 2, 3);

        var visible = (await Service(db).GetVersionesAsync(2)).Value!.Single();
        visible.TieneCertificado.Should().BeTrue();
        visible.CertificadoNombre.Should().Be("actual.crt");
    }

    [Fact]
    public async Task Recuperar_RestauraSoloVersionDeLaEmpresa_EInvalidaToken()
    {
        await using var db = Db(nameof(Recuperar_RestauraSoloVersionDeLaEmpresa_EInvalidaToken));
        db.Empresas.AddRange(Empresa(2), Empresa(23));
        var config = new DteConfiguracion
        {
            EmpresaId = 2,
            AmbienteCodigo = "PRODUCCION",
            UsuarioMh = "actual",
            PasswordMhCifrado = "actual-pwd",
            TipoEstablecimientoCodigo = "02",
            CodigoEstablecimientoMh = "M999",
            CodigoPuntoVentaMh = "P999",
            TokenMhCifrado = "token-viejo",
            TokenMhExpiraAt = DateTime.UtcNow.AddHours(1),
        };
        var configDaniel = new DteConfiguracion { EmpresaId = 23, AmbienteCodigo = "PRODUCCION" };
        db.DteConfiguracion.AddRange(config, configDaniel);
        await db.SaveChangesAsync();
        var propia = new DteConfiguracionVersion
        {
            EmpresaId = 2,
            ConfiguracionId = config.Id,
            Motivo = "TEST",
            AmbienteCodigo = "PRODUCCION",
            UsuarioMh = "restaurado",
            PasswordMhCifrado = "restaurado-pwd",
            TipoEstablecimientoCodigo = "02",
            CodigoEstablecimientoMh = "M001",
            CodigoPuntoVentaMh = "P001",
            CertificadoBlob = [9, 8, 7],
            CertificadoNombre = "restaurado.crt",
        };
        var ajena = new DteConfiguracionVersion
        {
            EmpresaId = 23,
            ConfiguracionId = configDaniel.Id,
            Motivo = "TEST",
            AmbienteCodigo = "PRODUCCION",
        };
        db.DteConfiguracionVersiones.AddRange(propia, ajena);
        await db.SaveChangesAsync();

        var service = Service(db);
        (await service.RecuperarVersionAsync(2, ajena.Id, "admin")).ErrorCode
            .Should().Be("VERSION_NOT_FOUND");

        var result = await service.RecuperarVersionAsync(2, propia.Id, "admin");

        result.IsSuccess.Should().BeTrue();
        config.UsuarioMh.Should().Be("restaurado");
        config.CodigoEstablecimientoMh.Should().Be("M001");
        config.CertificadoNombre.Should().Be("restaurado.crt");
        config.TokenMhCifrado.Should().BeNull();
        config.TokenMhExpiraAt.Should().BeNull();
        (await db.DteConfiguracionVersiones.CountAsync(x => x.EmpresaId == 2)).Should().Be(2);
    }
}
