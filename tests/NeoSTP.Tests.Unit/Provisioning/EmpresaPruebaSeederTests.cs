using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Provisioning;
using NeoSTP.Domain.Core.Licenciamiento;
using NeoSTP.Domain.Core.Seguridad;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Persistence.Seed;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Provisioning;

public class EmpresaPruebaSeederTests
{
    private static ServiceProvider BuildProvider(string dbName, EmpresaPruebaOptions opts)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<NeoStpDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddSingleton(Options.Create(opts));

        var hasher = Substitute.For<IPasswordHasher>();
        hasher.Hash(Arg.Any<string>()).Returns(ci => "HASHED::" + ci.Arg<string>());
        services.AddSingleton(hasher);

        return services.BuildServiceProvider();
    }

    private static EmpresaPruebaOptions DefaultOpts() => new()
    {
        Enabled = true,
        Nit = "06140000000000",
        Nrc = "000000-0",
        RazonSocial = "NeoSTP Pruebas, S.A. de C.V.",
        PlanCodigo = "ENTERPRISE",
        Admin = new() { Username = "admin.prueba", Password = "ChangeMe!2026" },
        Sucursal = new() { Codigo = "0001", Nombre = "Casa Matriz" },
        PuntoVenta = new() { Codigo = "0001", Nombre = "Principal" },
        Dte = new() { AmbienteCodigo = "PRUEBAS", UsuarioMh = "06140000000000" },
    };

    /// <summary>Siembra el plan ENTERPRISE con 2 módulos y el rol ADMIN en la BD InMemory.</summary>
    private static async Task SeedPlanYRolAsync(IServiceProvider sp)
    {
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NeoStpDbContext>();

        db.Modulos.AddRange(
            new Modulo { Id = 100, Codigo = "CORE", Nombre = "Core", Activo = true, CreatedAt = DateTime.UtcNow },
            new Modulo { Id = 101, Codigo = "NEODTE", Nombre = "NeoDTE", Activo = true, CreatedAt = DateTime.UtcNow });
        db.Planes.Add(new Plan
        {
            Id = 204, Codigo = "ENTERPRISE", Nombre = "Enterprise", PrecioMensual = 400m,
            Activo = true, CreatedAt = DateTime.UtcNow,
            Modulos = { new PlanModulo { PlanId = 204, ModuloId = 100, Activo = true },
                        new PlanModulo { PlanId = 204, ModuloId = 101, Activo = true } },
        });
        db.Roles.Add(new Rol { Id = 501, Codigo = "ADMIN", Nombre = "Administrador", EsSistema = true, Activo = true, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Deshabilitado_NoHaceNada()
    {
        var opts = DefaultOpts();
        opts.Enabled = false;
        var sp = BuildProvider(nameof(Deshabilitado_NoHaceNada), opts);
        await SeedPlanYRolAsync(sp);

        await EmpresaPruebaSeeder.SeedAsync(sp);

        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NeoStpDbContext>();
        (await db.Empresas.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Habilitado_CreaEmpresaCompleta()
    {
        var sp = BuildProvider(nameof(Habilitado_CreaEmpresaCompleta), DefaultOpts());
        await SeedPlanYRolAsync(sp);

        await EmpresaPruebaSeeder.SeedAsync(sp);

        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NeoStpDbContext>();

        var empresa = await db.Empresas.SingleAsync();
        empresa.Nit.Should().Be("06140000000000");
        empresa.RazonSocial.Should().Be("NeoSTP Pruebas, S.A. de C.V.");

        (await db.EmpresaPlanes.CountAsync(p => p.EmpresaId == empresa.Id)).Should().Be(1);
        (await db.EmpresaModulos.CountAsync(m => m.EmpresaId == empresa.Id)).Should().Be(2);
        (await db.Sucursales.CountAsync(s => s.EmpresaId == empresa.Id)).Should().Be(1);

        var admin = await db.Usuarios.SingleAsync(u => u.EmpresaId == empresa.Id);
        admin.Username.Should().Be("admin.prueba");
        admin.TipoUsuarioCodigo.Should().Be("ADMIN");
        admin.PasswordHash.Should().StartWith("HASHED::");

        (await db.UsuarioRoles.CountAsync(ur => ur.UsuarioId == admin.Id)).Should().Be(1);

        var config = await db.DteConfiguracion.SingleAsync(c => c.EmpresaId == empresa.Id);
        config.AmbienteCodigo.Should().Be("PRUEBAS");
        config.PasswordMhCifrado.Should().BeNull("los secretos se cargan vía UI, no por el seeder");
        config.CertificadoBlob.Should().BeNull();
    }

    [Fact]
    public async Task Idempotente_NoDuplicaEnSegundaCorrida()
    {
        var sp = BuildProvider(nameof(Idempotente_NoDuplicaEnSegundaCorrida), DefaultOpts());
        await SeedPlanYRolAsync(sp);

        await EmpresaPruebaSeeder.SeedAsync(sp);
        await EmpresaPruebaSeeder.SeedAsync(sp);   // segunda corrida

        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NeoStpDbContext>();
        (await db.Empresas.CountAsync()).Should().Be(1);
        (await db.Usuarios.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task PlanInexistente_NoCreaEmpresa()
    {
        var opts = DefaultOpts();
        opts.PlanCodigo = "NO_EXISTE";
        var sp = BuildProvider(nameof(PlanInexistente_NoCreaEmpresa), opts);
        await SeedPlanYRolAsync(sp);

        await EmpresaPruebaSeeder.SeedAsync(sp);

        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NeoStpDbContext>();
        (await db.Empresas.CountAsync()).Should().Be(0);
    }
}
