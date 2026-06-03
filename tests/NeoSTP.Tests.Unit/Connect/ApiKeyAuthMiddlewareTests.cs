using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using NeoSTP.Api.Middlewares;
using NeoSTP.Application.Connect;
using NeoSTP.Domain.Core.Connect;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Connect;

/// <summary>
/// NeoConnect — middleware de autenticación por API Key: precedencia del JWT,
/// ausencia de header, key válida (contexto en Items) y key inválida (401).
/// </summary>
public class ApiKeyAuthMiddlewareTests
{
    private static DefaultHttpContext NewContext()
    {
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        return ctx;
    }

    [Fact]
    public async Task ConJwtAutenticado_NoValidaApiKey_LlamaNext()
    {
        var apiKeys = Substitute.For<IConnectApiKeyService>();
        var nextCalled = false;
        var mw = new ApiKeyAuthMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        var ctx = NewContext();
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "jwt")); // IsAuthenticated == true
        ctx.Request.Headers["X-Api-Key"] = "nsk_loquesea";

        await mw.InvokeAsync(ctx, apiKeys);

        nextCalled.Should().BeTrue();
        await apiKeys.DidNotReceive().ValidarAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SinHeader_LlamaNext_SinValidar()
    {
        var apiKeys = Substitute.For<IConnectApiKeyService>();
        var nextCalled = false;
        var mw = new ApiKeyAuthMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        var ctx = NewContext();

        await mw.InvokeAsync(ctx, apiKeys);

        nextCalled.Should().BeTrue();
        await apiKeys.DidNotReceive().ValidarAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task KeyValida_ColocaContextoEnItems_LlamaNext()
    {
        var apiKeys = Substitute.For<IConnectApiKeyService>();
        var resuelto = new ConnectApiKeyContext { ApiKeyId = 1, EmpresaId = 9, Scopes = new[] { ConnectScopes.DteWrite } };
        apiKeys.ValidarAsync("nsk_valida", Arg.Any<CancellationToken>()).Returns(resuelto);

        var nextCalled = false;
        var mw = new ApiKeyAuthMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        var ctx = NewContext();
        ctx.Request.Headers["X-Api-Key"] = "nsk_valida";

        await mw.InvokeAsync(ctx, apiKeys);

        nextCalled.Should().BeTrue();
        ctx.Items[ApiKeyAuthMiddleware.ContextItemKey].Should().BeSameAs(resuelto);
    }

    [Fact]
    public async Task KeyInvalida_Responde401_NoLlamaNext()
    {
        var apiKeys = Substitute.For<IConnectApiKeyService>();
        apiKeys.ValidarAsync("nsk_mala", Arg.Any<CancellationToken>()).Returns((ConnectApiKeyContext?)null);

        var nextCalled = false;
        var mw = new ApiKeyAuthMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        var ctx = NewContext();
        ctx.Request.Headers["X-Api-Key"] = "nsk_mala";

        await mw.InvokeAsync(ctx, apiKeys);

        nextCalled.Should().BeFalse();
        ctx.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        ctx.Items.ContainsKey(ApiKeyAuthMiddleware.ContextItemKey).Should().BeFalse();
    }
}
