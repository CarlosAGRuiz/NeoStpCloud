using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Domain.Core.Ops;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Ops;

/// <summary>
/// Sprint 20.3 — lista blanca de IP del panel admin: fail-open con lista vacía,
/// coincidencia exacta y por CIDR, y operaciones CRUD con validación.
/// </summary>
public class AdminIpAllowlistServiceTests
{
    private static (AdminIpAllowlistService svc, NeoStpDbContext db) Build(Action<NeoStpDbContext>? seed = null)
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"ipallow-{Guid.NewGuid()}")
            .Options;
        var db = new NeoStpDbContext(options);
        seed?.Invoke(db);
        db.SaveChanges();
        var audit = Substitute.For<IAuditoriaService>();
        return (new AdminIpAllowlistService(db, audit), db);
    }

    [Fact]
    public async Task EstaPermitida_ListaVacia_PermiteTodo()
    {
        var (svc, _) = Build();
        (await svc.EstaPermitidaAsync("203.0.113.10")).Should().BeTrue();
    }

    [Fact]
    public async Task EstaPermitida_SoloEntradasInactivas_PermiteTodo()
    {
        var (svc, _) = Build(db =>
            db.AdminIpAllowlist.Add(new AdminIpAllowlistEntry { IpCidr = "203.0.113.10", Activo = false }));

        (await svc.EstaPermitidaAsync("198.51.100.1")).Should().BeTrue();
    }

    [Fact]
    public async Task EstaPermitida_CoincidenciaExacta()
    {
        var (svc, _) = Build(db =>
            db.AdminIpAllowlist.Add(new AdminIpAllowlistEntry { IpCidr = "203.0.113.10", Activo = true }));

        (await svc.EstaPermitidaAsync("203.0.113.10")).Should().BeTrue();
        (await svc.EstaPermitidaAsync("203.0.113.11")).Should().BeFalse();
        (await svc.EstaPermitidaAsync(null)).Should().BeFalse();
    }

    [Fact]
    public async Task EstaPermitida_RangoCidr()
    {
        var (svc, _) = Build(db =>
            db.AdminIpAllowlist.Add(new AdminIpAllowlistEntry { IpCidr = "10.0.0.0/24", Activo = true }));

        (await svc.EstaPermitidaAsync("10.0.0.55")).Should().BeTrue();
        (await svc.EstaPermitidaAsync("10.0.1.55")).Should().BeFalse();
    }

    [Fact]
    public async Task Agregar_ValidaFormato()
    {
        var (svc, _) = Build();
        (await svc.AgregarAsync("no-es-ip", null, "tester")).IsFailure.Should().BeTrue();

        var ok = await svc.AgregarAsync("192.168.1.0/24", "oficina", "tester");
        ok.IsSuccess.Should().BeTrue();
        ok.Value!.IpCidr.Should().Be("192.168.1.0/24");

        var dup = await svc.AgregarAsync("192.168.1.0/24", null, "tester");
        dup.IsFailure.Should().BeTrue();
        dup.ErrorCode.Should().Be("IP_DUPLICATE");
    }

    [Fact]
    public async Task ToggleYEliminar_FuncionanYAfectanElEnforcement()
    {
        var (svc, _) = Build();
        var add = await svc.AgregarAsync("203.0.113.10", null, "tester");
        var id = add.Value!.Id;

        (await svc.EstaPermitidaAsync("198.51.100.1")).Should().BeFalse("hay una entrada activa");

        await svc.ToggleAsync(id, activo: false, "tester");
        (await svc.EstaPermitidaAsync("198.51.100.1")).Should().BeTrue("la única entrada quedó inactiva");

        (await svc.EliminarAsync(id, "tester")).IsSuccess.Should().BeTrue();
        (await svc.ListarAsync()).Should().BeEmpty();
    }
}
