using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using NeoSTP.Domain.Core.Seguridad;
using NeoSTP.Infrastructure.Persistence;
using Xunit;

namespace NeoSTP.Tests.Unit.Seguridad;

/// <summary>
/// Guardia contra permisos huérfanos: sembrar un permiso y olvidar otorgarlo a algún rol
/// ya pasó tres veces (NeoAgenda 422/423, Compras.Aprobar 424, webhooks 353-355). El
/// síntoma es silencioso — la pantalla existe, el permiso existe, y nadie puede entrar
/// salvo el SuperAdmin, que evade la validación y por eso nunca lo nota en pruebas.
/// </summary>
public class PermisosOtorgadosTests
{
    private static NeoStpDbContext NewDb() => new(
        new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"perm-{Guid.NewGuid()}").Options);

    /// <summary>
    /// El modelo de runtime no conserva el seed (está optimizado para lectura); hay que
    /// pedir el de diseño, que es donde vive <c>HasData</c>.
    /// </summary>
    private static IModel ModeloConSeed(NeoStpDbContext db)
        => db.GetService<IDesignTimeModel>().Model;

    /// <summary>Lee el seed del modelo, que es la fuente de verdad de permisos y roles.</summary>
    private static (List<Permiso> Permisos, HashSet<int> Otorgados) LeerSeed()
    {
        using var db = NewDb();
        var modelo = ModeloConSeed(db);

        var permisos = modelo.FindEntityType(typeof(Permiso))!.GetSeedData()
            .Select(d => new Permiso
            {
                Id = (int)d[nameof(Permiso.Id)]!,
                Codigo = (string)d[nameof(Permiso.Codigo)]!,
                Modulo = (string)d[nameof(Permiso.Modulo)]!,
            })
            .ToList();

        var otorgados = modelo.FindEntityType(typeof(RolPermiso))!.GetSeedData()
            .Select(d => (int)d[nameof(RolPermiso.PermisoId)]!)
            .ToHashSet();

        return (permisos, otorgados);
    }

    [Fact]
    public void TodoPermisoSembrado_EstaOtorgadoAAlgunRol()
    {
        var (permisos, otorgados) = LeerSeed();

        var huerfanos = permisos.Where(p => !otorgados.Contains(p.Id))
            .Select(p => $"{p.Id} {p.Codigo} ({p.Modulo})")
            .ToList();

        huerfanos.Should().BeEmpty(
            "un permiso que ningún rol tiene deja su pantalla inalcanzable para todos menos el " +
            "SuperAdmin, que evade la validación y por eso el hueco pasa desapercibido");
    }

    [Fact]
    public void SuperAdmin_TieneTodosLosPermisos()
    {
        using var db = NewDb();
        var modelo = ModeloConSeed(db);

        var permisos = modelo.FindEntityType(typeof(Permiso))!.GetSeedData()
            .Select(d => (int)d[nameof(Permiso.Id)]!).ToHashSet();
        var delSuperAdmin = modelo.FindEntityType(typeof(RolPermiso))!.GetSeedData()
            .Where(d => (int)d[nameof(RolPermiso.RolId)]! == 500)
            .Select(d => (int)d[nameof(RolPermiso.PermisoId)]!).ToHashSet();

        permisos.Except(delSuperAdmin).Should().BeEmpty(
            "el SuperAdmin evade la validación en runtime, pero su rol debe reflejar el " +
            "catálogo completo para que el seed sea coherente y auditable");
    }

    [Fact]
    public void PermisosDeIntegracion_LosTieneElAdminDeLaEmpresa()
    {
        using var db = NewDb();
        var delAdmin = ModeloConSeed(db).FindEntityType(typeof(RolPermiso))!.GetSeedData()
            .Where(d => (int)d[nameof(RolPermiso.RolId)]! == 501)
            .Select(d => (int)d[nameof(RolPermiso.PermisoId)]!).ToHashSet();

        // 351-355: API keys, webhooks y logs de NeoConnect. Es lo que compra un plan
        // Integrador API o Enterprise; el administrador del cliente debe poder usarlo.
        delAdmin.Should().Contain([351, 352, 353, 354, 355]);
    }

    [Fact]
    public void NoHayPermisosDuplicados()
    {
        var (permisos, _) = LeerSeed();

        permisos.Select(p => p.Id).Should().OnlyHaveUniqueItems();
        permisos.Select(p => p.Codigo).Should().OnlyHaveUniqueItems();
    }
}
