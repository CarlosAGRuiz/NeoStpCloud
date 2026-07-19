using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Licenciamiento;
using NeoSTP.Application.Usuarios.Dtos;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Licenciamiento;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Licenciamiento;

/// <summary>Entrega 7 — enforcement comercial: límites de plan y empresa suspendida.</summary>
public class LicenciaGuardServiceTests
{
    private const int Empresa = 101;

    private static NeoStpDbContext NewDb(int? limiteUsuarios = 1, int? limiteDteMensual = 2, string estadoEmpresa = "ACTIVA")
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"lic-{Guid.NewGuid()}").Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.Add(new Empresa { Id = Empresa, Nit = "L", RazonSocial = "Licencia SA", EstadoCodigo = estadoEmpresa });
        db.Planes.Add(new Plan
        {
            Id = 400, Codigo = "STARTER_T", Nombre = "Starter", PrecioMensual = 15m, Activo = true,
            LimiteUsuarios = limiteUsuarios, LimiteDteMensual = limiteDteMensual,
        });
        db.EmpresaPlanes.Add(new EmpresaPlan
        {
            EmpresaId = Empresa, PlanId = 400, EstadoCodigo = "ACTIVO",
            FechaInicio = DateTime.UtcNow.AddDays(-10),
        });
        db.SaveChanges();
        LicenciaGuardService.InvalidarEstadoCache();
        return db;
    }

    private static LicenciaGuardService NewGuard(NeoStpDbContext db) => new(db);

    [Fact]
    public async Task LimiteUsuarios_Alcanzado_Bloquea()
    {
        var db = NewDb(limiteUsuarios: 1);
        db.Usuarios.Add(new NeoSTP.Domain.Core.Seguridad.Usuario
        {
            EmpresaId = Empresa, Username = "u1", Email = "u1@x.com", PasswordHash = "h",
            NombreCompleto = "U1", TipoUsuarioCodigo = "OPERADOR", EstadoCodigo = "ACTIVO",
        });
        await db.SaveChangesAsync();
        var guard = NewGuard(db);

        var r = await guard.ValidarLimiteAsync(Empresa, RecursoLimitado.Usuarios);

        r.ErrorCode.Should().Be("LIMIT_EXCEEDED");
        r.Error.Should().Contain("Starter").And.Contain("Mejora tu plan");
    }

    [Fact]
    public async Task SinPlan_OLimiteNull_EsIlimitado()
    {
        var db = NewDb(limiteUsuarios: null);
        var guard = NewGuard(db);
        (await guard.ValidarLimiteAsync(Empresa, RecursoLimitado.Usuarios)).IsSuccess.Should().BeTrue();

        // Empresa sin plan asignado tampoco se bloquea.
        (await guard.ValidarLimiteAsync(999, RecursoLimitado.Usuarios)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task LimiteDteMensual_CuentaSoloElMesActual()
    {
        var db = NewDb(limiteDteMensual: 2);
        void Dte(DateTime created) => db.DteDocumentos.Add(new DteDocumento
        {
            EmpresaId = Empresa, TipoDteCodigo = "01", NumeroControl = $"DTE-{Guid.NewGuid():N}",
            CodigoGeneracion = Guid.NewGuid().ToString(), EstadoCodigo = "PROCESADO",
            AmbienteCodigo = "PRUEBAS", CreatedAt = created,
        });
        Dte(DateTime.UtcNow);                    // este mes
        Dte(DateTime.UtcNow.AddMonths(-1));      // mes pasado (no cuenta)
        await db.SaveChangesAsync();
        var guard = NewGuard(db);

        (await guard.ValidarLimiteAsync(Empresa, RecursoLimitado.DteMensual)).IsSuccess.Should().BeTrue(); // 1 < 2

        Dte(DateTime.UtcNow);
        await db.SaveChangesAsync();
        (await guard.ValidarLimiteAsync(Empresa, RecursoLimitado.DteMensual)).ErrorCode.Should().Be("LIMIT_EXCEEDED"); // 2 >= 2
    }

    [Fact]
    public async Task UsuariosService_ConGuard_RechazaSobreLimite()
    {
        var db = NewDb(limiteUsuarios: 1);
        var policy = Substitute.For<IPasswordPolicy>();
        policy.Validate(Arg.Any<string?>()).Returns(Result.Ok());
        var hasher = Substitute.For<IPasswordHasher>();
        hasher.Hash(Arg.Any<string>()).Returns("h");
        var svc = new UsuariosService(db, hasher, Substitute.For<IAuditoriaService>(), policy, NewGuard(db));

        CreateUsuarioRequest Req(string u) => new()
        { Username = u, Email = $"{u}@x.com", Password = "P!", NombreCompleto = u };

        (await svc.CreateAsync(Empresa, Req("primero"), "t")).IsSuccess.Should().BeTrue();
        var segundo = await svc.CreateAsync(Empresa, Req("segundo"), "t");
        segundo.ErrorCode.Should().Be("LIMIT_EXCEEDED");

        // Usuarios globales (SuperAdmin, empresaId null) no pasan por el límite.
        (await svc.CreateAsync(null, Req("global"), "t")).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task EmpresaOperativa_SegunEstado_YCache()
    {
        var db = NewDb(estadoEmpresa: "SUSPENDIDA");
        var guard = NewGuard(db);

        (await guard.EmpresaOperativaAsync(Empresa)).Should().BeFalse();

        // Reactivar: el caché de 60s sigue diciendo no operativa hasta invalidar.
        (await db.Empresas.FirstAsync()).EstadoCodigo = "ACTIVA";
        await db.SaveChangesAsync();
        (await guard.EmpresaOperativaAsync(Empresa)).Should().BeFalse();
        LicenciaGuardService.InvalidarEstadoCache(Empresa);
        (await guard.EmpresaOperativaAsync(Empresa)).Should().BeTrue();
    }

    [Fact]
    public async Task EmpresaInexistente_NoEsOperativa()
    {
        var guard = NewGuard(NewDb());
        (await guard.EmpresaOperativaAsync(9999)).Should().BeFalse();
    }
}
