using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Domain.Core.Clientes;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using Xunit;

namespace NeoSTP.Tests.Unit.Lookups;

/// <summary>
/// B-6: verificación de NIT/DUI — formato salvadoreño + autocompletado local.
/// </summary>
public class NitVerificationServiceTests
{
    private const int EmpresaA = 70;
    private const int EmpresaB = 71;

    private static NeoStpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"nit-{Guid.NewGuid()}")
            .Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.Add(new Empresa { Id = EmpresaA, Nit = "A", RazonSocial = "A", EstadoCodigo = "ACTIVA" });
        db.Empresas.Add(new Empresa { Id = EmpresaB, Nit = "B", RazonSocial = "B", EstadoCodigo = "ACTIVA" });
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task Nit14Digitos_FormatoValido_Normaliza()
    {
        var svc = new NitVerificationService(NewDb());

        var r = await svc.VerificarAsync(EmpresaA, "06140101010012");

        r.FormatoValido.Should().BeTrue();
        r.TipoDocumento.Should().Be("NIT");
        r.DocumentoNormalizado.Should().Be("0614-010101-001-2");
    }

    [Fact]
    public async Task Dui9Digitos_FormatoValido()
    {
        var svc = new NitVerificationService(NewDb());

        var r = await svc.VerificarAsync(EmpresaA, "01234567-8");

        r.FormatoValido.Should().BeTrue();
        r.TipoDocumento.Should().Be("DUI");
        r.DocumentoNormalizado.Should().Be("01234567-8");
    }

    [Fact]
    public async Task FormatoInvalido()
    {
        var svc = new NitVerificationService(NewDb());

        var r = await svc.VerificarAsync(EmpresaA, "123");

        r.FormatoValido.Should().BeFalse();
        r.TipoDocumento.Should().Be("DESCONOCIDO");
    }

    [Fact]
    public async Task EncuentraClienteLocal_YAutocompleta()
    {
        var db = NewDb();
        db.Clientes.Add(new Cliente
        {
            EmpresaId = EmpresaA, TipoDocumentoCodigo = "NIT", NumeroDocumento = "0614-010101-001-2",
            Nombre = "Cliente Demo", Nrc = "12345-6", TipoContribuyenteCodigo = "CONTRIBUYENTE", EstadoCodigo = "ACTIVO",
        });
        await db.SaveChangesAsync();
        var svc = new NitVerificationService(db);

        var r = await svc.VerificarAsync(EmpresaA, "06140101010012");

        r.EncontradoLocal.Should().BeTrue();
        r.Nombre.Should().Be("Cliente Demo");
        r.Nrc.Should().Be("12345-6");
        r.Fuente.Should().Be("LOCAL");
    }

    [Fact]
    public async Task ClienteDeOtraEmpresa_NoSeEncuentra()
    {
        var db = NewDb();
        db.Clientes.Add(new Cliente
        {
            EmpresaId = EmpresaB, TipoDocumentoCodigo = "NIT", NumeroDocumento = "0614-010101-001-2",
            Nombre = "Ajeno", TipoContribuyenteCodigo = "CONTRIBUYENTE", EstadoCodigo = "ACTIVO",
        });
        await db.SaveChangesAsync();
        var svc = new NitVerificationService(db);

        var r = await svc.VerificarAsync(EmpresaA, "06140101010012");

        r.FormatoValido.Should().BeTrue();
        r.EncontradoLocal.Should().BeFalse();
    }
}
