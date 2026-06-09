using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Comunicaciones;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Comunicaciones;

/// <summary>Correo por empresa — ConfiguracionCorreoService: cifra password y no lo expone.</summary>
public class ConfiguracionCorreoServiceTests
{
    private const int Empresa = 88;

    private static NeoStpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"correo-{Guid.NewGuid()}").Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.Add(new Empresa { Id = Empresa, Nit = "X", RazonSocial = "Empresa", EstadoCodigo = "ACTIVA" });
        db.SaveChanges();
        return db;
    }

    private static (ConfiguracionCorreoService svc, ISecretProtector prot, ITenantEmailSender sender) NewSvc(NeoStpDbContext db)
    {
        var prot = Substitute.For<ISecretProtector>();
        prot.Protect(Arg.Any<string>()).Returns(ci => $"enc::{ci.Arg<string>()}");
        var sender = Substitute.For<ITenantEmailSender>();
        sender.EnviarAsync(Arg.Any<int>(), Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(new EmailSendResult { Success = true });
        var svc = new ConfiguracionCorreoService(db, Substitute.For<IAuditoriaService>(), prot, sender);
        return (svc, prot, sender);
    }

    private static GuardarConfiguracionCorreoRequest Req(string? pass = "secreto") => new()
    {
        Activo = true, Host = "smtp.test.com", Puerto = 587, UsarStartTls = true,
        Usuario = "user@test.com", Password = pass, FromNombre = "Empresa", FromEmail = "ventas@test.com",
    };

    [Fact]
    public async Task Guardar_CifraPassword_YNoLaExpone()
    {
        var db = NewDb(); var (svc, prot, _) = NewSvc(db);

        var r = await svc.GuardarAsync(Empresa, Req("secreto"), "t");

        r.IsSuccess.Should().BeTrue();
        prot.Received(1).Protect("secreto");
        r.Value!.TienePassword.Should().BeTrue();
        // El DTO nunca incluye la contraseña en claro ni cifrada.
        var almacenada = (await db.ConfiguracionesCorreo.FirstAsync()).PasswordProtegida;
        almacenada.Should().Be("enc::secreto");
    }

    [Fact]
    public async Task Guardar_PasswordVacio_ConservaAnterior()
    {
        var db = NewDb(); var (svc, prot, _) = NewSvc(db);
        await svc.GuardarAsync(Empresa, Req("original"), "t");

        await svc.GuardarAsync(Empresa, Req(pass: null), "t"); // sin password

        (await db.ConfiguracionesCorreo.FirstAsync()).PasswordProtegida.Should().Be("enc::original");
        prot.Received(1).Protect(Arg.Any<string>()); // sólo la primera vez
    }

    [Fact]
    public async Task Get_SinConfig_DevuelveNoConfigurado()
    {
        var db = NewDb(); var (svc, _, _) = NewSvc(db);

        var r = await svc.GetAsync(Empresa);

        r.IsSuccess.Should().BeTrue();
        r.Value!.Configurado.Should().BeFalse();
    }

    [Fact]
    public async Task Probar_SinConfigActiva_Falla()
    {
        var db = NewDb(); var (svc, _, _) = NewSvc(db);

        var r = await svc.ProbarAsync(Empresa, "x@y.com", "t");

        r.IsFailure.Should().BeTrue();
        r.ErrorCode.Should().Be("INVALID_STATE");
    }

    [Fact]
    public async Task Probar_ConConfig_EnviaCorreo()
    {
        var db = NewDb(); var (svc, _, sender) = NewSvc(db);
        await svc.GuardarAsync(Empresa, Req(), "t");

        var r = await svc.ProbarAsync(Empresa, "x@y.com", "t");

        r.IsSuccess.Should().BeTrue();
        await sender.Received(1).EnviarAsync(Empresa, Arg.Is<EmailMessage>(m => m.To == "x@y.com"), Arg.Any<CancellationToken>());
    }
}
