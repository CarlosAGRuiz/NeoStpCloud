using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NeoSTP.Application.Cobranza;
using NeoSTP.Application.Cobranza.Dtos;
using NeoSTP.Application.Common;
using NeoSTP.Application.Notificaciones;
using NeoSTP.Application.Notificaciones.Dtos;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Notificaciones;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Notificaciones;

/// <summary>
/// B-4: centro de alertas (dedupe por clave, push a dispositivos, marcar/resolver) y
/// generación desde datos reales (DTE rechazado, certificado por vencer, facturas vencidas).
/// </summary>
public class AlertaServiceTests
{
    private const int EmpresaA = 50;
    private const int Usuario = 9;

    private static NeoStpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"alertas-{Guid.NewGuid()}")
            .Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.Add(new Empresa { Id = EmpresaA, Nit = "A", RazonSocial = "A", EstadoCodigo = "ACTIVA" });
        db.SaveChanges();
        return db;
    }

    private static (AlertaService svc, IPushSender push) NewSvc(NeoStpDbContext db)
    {
        var push = Substitute.For<IPushSender>();
        push.EnviarAsync(Arg.Any<PushMessage>(), Arg.Any<CancellationToken>())
            .Returns(new PushResult { Success = true, Enviados = 1 });
        return (new AlertaService(db, push, NullLogger<AlertaService>.Instance), push);
    }

    private static CrearAlertaRequest Req(int? entidadId = 1) => new()
    {
        EmpresaId = EmpresaA, TipoCodigo = AlertaTipos.DteRechazado, Severidad = AlertaSeveridades.Critica,
        Titulo = "DTE rechazado", Mensaje = "Revisa", EntidadTipo = "DteDocumento", EntidadId = entidadId,
    };

    [Fact]
    public async Task Crear_DeduplicaPorClave()
    {
        var db = NewDb();
        var (svc, _) = NewSvc(db);

        await svc.CrearAsync(Req(entidadId: 1));
        await svc.CrearAsync(Req(entidadId: 1)); // misma clave → no duplica
        await svc.CrearAsync(Req(entidadId: 2)); // distinta → nueva

        (await db.Alertas.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Crear_EnviaPushASoloDispositivosActivos()
    {
        var db = NewDb();
        db.DispositivosNotificacion.Add(new DispositivoNotificacion { EmpresaId = EmpresaA, UsuarioId = Usuario, Token = "t1", Activo = true });
        db.DispositivosNotificacion.Add(new DispositivoNotificacion { EmpresaId = EmpresaA, UsuarioId = Usuario, Token = "t2", Activo = false });
        await db.SaveChangesAsync();
        var (svc, push) = NewSvc(db);

        await svc.CrearAsync(Req());

        await push.Received(1).EnviarAsync(Arg.Is<PushMessage>(m => m.Tokens.Count == 1 && m.Tokens.Contains("t1")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Crear_DesactivaTokensReportadosComoInvalidos()
    {
        var db = NewDb();
        db.DispositivosNotificacion.Add(new DispositivoNotificacion { EmpresaId = EmpresaA, UsuarioId = Usuario, Token = "ok", Activo = true });
        db.DispositivosNotificacion.Add(new DispositivoNotificacion { EmpresaId = EmpresaA, UsuarioId = Usuario, Token = "bad", Activo = true });
        await db.SaveChangesAsync();

        var push = Substitute.For<IPushSender>();
        push.EnviarAsync(Arg.Any<PushMessage>(), Arg.Any<CancellationToken>())
            .Returns(new PushResult { Success = true, Enviados = 1, InvalidTokens = new[] { "bad" } });
        var svc = new AlertaService(db, push, NullLogger<AlertaService>.Instance);

        await svc.CrearAsync(Req());

        (await db.DispositivosNotificacion.FirstAsync(d => d.Token == "bad")).Activo.Should().BeFalse();
        (await db.DispositivosNotificacion.FirstAsync(d => d.Token == "ok")).Activo.Should().BeTrue();
    }

    [Fact]
    public async Task Resumen_CuentaPendientesPorSeveridad()
    {
        var db = NewDb();
        var (svc, _) = NewSvc(db);
        await svc.CrearAsync(Req(1)); // crítica
        await svc.CrearAsync(new CrearAlertaRequest { EmpresaId = EmpresaA, TipoCodigo = "X", Severidad = AlertaSeveridades.Advertencia, Titulo = "t", Mensaje = "m", Clave = "X:1" });

        var r = await svc.ResumenAsync(EmpresaA, Usuario);
        r.Pendientes.Should().Be(2);
        r.Criticas.Should().Be(1);
        r.Advertencias.Should().Be(1);
    }

    [Fact]
    public async Task Resolver_SacaDeListadoPorDefecto()
    {
        var db = NewDb();
        var (svc, _) = NewSvc(db);
        var a = (await svc.CrearAsync(Req())).Value!;

        (await svc.ResolverAsync(EmpresaA, Usuario, a.Id)).IsSuccess.Should().BeTrue();

        var lista = await svc.ListarAsync(EmpresaA, Usuario, new AlertaQuery());
        lista.Value!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task RegistrarDispositivo_UpsertPorToken()
    {
        var db = NewDb();
        var (svc, _) = NewSvc(db);

        await svc.RegistrarDispositivoAsync(EmpresaA, Usuario, new RegistrarDispositivoRequest { Token = "abc", Plataforma = "android" });
        await svc.RegistrarDispositivoAsync(EmpresaA, Usuario, new RegistrarDispositivoRequest { Token = "abc", Plataforma = "ios" });

        (await db.DispositivosNotificacion.CountAsync()).Should().Be(1);
        (await db.DispositivosNotificacion.FirstAsync()).Plataforma.Should().Be("IOS");
    }

    [Fact]
    public async Task Generacion_CreaAlertasDesdeDatosReales()
    {
        var db = NewDb();
        db.DteDocumentos.Add(new DteDocumento
        {
            Id = 1, EmpresaId = EmpresaA, TipoDteCodigo = "01", EstadoCodigo = DteEstadoCodigos.Rechazado,
            NumeroControl = "DTE-01-0001", CodigoGeneracion = Guid.NewGuid().ToString(),
        });
        await db.SaveChangesAsync();
        var (alertaSvc, _) = NewSvc(db);
        var cobranza = Substitute.For<ICobranzaService>();
        cobranza.GetPendientesAsync(EmpresaA, Arg.Any<CobranzaQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<PagedResult<CobroPendienteDto>>.Ok(PagedResult<CobroPendienteDto>.Create(Array.Empty<CobroPendienteDto>(), 0, 1, 20)));
        var gen = new AlertaGeneracionService(db, alertaSvc, cobranza);

        var creadas = await gen.GenerarAsync(EmpresaA);

        creadas.Should().Be(1); // el DTE rechazado
        (await db.Alertas.AnyAsync(a => a.TipoCodigo == AlertaTipos.DteRechazado)).Should().BeTrue();

        // Re-generar no duplica
        (await gen.GenerarAsync(EmpresaA)).Should().Be(0);
    }
}
