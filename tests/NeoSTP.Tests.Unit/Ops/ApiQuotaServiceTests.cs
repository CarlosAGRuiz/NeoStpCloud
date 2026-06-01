using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NeoSTP.Application.Ops;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Ops;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Ops;

/// <summary>
/// Sprint 20.2 — cuotas / rate limiting: evaluación de reglas por ámbito,
/// conteo por ventana deslizante, exención de SuperAdmin y aislamiento por empresa.
/// </summary>
public class ApiQuotaServiceTests
{
    private const int EmpresaA = 10;
    private const int EmpresaB = 20;

    private static (ApiQuotaService svc, NeoStpDbContext db) Build(Action<NeoStpDbContext>? seed = null)
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"quota-{Guid.NewGuid()}")
            .Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.Add(new Empresa { Id = EmpresaA, Nit = "A", RazonSocial = "A", EstadoCodigo = "ACTIVA" });
        db.Empresas.Add(new Empresa { Id = EmpresaB, Nit = "B", RazonSocial = "B", EstadoCodigo = "ACTIVA" });
        seed?.Invoke(db);
        db.SaveChanges();
        var audit = Substitute.For<NeoSTP.Application.Auth.Abstractions.IAuditoriaService>();
        return (new ApiQuotaService(db, audit, NullLogger<ApiQuotaService>.Instance), db);
    }

    private static ApiUsageLog Uso(int? empresaId, string modulo, DateTime at, int? usuarioId = null)
        => new() { EmpresaId = empresaId, UsuarioId = usuarioId, Modulo = modulo, Metodo = "GET", Ruta = "/api/x", StatusCode = 200, OcurrioAt = at };

    private static QuotaContext Ctx(int? empresaId = EmpresaA, int? usuarioId = 1, string? modulo = "NEODTE", bool superAdmin = false)
        => new() { EmpresaId = empresaId, UsuarioId = usuarioId, Modulo = modulo, IsSuperAdmin = superAdmin };

    [Fact]
    public async Task SinCuotas_Permite()
    {
        var (svc, _) = Build();
        var d = await svc.EvaluarAsync(Ctx());
        d.Allowed.Should().BeTrue();
        d.Limit.Should().BeNull();
    }

    [Fact]
    public async Task SuperAdmin_ExentoAunConCuotaExcedida()
    {
        var (svc, _) = Build(db =>
        {
            db.ApiQuotas.Add(new ApiQuota { EmpresaId = EmpresaA, Ambito = ApiQuotaAmbito.Empresa, VentanaSegundos = 60, LimitePeticiones = 1 });
            db.ApiUsageLogs.Add(Uso(EmpresaA, "NEODTE", DateTime.UtcNow));
            db.ApiUsageLogs.Add(Uso(EmpresaA, "NEODTE", DateTime.UtcNow));
        });

        var d = await svc.EvaluarAsync(Ctx(superAdmin: true));
        d.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task CuotaEmpresa_BloqueaAlAlcanzarLimite()
    {
        var (svc, _) = Build(db =>
        {
            db.ApiQuotas.Add(new ApiQuota { EmpresaId = EmpresaA, Ambito = ApiQuotaAmbito.Empresa, VentanaSegundos = 60, LimitePeticiones = 2 });
            db.ApiUsageLogs.Add(Uso(EmpresaA, "NEODTE", DateTime.UtcNow));
            db.ApiUsageLogs.Add(Uso(EmpresaA, "CORE", DateTime.UtcNow));
        });

        var d = await svc.EvaluarAsync(Ctx());
        d.Allowed.Should().BeFalse();
        d.AmbitoExcedido.Should().Be(ApiQuotaAmbito.Empresa);
        d.Limit.Should().Be(2);
        d.RetryAfterSeconds.Should().Be(60);
    }

    [Fact]
    public async Task CuotaEmpresa_PermiteDentroDeVentana_ConRemainingCorrecto()
    {
        var (svc, _) = Build(db =>
        {
            db.ApiQuotas.Add(new ApiQuota { EmpresaId = EmpresaA, Ambito = ApiQuotaAmbito.Empresa, VentanaSegundos = 60, LimitePeticiones = 5 });
            db.ApiUsageLogs.Add(Uso(EmpresaA, "NEODTE", DateTime.UtcNow));
            db.ApiUsageLogs.Add(Uso(EmpresaA, "CORE", DateTime.UtcNow));
        });

        var d = await svc.EvaluarAsync(Ctx());
        d.Allowed.Should().BeTrue();
        d.Limit.Should().Be(5);
        d.Remaining.Should().Be(3);
    }

    [Fact]
    public async Task VentanaDeslizante_IgnoraUsoFueraDeLaVentana()
    {
        var (svc, _) = Build(db =>
        {
            db.ApiQuotas.Add(new ApiQuota { EmpresaId = EmpresaA, Ambito = ApiQuotaAmbito.Empresa, VentanaSegundos = 60, LimitePeticiones = 2 });
            // 2 usos viejos (hace 5 min) NO deben contar
            db.ApiUsageLogs.Add(Uso(EmpresaA, "NEODTE", DateTime.UtcNow.AddMinutes(-5)));
            db.ApiUsageLogs.Add(Uso(EmpresaA, "NEODTE", DateTime.UtcNow.AddMinutes(-5)));
        });

        var d = await svc.EvaluarAsync(Ctx());
        d.Allowed.Should().BeTrue();
        d.Remaining.Should().Be(2);
    }

    [Fact]
    public async Task CuotaModulo_CuentaSoloElModuloDelContexto()
    {
        var (svc, _) = Build(db =>
        {
            db.ApiQuotas.Add(new ApiQuota { EmpresaId = EmpresaA, Ambito = ApiQuotaAmbito.Modulo, AmbitoRef = "NEODTE", VentanaSegundos = 60, LimitePeticiones = 2 });
            db.ApiUsageLogs.Add(Uso(EmpresaA, "NEODTE", DateTime.UtcNow));
            db.ApiUsageLogs.Add(Uso(EmpresaA, "NEODTE", DateTime.UtcNow));
            db.ApiUsageLogs.Add(Uso(EmpresaA, "CORE", DateTime.UtcNow)); // otro módulo, no cuenta
        });

        var bloqueado = await svc.EvaluarAsync(Ctx(modulo: "NEODTE"));
        bloqueado.Allowed.Should().BeFalse();

        var permitido = await svc.EvaluarAsync(Ctx(modulo: "CORE"));
        permitido.Allowed.Should().BeTrue("la cuota es de NEODTE y no aplica a CORE");
    }

    [Fact]
    public async Task CuotaEmpresa_NoMezclaEmpresas()
    {
        var (svc, _) = Build(db =>
        {
            db.ApiQuotas.Add(new ApiQuota { EmpresaId = EmpresaA, Ambito = ApiQuotaAmbito.Empresa, VentanaSegundos = 60, LimitePeticiones = 2 });
            // uso de EmpresaB no debe afectar el conteo de EmpresaA
            db.ApiUsageLogs.Add(Uso(EmpresaB, "NEODTE", DateTime.UtcNow));
            db.ApiUsageLogs.Add(Uso(EmpresaB, "NEODTE", DateTime.UtcNow));
        });

        var d = await svc.EvaluarAsync(Ctx(empresaId: EmpresaA));
        d.Allowed.Should().BeTrue();
        d.Remaining.Should().Be(2);
    }

    [Fact]
    public async Task Crear_CuotaValida_ApareceEnListar()
    {
        var (svc, _) = Build();
        var r = await svc.CrearAsync(new CrearApiQuotaRequest
        {
            EmpresaId = EmpresaA, Ambito = ApiQuotaAmbito.Modulo, AmbitoRef = "NEODTE",
            LimitePeticiones = 100, VentanaSegundos = 60, Descripcion = "Tope NEODTE",
        }, "tester");

        r.IsSuccess.Should().BeTrue();
        var lista = await svc.ListarAsync();
        lista.Should().ContainSingle(q => q.AmbitoRef == "NEODTE" && q.LimitePeticiones == 100);
    }

    [Fact]
    public async Task Crear_AmbitoInvalido_DevuelveValidation()
    {
        var (svc, _) = Build();
        var r = await svc.CrearAsync(new CrearApiQuotaRequest { Ambito = "RARO", LimitePeticiones = 10 }, "tester");
        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("VALIDATION");
    }

    [Fact]
    public async Task Eliminar_QuotaInexistente_DevuelveNotFound()
    {
        var (svc, _) = Build();
        var r = await svc.EliminarAsync(999, "tester");
        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("QUOTA_NOT_FOUND");
    }

    [Fact]
    public async Task RegistrarUso_InsertaFilaEnUsageLog()
    {
        var (svc, db) = Build();
        await svc.RegistrarUsoAsync(new ApiUsageEntry
        {
            EmpresaId = EmpresaA, UsuarioId = 7, Metodo = "POST", Ruta = "/api/dte/factura",
            Modulo = "NEODTE", StatusCode = 200, DuracionMs = 42, IpOrigen = "10.0.0.1",
        });

        var fila = await db.ApiUsageLogs.AsNoTracking().SingleAsync();
        fila.EmpresaId.Should().Be(EmpresaA);
        fila.StatusCode.Should().Be(200);
        fila.Modulo.Should().Be("NEODTE");
        fila.DuracionMs.Should().Be(42);
    }
}
