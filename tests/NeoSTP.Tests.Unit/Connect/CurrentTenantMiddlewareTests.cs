using FluentAssertions;
using Microsoft.AspNetCore.Http;
using NeoSTP.Api.Middlewares;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Connect;
using NeoSTP.Application.Empresas;
using NeoSTP.Application.Empresas.Dtos;
using NeoSTP.Application.Licenciamiento;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Connect;

public class CurrentTenantMiddlewareTests
{
    private const int Empresa = 23;

    private static DefaultHttpContext ContextoApiKey(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        context.Items[ApiKeyAuthMiddleware.ContextItemKey] = new ConnectApiKeyContext
        {
            ApiKeyId = 1,
            EmpresaId = Empresa,
        };
        return context;
    }

    private static LicenciaDto Licencia(bool vigente, params string[] modulos) => new()
    {
        EmpresaId = Empresa,
        EmpresaNombre = "Cliente",
        EmpresaEstado = "ACTIVA",
        Vigente = vigente,
        Modulos = modulos.Select((codigo, i) => new EmpresaModuloDto
        {
            ModuloId = i + 1,
            Codigo = codigo,
            Nombre = codigo,
            Activo = true,
        }).ToArray(),
    };

    [Fact]
    public async Task ApiKeyDte_ConPlanYModuloActivo_LlamaNext()
    {
        var guard = Substitute.For<ILicenciaGuardService>();
        guard.EmpresaOperativaAsync(Empresa, Arg.Any<CancellationToken>()).Returns(true);
        var resolver = Substitute.For<ILicenciaResolver>();
        resolver.ResolveAsync(Empresa, Arg.Any<CancellationToken>()).Returns(Licencia(true, "NEODTE"));
        var nextCalled = false;
        var middleware = new CurrentTenantMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(ContextoApiKey("/api/v1/dte"), Substitute.For<ICurrentUser>(), guard, resolver);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task ApiKeyDte_SinPlanVigente_Responde402()
    {
        var guard = Substitute.For<ILicenciaGuardService>();
        guard.EmpresaOperativaAsync(Empresa, Arg.Any<CancellationToken>()).Returns(true);
        var resolver = Substitute.For<ILicenciaResolver>();
        resolver.ResolveAsync(Empresa, Arg.Any<CancellationToken>()).Returns(Licencia(false, "NEODTE"));
        var nextCalled = false;
        var middleware = new CurrentTenantMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = ContextoApiKey("/api/v1/dte");

        await middleware.InvokeAsync(context, Substitute.For<ICurrentUser>(), guard, resolver);

        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status402PaymentRequired);
    }

    [Fact]
    public async Task ApiKeyDte_SinModulo_Responde403()
    {
        var guard = Substitute.For<ILicenciaGuardService>();
        guard.EmpresaOperativaAsync(Empresa, Arg.Any<CancellationToken>()).Returns(true);
        var resolver = Substitute.For<ILicenciaResolver>();
        resolver.ResolveAsync(Empresa, Arg.Any<CancellationToken>()).Returns(Licencia(true, "CORE"));
        var nextCalled = false;
        var middleware = new CurrentTenantMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = ContextoApiKey("/api/v1/dte/99/pdf");

        await middleware.InvokeAsync(context, Substitute.For<ICurrentUser>(), guard, resolver);

        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task ApiKeyPing_NoExigeModuloPeroSiEmpresaOperativa()
    {
        var guard = Substitute.For<ILicenciaGuardService>();
        guard.EmpresaOperativaAsync(Empresa, Arg.Any<CancellationToken>()).Returns(true);
        var resolver = Substitute.For<ILicenciaResolver>();
        var nextCalled = false;
        var middleware = new CurrentTenantMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(ContextoApiKey("/api/v1/ping"), Substitute.For<ICurrentUser>(), guard, resolver);

        nextCalled.Should().BeTrue();
        await resolver.DidNotReceive().ResolveAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }
}
