using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeoSTP.Api.Auth;
using NeoSTP.Api.Controllers;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Auth.Dtos;
using NeoSTP.Application.Common;
using NeoSTP.Application.Ops;
using NeoSTP.Application.Usuarios;
using NeoSTP.Application.Usuarios.Dtos;
using NeoSTP.Infrastructure.Auth;
using NSubstitute;

namespace NeoSTP.Tests.Unit.Auth;

public class ApiAuthRateLimitIntegrationTests
{
    // Real AuthController and API rate/proxy registration; never run Program, seeds or SQL.
    private sealed class Fixture : IDisposable
    {
        public IAuthService Auth { get; } = Substitute.For<IAuthService>();
        public IMfaService Mfa { get; } = Substitute.For<IMfaService>();
        public IUsuariosService Users { get; } = Substitute.For<IUsuariosService>();
        public IHost Host { get; }
        public HttpClient Client { get; }

        public Fixture()
        {
            Auth.LoginAsync(Arg.Any<LoginRequest>(), Arg.Any<AuthContext>(), Arg.Any<CancellationToken>())
                .Returns(Result<LoginResponse>.Fail("Credenciales incorrectas.", "AUTH_INVALID_CREDENTIALS"));
            Auth.RefreshAsync(Arg.Any<string>(), Arg.Any<AuthContext>(), Arg.Any<CancellationToken>())
                .Returns(Result<LoginResponse>.Fail("Refresh inválido.", "AUTH_REFRESH_INVALID"));
            Auth.VerifyMfaChallengeAsync(Arg.Any<string>(), Arg.Any<AuthContext>(), Arg.Any<CancellationToken>())
                .Returns(Result<LoginResponse>.Fail("MFA inválido.", "AUTH_MFA_INVALID"));
            Auth.GetCurrentUserInfoAsync(20, Arg.Any<CancellationToken>())
                .Returns(Result<UserInfo>.Ok(new UserInfo { Id = 20, EmpresaId = 101, Username = "fixture", Email = "fixture@example.test", NombreCompleto = "Fixture", TipoUsuarioCodigo = "OPERADOR" }));
            Auth.LogoutAsync(Arg.Any<string?>(), Arg.Any<AuthContext>(), Arg.Any<CancellationToken>()).Returns(Result.Ok());
            Users.ChangePasswordAsync(20, Arg.Any<ChangePasswordRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Result.Ok());
            Mfa.IniciarEnrolamientoAsync(20, Arg.Any<CancellationToken>())
                .Returns(Result<MfaEnrollDto>.Ok(new MfaEnrollDto { Secret = "fixture-only", OtpAuthUri = "fixture-only" }));
            Mfa.ConfirmarEnrolamientoAsync(20, Arg.Any<string>(), Arg.Any<AuthContext>(), Arg.Any<CancellationToken>())
                .Returns(Result<MfaConfirmDto>.Ok(new MfaConfirmDto()));
            Mfa.DeshabilitarAsync(20, Arg.Any<string>(), Arg.Any<AuthContext>(), Arg.Any<CancellationToken>())
                .Returns(Result.Ok());
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:AuthRateLimit:WindowSeconds"] = "60",
                ["Security:AuthRateLimit:LoginPermits"] = "2",
                ["Security:AuthRateLimit:MfaPermits"] = "2",
                ["Security:AuthRateLimit:RefreshPermits"] = "2"
            }).Build();
            Host = new HostBuilder().ConfigureWebHost(web => web.UseTestServer().ConfigureServices(services =>
            {
                services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
                services.AddApiAuthRateLimiting(config);
                services.AddHttpContextAccessor();
                services.AddScoped<ICurrentUser, CurrentUserAccessor>();
                services.AddSingleton(Auth);
                services.AddSingleton(Mfa);
                services.AddSingleton(Users);
                services.AddAuthentication("Fixture").AddScheme<AuthenticationSchemeOptions, FixtureAuthentication>("Fixture", _ => { });
                services.AddAuthorization();
                services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins("https://app.example.test").AllowAnyMethod().AllowAnyHeader()));
            }).Configure(app =>
            {
                app.Use((context, next) =>
                {
                    // Only this isolated test server accepts the synthetic transport address.
                    context.Connection.RemoteIpAddress = IPAddress.Parse(context.Request.Headers["Test-Remote-IP"].FirstOrDefault() ?? "203.0.113.10");
                    return next(context);
                });
                app.UseForwardedHeaders();
                app.UseRouting();
                app.UseCors();
                app.UseRateLimiter();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapControllers();
                    endpoints.MapGet("/health", () => "ok");
                });
            })).Start();
            Client = Host.GetTestClient();
        }

        public Task<HttpResponseMessage> Post(string route = "login", bool authenticated = false,
            string ip = "203.0.113.10", string? forwarded = null, string? path = null, HttpContent? content = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, path ?? "/api/auth/" + route)
            {
                Content = content ?? JsonContent.Create(new
                {
                    usernameOrEmail = "fixture", password = "test-only", refreshToken = "test-only",
                    currentPassword = "test-only", newPassword = "test-only-new", code = "000000"
                })
            };
            request.Headers.Add("Test-Remote-IP", ip);
            if (authenticated) request.Headers.Add("Test-Authenticated", "yes");
            if (forwarded is not null) request.Headers.Add("X-Forwarded-For", forwarded);
            return Client.SendAsync(request);
        }

        public void Dispose() { Client.Dispose(); Host.Dispose(); }
    }

    private sealed class FixtureAuthentication(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
            Task.FromResult(Request.Headers["Test-Authenticated"] == "yes"
                ? AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "20"), new Claim(ClaimTypes.Name, "fixture"),
                    new Claim("empresa_id", "101"), new Claim("tipo_usuario", "OPERADOR")
                }, Scheme.Name)), Scheme.Name))
                : AuthenticateResult.NoResult());
    }

    [Theory]
    [InlineData("login", HttpStatusCode.Unauthorized)]
    [InlineData("refresh", HttpStatusCode.Unauthorized)]
    [InlineData("change-password", HttpStatusCode.OK)]
    [InlineData("mfa/enroll", HttpStatusCode.OK)]
    [InlineData("mfa/confirm", HttpStatusCode.OK)]
    [InlineData("mfa/disable", HttpStatusCode.OK)]
    [InlineData("mfa/verify", HttpStatusCode.BadRequest)]
    public async Task RealController_ThirdRequestIsRejectedBeforeCallingServices(string route, HttpStatusCode expected)
    {
        using var f = new Fixture();
        (await f.Post(route, authenticated: true)).StatusCode.Should().Be(expected);
        (await f.Post(route, authenticated: true)).StatusCode.Should().Be(expected);
        var callsBefore = f.Auth.ReceivedCalls().Count() + f.Mfa.ReceivedCalls().Count() + f.Users.ReceivedCalls().Count();

        var response = await f.Post(route, authenticated: true);
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter!.Delta.Should().BeGreaterThan(TimeSpan.Zero);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        (await response.Content.ReadAsStringAsync()).Should().Contain("Demasiados intentos").And.NotContain("test-only");
        (f.Auth.ReceivedCalls().Count() + f.Mfa.ReceivedCalls().Count() + f.Users.ReceivedCalls().Count()).Should().Be(callsBefore);
    }

    [Theory]
    [InlineData("change-password")]
    [InlineData("mfa/enroll")]
    [InlineData("mfa/confirm")]
    [InlineData("mfa/disable")]
    [InlineData("mfa/verify")]
    public async Task RestrictedActions_StillRequireAuthentication(string route)
    {
        using var f = new Fixture();
        (await f.Post(route)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        f.Auth.ReceivedCalls().Should().BeEmpty();
        f.Mfa.ReceivedCalls().Should().BeEmpty();
        f.Users.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task LoginLimit_DoesNotPreventLogoutMeHealthOrRefresh()
    {
        using var f = new Fixture();
        await f.Post();
        await f.Post();
        (await f.Post()).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await f.Post("logout", authenticated: true)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await f.Post("refresh")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await f.Client.GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.OK);
        using var me = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        me.Headers.Add("Test-Authenticated", "yes");
        (await f.Client.SendAsync(me)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MfaActions_ShareOneBudget()
    {
        using var f = new Fixture();
        (await f.Post("mfa/enroll", authenticated: true)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await f.Post("mfa/confirm", authenticated: true)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await f.Post("mfa/disable", authenticated: true)).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        await f.Mfa.DidNotReceive().DeshabilitarAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<AuthContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MalformedRequests_ConsumeBudgetWithoutCallingAuth()
    {
        using var f = new Fixture();
        for (var i = 0; i < 2; i++)
            (await f.Post(content: new StringContent("{", System.Text.Encoding.UTF8, "application/json")))
                .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await f.Post()).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        f.Auth.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task CaseAndTrailingSlash_DoNotBypassRoutePolicy()
    {
        using var f = new Fixture();
        await f.Post();
        await f.Post(path: "/API/AUTH/LOGIN");
        (await f.Post(path: "/api/auth/login/")).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task UntrustedForwardedHeader_DoesNotChangeQuotaOrAuthContext()
    {
        using var f = new Fixture();
        await f.Post(forwarded: "198.51.100.1");
        await f.Post(forwarded: "198.51.100.2");
        (await f.Post(forwarded: "198.51.100.3")).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        await f.Auth.Received(2).LoginAsync(Arg.Any<LoginRequest>(),
            Arg.Is<AuthContext>(c => c.IpAddress == "203.0.113.10"), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    [InlineData("::ffff:127.0.0.1")]
    public async Task TrustedProxy_PartitionsByVerifiedClientAndForwardsCorrectAuthContext(string proxy)
    {
        using var f = new Fixture();
        await f.Post(ip: proxy, forwarded: "198.51.100.1, 203.0.113.20");
        await f.Post(ip: proxy, forwarded: "198.51.100.2, 203.0.113.20");
        (await f.Post(ip: proxy, forwarded: "198.51.100.3, 203.0.113.20")).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await f.Post(ip: proxy, forwarded: "203.0.113.21")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await f.Auth.Received(2).LoginAsync(Arg.Any<LoginRequest>(),
            Arg.Is<AuthContext>(c => c.IpAddress == "203.0.113.20"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PreflightDoesNotConsumeLoginQuota_And429HasCorsHeader()
    {
        using var f = new Fixture();
        for (var i = 0; i < 3; i++)
        {
            using var options = new HttpRequestMessage(HttpMethod.Options, "/api/auth/login");
            options.Headers.Add("Origin", "https://app.example.test");
            options.Headers.Add("Access-Control-Request-Method", "POST");
            (await f.Client.SendAsync(options)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        }
        (await f.Post()).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await f.Post()).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        f.Client.DefaultRequestHeaders.Add("Origin", "https://app.example.test");
        var response = await f.Post();
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Headers.GetValues("Access-Control-Allow-Origin").Should().ContainSingle().Which.Should().Be("https://app.example.test");
    }

    [Fact]
    public async Task SuccessfulLogin_StillReturnsOriginalResponseContract()
    {
        using var f = new Fixture();
        f.Auth.LoginAsync(Arg.Any<LoginRequest>(), Arg.Any<AuthContext>(), Arg.Any<CancellationToken>())
            .Returns(Result<LoginResponse>.Ok(new LoginResponse
            {
                AccessToken = "fixture-access", RefreshToken = "fixture-refresh",
                User = new UserInfo { Id = 20, Username = "fixture", Email = "fixture@example.test", NombreCompleto = "Fixture", TipoUsuarioCodigo = "OPERADOR" }
            }));
        var response = await f.Post();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"success\":true").And.Contain("\"accessToken\":\"fixture-access\"").And.Contain("\"refreshToken\":\"fixture-refresh\"");
    }

    [Fact]
    public void ActualProgram_RegistersRateLimitInCorrectOrder_AndOnlyTargetedActionsHavePolicies()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "NeoSTP.slnx"))) root = root.Parent;
        root.Should().NotBeNull();
        var program = File.ReadAllText(Path.Combine(root!.FullName, "src", "NeoSTP.Api", "Program.cs"));
        program.Should().Contain("builder.Services.AddApiAuthRateLimiting(builder.Configuration);");
        var ordered = new[] { "app.UseForwardedHeaders();", "app.UseHttpsRedirection();", "app.UseRouting();",
            "app.UseCors();", "app.UseRateLimiter();", "app.UseAuthentication();", "app.UseAuthorization();", "app.MapControllers();" };
        var positions = ordered.Select(x => program.IndexOf(x, StringComparison.Ordinal)).ToArray();
        positions.Should().OnlyContain(i => i >= 0).And.BeInAscendingOrder();
        var policies = typeof(AuthController).GetMethods()
            .Select(m => (m.Name, Policy: m.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName))
            .Where(x => x.Policy is not null).ToDictionary(x => x.Name, x => x.Policy);
        policies.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            [nameof(AuthController.Login)] = AuthRateLimiting.Login,
            [nameof(AuthController.Refresh)] = AuthRateLimiting.Refresh,
            [nameof(AuthController.ChangePassword)] = AuthRateLimiting.Login,
            [nameof(AuthController.MfaEnroll)] = AuthRateLimiting.Mfa,
            [nameof(AuthController.MfaConfirm)] = AuthRateLimiting.Mfa,
            [nameof(AuthController.MfaDisable)] = AuthRateLimiting.Mfa,
            [nameof(AuthController.MfaVerify)] = AuthRateLimiting.Mfa
        });
    }
}
