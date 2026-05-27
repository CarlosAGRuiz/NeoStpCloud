using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Empresas;
using NeoSTP.Application.Empresas.Dtos;
using NeoSTP.Application.Common;
using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Empresas;

public class SucursalesLimitTests
{
    private static (SucursalesService svc, NeoStpDbContext db, ILicenciaResolver lic) Build(int sucursalesUsadas, int? limite, bool vigente = true)
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"limit-{Guid.NewGuid()}")
            .Options;
        var db = new NeoStpDbContext(options);

        db.Empresas.Add(new Empresa
        {
            Id = 1, Nit = "0000-1", RazonSocial = "Demo", EstadoCodigo = "ACTIVA",
            CreatedAt = DateTime.UtcNow,
        });
        for (var i = 0; i < sucursalesUsadas; i++)
        {
            db.Sucursales.Add(new Sucursal
            {
                EmpresaId = 1, Codigo = $"S{i:00}", Nombre = $"S{i}",
                EstadoCodigo = EstadoCodes.Activo, CreatedAt = DateTime.UtcNow,
            });
        }
        db.SaveChanges();

        var lic = Substitute.For<ILicenciaResolver>();
        lic.ResolveAsync(1, Arg.Any<CancellationToken>()).Returns(new LicenciaDto
        {
            EmpresaId = 1, EmpresaNombre = "Demo", EmpresaEstado = "ACTIVA",
            PlanCodigo = "PYME", Vigente = vigente,
            LimiteSucursales = limite, SucursalesUsadas = sucursalesUsadas,
        });

        var audit = Substitute.For<IAuditoriaService>();
        return (new SucursalesService(db, lic, audit), db, lic);
    }

    [Fact]
    public async Task CreateAsync_ReturnsLimitExceeded_WhenAtLimit()
    {
        var (svc, _, _) = Build(sucursalesUsadas: 1, limite: 1);

        var result = await svc.CreateAsync(1, new CreateSucursalRequest { Codigo = "SUC02", Nombre = "Centro" }, "tester");

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("LIMIT_EXCEEDED");
        result.Error.Should().Contain("PYME").And.Contain("1");
    }

    [Fact]
    public async Task CreateAsync_Succeeds_WhenBelowLimit()
    {
        var (svc, db, _) = Build(sucursalesUsadas: 0, limite: 3);

        var result = await svc.CreateAsync(1, new CreateSucursalRequest { Codigo = "SUC01", Nombre = "Matriz" }, "tester");

        result.IsSuccess.Should().BeTrue();
        result.Value!.Codigo.Should().Be("SUC01");
        (await db.Sucursales.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_ReturnsLicenseInvalid_WhenLicenseNotVigente()
    {
        var (svc, _, _) = Build(sucursalesUsadas: 0, limite: 3, vigente: false);

        var result = await svc.CreateAsync(1, new CreateSucursalRequest { Codigo = "X", Nombre = "X" }, "tester");

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("LICENSE_INVALID");
    }

    [Fact]
    public async Task CreateAsync_AllowsCreation_WhenLimitIsNull()
    {
        // limite null = ilimitado
        var (svc, _, _) = Build(sucursalesUsadas: 100, limite: null);

        var result = await svc.CreateAsync(1, new CreateSucursalRequest { Codigo = "SUC101", Nombre = "x" }, "tester");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_RejectsDuplicateCodigo()
    {
        var (svc, _, _) = Build(sucursalesUsadas: 0, limite: 5);
        var ok = await svc.CreateAsync(1, new CreateSucursalRequest { Codigo = "SUC01", Nombre = "x" }, "tester");
        ok.IsSuccess.Should().BeTrue();

        var dup = await svc.CreateAsync(1, new CreateSucursalRequest { Codigo = "suc01", Nombre = "y" }, "tester");
        dup.IsFailure.Should().BeTrue();
        dup.ErrorCode.Should().Be("SUCURSAL_DUPLICATE");
    }
}
