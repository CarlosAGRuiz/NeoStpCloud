using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Domain.Core.Clientes;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Datos;

/// <summary>Etiquetas CRM del cliente (VIP/FRECUENTE) — follow-up B-2.</summary>
public class ClienteEtiquetaTests
{
    private const int EmpresaA = 80;

    private static (ClientesService svc, NeoStpDbContext db) NewSvc()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>().UseInMemoryDatabase($"etq-{Guid.NewGuid()}").Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.Add(new Empresa { Id = EmpresaA, Nit = "A", RazonSocial = "A", EstadoCodigo = "ACTIVA" });
        db.Clientes.Add(new Cliente
        {
            Id = 1, EmpresaId = EmpresaA, TipoDocumentoCodigo = "DUI", NumeroDocumento = "1-1",
            Nombre = "C", TipoContribuyenteCodigo = "CONSUMIDOR_FINAL", EstadoCodigo = "ACTIVO",
        });
        db.SaveChanges();
        return (new ClientesService(db, Substitute.For<IAuditoriaService>()), db);
    }

    [Fact]
    public async Task SetEtiqueta_Vip_Normaliza()
    {
        var (svc, db) = NewSvc();
        (await svc.SetEtiquetaAsync(EmpresaA, 1, "vip", "tester")).IsSuccess.Should().BeTrue();
        (await db.Clientes.AsNoTracking().FirstAsync()).Etiqueta.Should().Be("VIP");
    }

    [Fact]
    public async Task SetEtiqueta_Vacia_LaQuita()
    {
        var (svc, db) = NewSvc();
        await svc.SetEtiquetaAsync(EmpresaA, 1, "FRECUENTE", "tester");
        (await svc.SetEtiquetaAsync(EmpresaA, 1, null, "tester")).IsSuccess.Should().BeTrue();
        (await db.Clientes.AsNoTracking().FirstAsync()).Etiqueta.Should().BeNull();
    }

    [Fact]
    public async Task SetEtiqueta_Invalida_Validation()
    {
        var (svc, _) = NewSvc();
        (await svc.SetEtiquetaAsync(EmpresaA, 1, "MOROSO", "tester")).ErrorCode.Should().Be("VALIDATION");
    }

    [Fact]
    public async Task SetEtiqueta_Inexistente_NotFound()
    {
        var (svc, _) = NewSvc();
        (await svc.SetEtiquetaAsync(EmpresaA, 999, "VIP", "tester")).ErrorCode.Should().Be("CLIENTE_NOT_FOUND");
    }
}
