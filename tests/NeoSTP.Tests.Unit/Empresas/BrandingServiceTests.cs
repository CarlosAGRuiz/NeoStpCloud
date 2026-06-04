using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Empresas;

/// <summary>
/// Branding por empresa (logo/firma): validación de tipo/tamaño, guardado/lectura,
/// texto de firma, eliminación y aislamiento por empresa.
/// </summary>
public class BrandingServiceTests
{
    private const int EmpresaA = 20;
    private const int EmpresaB = 21;

    private static NeoStpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"branding-{Guid.NewGuid()}")
            .Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.Add(new Empresa { Id = EmpresaA, Nit = "A", RazonSocial = "A", EstadoCodigo = "ACTIVA" });
        db.Empresas.Add(new Empresa { Id = EmpresaB, Nit = "B", RazonSocial = "B", EstadoCodigo = "ACTIVA" });
        db.SaveChanges();
        return db;
    }

    private static BrandingService NewSvc(NeoStpDbContext db)
        => new(db, Substitute.For<IAuditoriaService>());

    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3, 4 };

    [Fact]
    public async Task GuardarLogo_FormatoInvalido_Validation()
    {
        var db = NewDb();
        var r = await NewSvc(db).GuardarLogoAsync(EmpresaA, Png, "application/pdf", "x.pdf", "tester");
        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("VALIDATION");
    }

    [Fact]
    public async Task GuardarLogo_DemasiadoGrande_Validation()
    {
        var db = NewDb();
        var big = new byte[1_048_577];
        var r = await NewSvc(db).GuardarLogoAsync(EmpresaA, big, "image/png", "x.png", "tester");
        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("VALIDATION");
    }

    [Fact]
    public async Task GuardarLogo_Ok_LuegoGetLogo()
    {
        var db = NewDb();
        var svc = NewSvc(db);

        var r = await svc.GuardarLogoAsync(EmpresaA, Png, "image/png", "logo.png", "tester");
        r.IsSuccess.Should().BeTrue();

        var img = await svc.GetLogoAsync(EmpresaA);
        img.Should().NotBeNull();
        img!.ContentType.Should().Be("image/png");
        img.Contenido.Should().BeEquivalentTo(Png);

        (await svc.GetAsync(EmpresaA)).TieneLogo.Should().BeTrue();
        (await svc.GetLogoAsync(EmpresaB)).Should().BeNull(); // aislamiento
    }

    [Fact]
    public async Task FirmaTexto_GuardaYRecorta()
    {
        var db = NewDb();
        var svc = NewSvc(db);

        (await svc.GuardarFirmaTextoAsync(EmpresaA, "  Firma autorizada  ", "tester")).IsSuccess.Should().BeTrue();
        (await svc.GetAsync(EmpresaA)).FirmaTexto.Should().Be("Firma autorizada");

        var largo = new string('x', 301);
        (await svc.GuardarFirmaTextoAsync(EmpresaA, largo, "tester")).ErrorCode.Should().Be("VALIDATION");
    }

    [Fact]
    public async Task EliminarLogo_DejaSinLogo()
    {
        var db = NewDb();
        var svc = NewSvc(db);
        await svc.GuardarLogoAsync(EmpresaA, Png, "image/png", "l.png", "tester");

        (await svc.EliminarLogoAsync(EmpresaA, "tester")).IsSuccess.Should().BeTrue();

        (await svc.GetAsync(EmpresaA)).TieneLogo.Should().BeFalse();
        (await svc.GetLogoAsync(EmpresaA)).Should().BeNull();
    }
}
