using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Auth;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Auth.Dtos;
using NeoSTP.Application.Common;
using NeoSTP.Application.Ops;
using NeoSTP.Application.Usuarios;
using NeoSTP.Web.Controllers;
using NeoSTP.Web.Models;
using NSubstitute;
using ApiAuthController = NeoSTP.Api.Controllers.AuthController;

namespace NeoSTP.Tests.Unit.Auth;

public class AuthSurfaceRegressionTests
{
    [Theory]
    [InlineData("AUTH_REFRESH_CONTEXT_REQUIRED", 401)]
    [InlineData("AUTH_REFRESH_INVALID", 401)]
    [InlineData("AUTH_INVALID_CONTEXT", 401)]
    [InlineData("EMPRESA_NO_MEMBRESIA", 403)]
    [InlineData("EMPRESA_SUSPENDIDA", 403)]
    public async Task ApiRefresh_UsesExplicitAuthenticationOrAuthorizationStatus(string code, int status)
    {
        var auth = Substitute.For<IAuthService>();
        auth.RefreshAsync(Arg.Any<string>(), Arg.Any<AuthContext>(), Arg.Any<CancellationToken>())
            .Returns(Result<LoginResponse>.Fail("Inicia sesión nuevamente.", code));
        var controller = new ApiAuthController(auth, Substitute.For<IUsuariosService>(),
            Substitute.For<ICurrentUser>(), Substitute.For<IMfaService>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var response = await controller.Refresh(new RefreshRequest { RefreshToken = "synthetic" }, default);
        response.Should().BeAssignableTo<ObjectResult>().Which.StatusCode.Should().Be(status);
    }

    [Theory]
    [InlineData(" 123456 ", "123456")]
    [InlineData(" ABCDE-12345 ", "ABCDE-12345")]
    [InlineData(null, null)]
    public async Task WebLogin_PassesMfaOrRecoveryCode_ToSharedAuthService(string? entered, string? expected)
    {
        var auth = Substitute.For<IAuthService>();
        auth.LoginAsync(Arg.Any<LoginRequest>(), Arg.Any<AuthContext>(), Arg.Any<CancellationToken>())
            .Returns(Result<LoginResponse>.Fail("MFA requerido.", "AUTH_MFA_REQUIRED"));
        var controller = new AccountController(auth, Substitute.For<IUsuariosService>(),
            Substitute.For<ICurrentUser>(), Options.Create(new SsoOptions()), NullLogger<AccountController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var response = await controller.Login(new LoginViewModel
        {
            UsernameOrEmail = "test@example.test", Password = "synthetic", MfaCode = entered
        }, default);

        response.Should().BeOfType<ViewResult>();
        await auth.Received(1).LoginAsync(Arg.Is<LoginRequest>(r => r.MfaCode == expected),
            Arg.Any<AuthContext>(), Arg.Any<CancellationToken>());
        controller.ModelState.IsValid.Should().BeFalse();
    }
}
