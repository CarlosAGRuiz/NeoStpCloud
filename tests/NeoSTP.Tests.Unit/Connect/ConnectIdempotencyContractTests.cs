using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NeoSTP.Api.Controllers;
using NeoSTP.Api.Middlewares;
using NeoSTP.Application.Clientes;
using NeoSTP.Application.Common;
using NeoSTP.Application.Connect;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Dtos;
using NeoSTP.Application.Productos;
using NeoSTP.Domain.Core.Connect;
using NSubstitute;

namespace NeoSTP.Tests.Unit.Connect;

public class ConnectIdempotencyContractTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ClaveNoSustituyeScopeYSePropagaSoloAlTenantAutorizado(bool canWrite)
    {
        var service = Substitute.For<IConnectDteService>();
        service.EmitirAsync(17, Arg.Any<CreateDteDocumentoRequest>(), "apikey:3", Arg.Any<CancellationToken>())
            .Returns(Result<DteDocumentoDto>.Ok(new() { Id = 1, IdempotencyReplayed = true }));
        var ctrl = new ConnectApiV1Controller(service, Substitute.For<IDteDocumentosService>(),
            Substitute.For<IClientesService>(), Substitute.For<IProductosService>())
        { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        ctrl.HttpContext.Items[ApiKeyAuthMiddleware.ContextItemKey] = new ConnectApiKeyContext
        { EmpresaId = 17, ApiKeyId = 3, Scopes = canWrite ? [ConnectScopes.DteWrite] : [ConnectScopes.DteRead] };
        ctrl.Request.Headers["Idempotency-Key"] = "same-sale";
        var request = new CreateDteDocumentoRequest();
        var result = await ctrl.EmitirDte(request, default);
        if (canWrite)
        {
            result.Should().BeOfType<OkObjectResult>();
            request.IdempotencyKey.Should().Be("same-sale");
            await service.Received(1).EmitirAsync(17, request, "apikey:3", Arg.Any<CancellationToken>());
        }
        else
        {
            result.Should().BeOfType<ObjectResult>().Subject.StatusCode.Should().Be(403);
            service.ReceivedCalls().Should().BeEmpty();
        }
    }
}
