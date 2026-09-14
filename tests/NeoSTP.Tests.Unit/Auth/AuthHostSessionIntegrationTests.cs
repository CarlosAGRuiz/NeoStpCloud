using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NeoSTP.Api.Auth;
using NeoSTP.Application.Auth;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Auth.Dtos;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Application.Ops;
using NeoSTP.Application.Usuarios;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Seguridad;
using NeoSTP.Infrastructure.Auth;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NeoSTP.Shared;
using NeoSTP.Web.Auth;
using NeoSTP.Web.Controllers;
using NSubstitute;

namespace NeoSTP.Tests.Unit.Auth;

// Actual MVC controllers, JWT/cookie handlers and shared Auth/MFA/session services.
// Isolated TestServer, ephemeral DP, synthetic keys, EF InMemory: NO Program/SQL/seeds.
public class AuthHostSessionIntegrationTests
{
    private sealed class Fixture : IDisposable
    {
        public IHost Host { get; }
        public HttpClient Client { get; }
        public bool Web { get; }
        private readonly Dictionary<string, string> cookies = new();
        public string? Token { get; set; }
        public string? AuthCookie
        {
            get => cookies.GetValueOrDefault("fixture-auth");
            set { if (value is not null) cookies["fixture-auth"] = value; }
        }
        private string? csrf;

        public Fixture(bool web = false, bool platform = false, bool mfa = false, int permits = 100)
        {
            Web = web;
            var dbName = "host-session-" + Guid.NewGuid();
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:AuthRateLimit:LoginPermits"] = permits.ToString(),
                ["Security:AuthRateLimit:MfaPermits"] = permits.ToString()
            }).Build();
            Host = new HostBuilder().ConfigureWebHost(builder => builder.UseTestServer()
                .UseContentRoot(Path.Combine(RepoRoot(), "src", "NeoSTP.Web"))
                .UseWebRoot("wwwroot")
                .UseSetting(WebHostDefaults.ApplicationKey, typeof(AccountController).Assembly.GetName().Name)
                .ConfigureServices(services =>
                {
                    services.AddControllersWithViews().AddApplicationPart(typeof(AccountController).Assembly)
                        .AddApplicationPart(typeof(NeoSTP.Api.Controllers.AuthController).Assembly);
                    services.AddDataProtection().UseEphemeralDataProtectionProvider();
                    services.AddDbContext<NeoStpDbContext>(o => o.UseInMemoryDatabase(dbName));
                    services.AddHttpContextAccessor();
                    services.AddScoped<ICurrentUser, CookieCurrentUser>();
                    services.AddScoped<IAuthService, AuthService>();
                    services.AddScoped<IAuthSessionService, AuthSessionService>();
                    services.AddScoped<IMfaService, MfaService>();
                    services.AddScoped<IJwtTokenService, JwtTokenService>();
                    services.AddSingleton(Substitute.For<IAuditoriaService>());
                    services.AddSingleton(Substitute.For<IUsuariosService>());
                    var hasher = Substitute.For<IPasswordHasher>();
                    hasher.Verify("valid-password", "fixture-hash").Returns(true);
                    services.AddSingleton(hasher);
                    var protector = Substitute.For<ISecretProtector>();
                    protector.Protect(Arg.Any<string>()).Returns(c => "protected:" + c.Arg<string>());
                    protector.Unprotect(Arg.Any<string>()).Returns(c => c.Arg<string>().Replace("protected:", ""));
                    services.AddSingleton(protector);
                    var totp = Substitute.For<ITotpService>();
                    totp.GenerarSecreto().Returns("TESTONLYBASE32SECRET");
                    totp.Validar("TESTONLYBASE32SECRET", "123456").Returns(true);
                    totp.BuildOtpAuthUri(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns("otpauth://totp/test-only");
                    services.AddSingleton(totp);
                    services.Configure<JwtOptions>(o =>
                    {
                        o.Key = "test-only-signing-key-not-for-production-0123456789";
                        o.Issuer = "fixture"; o.Audience = "fixture"; o.ExpiryMinutes = 60; o.RefreshTokenExpiryDays = 14;
                    });
                    services.Configure<SecurityOptions>(_ => { });
                    services.Configure<SsoOptions>(_ => { });
                    services.AddAuthRateLimiting(config);
                    services.AddScoped<SessionCookieEvents>();
                    services.AddScoped<SessionJwtEvents>();
                    services.AddAuthentication(web ? CookieAuthenticationDefaults.AuthenticationScheme : JwtBearerDefaults.AuthenticationScheme)
                        .AddCookie(o =>
                        {
                            o.EventsType = typeof(SessionCookieEvents);
                            o.Cookie.Name = "fixture-auth"; o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                            o.LoginPath = "/Account/Login";
                        })
                        .AddJwtBearer(o =>
                        {
                            o.EventsType = typeof(SessionJwtEvents);
                            o.TokenValidationParameters = new TokenValidationParameters
                            {
                                ValidateIssuer = true, ValidateAudience = true, ValidateIssuerSigningKey = true, ValidateLifetime = true,
                                ValidIssuer = "fixture", ValidAudience = "fixture",
                                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("test-only-signing-key-not-for-production-0123456789"))
                            };
                        });
                    services.AddAuthorization();
                }).Configure(app =>
                {
                    app.Use((context, next) => { context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10"); return next(context); });
                    app.UseRouting();
                    app.UseRateLimiter();
                    app.UseAuthentication();
                    app.UseMiddleware<MfaChallengeMiddleware>();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapControllers();
                        endpoints.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
                        endpoints.MapGet("/fixture/private", () => "business-data").RequireAuthorization();
                        endpoints.MapPost("/fixture/private", () => "business-write").RequireAuthorization();
                        endpoints.MapGet("/fixture/public", () => "public-business").AllowAnonymous();
                        endpoints.MapGet("/fixture/static.css", () => "body{}").WithMetadata(
                            new AllowMfaChallengeAttribute(SessionClaims.MfaEnroll, SessionClaims.MfaVerify));
                        endpoints.MapGet("/health", () => "ok");
                    });
                })).Start();
            Client = Host.GetTestClient();
            Client.BaseAddress = new Uri("https://localhost");
            using var scope = Host.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NeoStpDbContext>();
            db.Empresas.Add(new Empresa { Id = 101, Nit = "fixture", RazonSocial = "Fixture", EstadoCodigo = "ACTIVA" });
            var role = new Rol { Id = 1, Codigo = platform ? "SUPERADMIN" : "OPERADOR", Nombre = "Fixture", Activo = true, EsSistema = platform, EmpresaId = platform ? null : 101 };
            db.Usuarios.Add(new Usuario
            {
                Id = 20, EmpresaId = platform ? null : 101, Username = "fixture", Email = "fixture@example.test",
                NombreCompleto = "Fixture", PasswordHash = "fixture-hash", EstadoCodigo = "ACTIVO",
                TipoUsuarioCodigo = platform ? "SUPERADMIN" : "OPERADOR",
                Roles = new List<UsuarioRol> { new() { Rol = role } },
                MfaHabilitado = mfa, MfaSecretoCifrado = mfa ? "protected:TESTONLYBASE32SECRET" : null,
                SsoProveedor = SsoProveedores.Entra, SsoSubject = "fixture-subject", SsoIssuer = SsoTestIdentity.Issuer
            });
            SsoTestIdentity.Configure(db, 101);
            db.SaveChanges();
        }

        public async Task<HttpResponseMessage> Send(HttpMethod method, string path, object? json = null,
            Dictionary<string, string>? form = null, bool antiForgery = true)
        {
            if (form is not null && antiForgery && csrf is not null) form["__RequestVerificationToken"] = csrf;
            var request = new HttpRequestMessage(method, path);
            if (json is not null) request.Content = JsonContent.Create(json);
            if (form is not null) request.Content = new FormUrlEncodedContent(form);
            if (Token is not null && !Web) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
            if (cookies.Count > 0) request.Headers.Add("Cookie", string.Join("; ", cookies.Values));
            var response = await Client.SendAsync(request);
            if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
                foreach (var cookie in setCookies)
                {
                    var pair = cookie.Split(';')[0];
                    cookies[pair.Split('=')[0]] = pair;
                }
            if (response.Content.Headers.ContentType?.MediaType == "text/html")
            {
                var html = await response.Content.ReadAsStringAsync();
                var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
                if (match.Success) csrf = WebUtility.HtmlDecode(match.Groups[1].Value);
            }
            return response;
        }

        public async Task<LoginResponse> ApiLogin(string? mfa = null)
        {
            var response = await Send(HttpMethod.Post, "/api/auth/login", new { usernameOrEmail = "fixture", password = "valid-password", mfaCode = mfa });
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var login = (await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>())!.Data!;
            Token = login.AccessToken;
            return login;
        }

        public async Task<HttpResponseMessage> WebLogin(string password = "valid-password", bool remember = false)
        {
            await Send(HttpMethod.Get, "/Account/Login");
            return await Send(HttpMethod.Post, "/Account/Login", form: new()
            {
                ["UsernameOrEmail"] = "fixture", ["Password"] = password, ["RememberMe"] = remember.ToString()
            });
        }

        public async Task ChangeUser(Action<Usuario> change)
        {
            using var scope = Host.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NeoStpDbContext>();
            change(await db.Usuarios.SingleAsync());
            await db.SaveChangesAsync();
        }

        public async Task<LoginResponse> Sso()
        {
            using var scope = Host.Services.CreateScope();
            var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
            var result = await auth.LoginExternoAsync(SsoTestIdentity.Info(), new AuthContext());
            result.IsSuccess.Should().BeTrue(result.Error);
            Token = result.Value!.AccessToken;
            if (Web)
            {
                var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
                context.Request.Scheme = "https";
                await SessionCookieSignIn.SignInAsync(context, result.Value.User);
                AuthCookie = context.Response.Headers.SetCookie.Single(c => c!.StartsWith("fixture-auth="))!.Split(';')[0];
            }
            return result.Value;
        }

        public void Dispose() { Client.Dispose(); Host.Dispose(); }
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "NeoSTP.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealJwtAndCookie_RejectSessionAfterPasswordChange(bool web)
    {
        using var f = new Fixture(web);
        if (web) (await f.WebLogin()).StatusCode.Should().Be(HttpStatusCode.Redirect);
        else await f.ApiLogin();
        (await f.Send(HttpMethod.Get, "/fixture/private")).StatusCode.Should().Be(HttpStatusCode.OK);
        await f.ChangeUser(u => u.PasswordHash = "changed-hash");
        (await f.Send(HttpMethod.Get, "/fixture/private")).StatusCode.Should().Be(web ? HttpStatusCode.Redirect : HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LogoutRevokesServerSession_AndCopiedCredentialCannotBeReused(bool web)
    {
        using var f = new Fixture(web);
        if (web) await f.WebLogin(); else await f.ApiLogin();
        var copiedCookie = f.AuthCookie;
        using var scope = f.Host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NeoStpDbContext>();
        var id = (await db.AuthSessions.SingleAsync()).Id;
        if (web)
        {
            // Refresh the anti-forgery token for the authenticated identity.
            await f.Send(HttpMethod.Get, "/Account/MfaEnrollment");
            (await f.Send(HttpMethod.Post, "/Account/Logout", form: new())).StatusCode.Should().Be(HttpStatusCode.Redirect);
        }
        else (await f.Send(HttpMethod.Post, "/api/auth/logout", new { })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await new AuthSessionService(db).ValidateAsync(id)).IsFailure.Should().BeTrue();
        f.AuthCookie = copiedCookie;
        (await f.Send(HttpMethod.Get, "/fixture/private")).StatusCode.Should().Be(web ? HttpStatusCode.Redirect : HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PlatformWithoutMfaReceivesFullSessionAndCanReachBusiness()
    {
        using var f = new Fixture(platform: true);
        var login = await f.ApiLogin();
        login.User.SessionPurpose.Should().Be(SessionClaims.Full);
        login.MfaEnrollmentRequired.Should().BeFalse();
        login.RefreshToken.Should().NotBeEmpty();
        (await f.Send(HttpMethod.Post, "/fixture/private")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ApiEnrollment_ConfirmsMfa_RejectsOldJwt_AndRequiresCodeOnNextLogin()
    {
        using var f = new Fixture(platform: true);
        var login = await f.ApiLogin();
        (await f.Send(HttpMethod.Post, "/api/auth/mfa/enroll", new { })).StatusCode.Should().Be(HttpStatusCode.OK);
        var confirm = await f.Send(HttpMethod.Post, "/api/auth/mfa/confirm", new { code = "123456" });
        confirm.StatusCode.Should().Be(HttpStatusCode.OK);
        confirm.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await confirm.Content.ReadFromJsonAsync<ApiResponse<MfaConfirmDto>>())!.Data!.RecoveryCodes.Should().HaveCount(10);
        (await f.Send(HttpMethod.Get, "/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        f.Token = null;
        var denied = await f.Send(HttpMethod.Post, "/api/auth/login", new { usernameOrEmail = "fixture", password = "valid-password" });
        (await denied.Content.ReadAsStringAsync()).Should().Contain("segundo factor");
        var complete = await f.ApiLogin("123456");
        complete.User.Roles.Should().Contain("SUPERADMIN");
        var me = await f.Send(HttpMethod.Get, "/api/auth/me");
        me.StatusCode.Should().Be(HttpStatusCode.OK);
        (await me.Content.ReadFromJsonAsync<ApiResponse<UserInfo>>())!.Data!.SessionId.Should().Be(complete.User.SessionId);
        (await f.Send(HttpMethod.Post, "/api/auth/mfa/disable", new { code = "123456" })).StatusCode.Should().Be(HttpStatusCode.OK);
        f.Token = null;
        var withoutMfa = await f.ApiLogin();
        withoutMfa.MfaVerificationRequired.Should().BeFalse();
    }

    [Fact]
    public async Task ApiFullSessionCanLogoutWithNoBodyOrRefreshToken()
    {
        using var f = new Fixture(platform: true);
        await f.ApiLogin();
        (await f.Send(HttpMethod.Post, "/api/auth/logout")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await f.Send(HttpMethod.Post, "/api/auth/mfa/enroll", new { })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SsoChallenge_VerificationIsOnlyPathToFullApiAccess()
    {
        using var f = new Fixture(mfa: true);
        var challenge = await f.Sso();
        challenge.MfaVerificationRequired.Should().BeTrue();
        (await f.Send(HttpMethod.Post, "/api/auth/mfa/enroll", new { })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var verified = await f.Send(HttpMethod.Post, "/api/auth/mfa/verify", new { code = "123456" });
        verified.StatusCode.Should().Be(HttpStatusCode.OK);
        var full = (await verified.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>())!.Data!;
        (await f.Send(HttpMethod.Post, "/api/auth/mfa/verify", new { code = "123456" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        f.Token = full.AccessToken;
        (await f.Send(HttpMethod.Get, "/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WebSsoChallenge_VerificationReplacesRestrictedCookieAndConsumesChallenge()
    {
        using var f = new Fixture(web: true, mfa: true);
        await f.Sso();
        var copied = f.AuthCookie;
        var redirect = await f.Send(HttpMethod.Get, "/fixture/private");
        redirect.Headers.Location!.OriginalString.Should().Be("/Account/MfaVerification");
        await f.Send(HttpMethod.Get, "/Account/MfaVerification");
        (await f.Send(HttpMethod.Post, "/Account/MfaVerification", form: new() { ["Code"] = "123456" }, antiForgery: false)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var verified = await f.Send(HttpMethod.Post, "/Account/MfaVerification", form: new() { ["Code"] = "123456" });
        verified.StatusCode.Should().Be(HttpStatusCode.Redirect);
        verified.Headers.Location!.OriginalString.Should().Be("/");
        (await f.Send(HttpMethod.Get, "/fixture/private")).StatusCode.Should().Be(HttpStatusCode.OK);
        f.AuthCookie = copied;
        (await f.Send(HttpMethod.Get, "/Account/MfaVerification")).StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task WebEnrollment_IsOptional_UsesAntiforgery_AndShowsRecoveryOnlyOnce()
    {
        using var f = new Fixture(web: true, platform: true);
        var login = await f.WebLogin(remember: true);
        login.Headers.Location!.OriginalString.Should().Be("/");
        login.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("fixture-auth=")).Should().Contain("expires=");
        (await f.Send(HttpMethod.Get, "/fixture/private")).StatusCode.Should().Be(HttpStatusCode.OK);
        var landing = await f.Send(HttpMethod.Get, "/Account/MfaEnrollment");
        (await landing.Content.ReadAsStringAsync()).Should().NotContain("TESTONLYBASE32SECRET");
        (await f.Send(HttpMethod.Post, "/Account/MfaBegin", form: new(), antiForgery: false)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var begin = await f.Send(HttpMethod.Post, "/Account/MfaBegin", form: new());
        begin.StatusCode.Should().Be(HttpStatusCode.OK);
        begin.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await begin.Content.ReadAsStringAsync()).Should().Contain("TESTONLYBASE32SECRET");
        var confirm = await f.Send(HttpMethod.Post, "/Account/MfaConfirm", form: new() { ["Code"] = "123456" });
        confirm.StatusCode.Should().Be(HttpStatusCode.OK);
        confirm.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await confirm.Content.ReadAsStringAsync()).Should().Contain("recovery-codes");
        (await f.Send(HttpMethod.Get, "/Account/MfaEnrollment")).StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await f.Send(HttpMethod.Get, "/fixture/private")).StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task WebLoginLimit_Returns429_WithoutDisablingLoginGetOrHealth()
    {
        using var f = new Fixture(web: true, permits: 2);
        (await f.WebLogin("wrong")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await f.WebLogin("wrong")).StatusCode.Should().Be(HttpStatusCode.OK);
        var limited = await f.WebLogin("wrong");
        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        limited.Headers.RetryAfter.Should().NotBeNull();
        limited.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await limited.Content.ReadAsStringAsync()).Should().Contain("Demasiados intentos");
        (await f.Send(HttpMethod.Get, "/Account/Login")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await f.Send(HttpMethod.Get, "/health")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WebMfaEndpointsShareBudget_AndLogoutStillWorks()
    {
        using var f = new Fixture(web: true, platform: true, permits: 2);
        await f.WebLogin();
        await f.Send(HttpMethod.Get, "/Account/MfaEnrollment");
        (await f.Send(HttpMethod.Post, "/Account/MfaBegin", form: new())).StatusCode.Should().Be(HttpStatusCode.OK);
        (await f.Send(HttpMethod.Post, "/Account/MfaConfirm", form: new() { ["Code"] = "000000" })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await f.Send(HttpMethod.Post, "/Account/MfaBegin", form: new())).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await f.Send(HttpMethod.Post, "/Account/Logout", form: new())).StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task SignedLegacyJwtWithoutSessionClaims_IsRejected()
    {
        using var f = new Fixture();
        var issued = await f.ApiLogin();
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(issued.AccessToken);
        var legacy = new JwtSecurityToken("fixture", "fixture",
            jwt.Claims.Where(c => c.Type != SessionClaims.Id && c.Type != SessionClaims.Purpose),
            expires: DateTime.UtcNow.AddMinutes(10), signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes("test-only-signing-key-not-for-production-0123456789")), SecurityAlgorithms.HmacSha256));
        f.Token = handler.WriteToken(legacy);
        (await f.Send(HttpMethod.Get, "/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedLegacyCookieWithoutSessionClaims_IsRejected()
    {
        using var f = new Fixture(web: true);
        using var scope = f.Host.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "20"), new Claim("tipo_usuario", "OPERADOR"), new Claim("empresa_id", "101")
        }, CookieAuthenticationDefaults.AuthenticationScheme)),
            new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1) }, CookieAuthenticationDefaults.AuthenticationScheme);
        f.AuthCookie = "fixture-auth=" + options.TicketDataFormat.Protect(ticket);
        (await f.Send(HttpMethod.Get, "/fixture/private")).StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealCredentialsAreRejectedWhenUserIsBlocked(bool web)
    {
        using var f = new Fixture(web);
        if (web) await f.WebLogin(); else await f.ApiLogin();
        await f.ChangeUser(u => u.EstadoCodigo = "BLOQUEADO");
        (await f.Send(HttpMethod.Get, "/fixture/private")).StatusCode.Should().Be(web ? HttpStatusCode.Redirect : HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RealCredentialsRevalidateRoleAndCompanyOnNextRequest(bool web, bool suspendCompany)
    {
        using var f = new Fixture(web);
        if (web) await f.WebLogin(); else await f.ApiLogin();
        using (var scope = f.Host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NeoStpDbContext>();
            if (suspendCompany) (await db.Empresas.SingleAsync()).EstadoCodigo = "SUSPENDIDA";
            else (await db.Roles.SingleAsync()).Activo = false;
            await db.SaveChangesAsync();
        }
        (await f.Send(HttpMethod.Get, "/fixture/private")).StatusCode.Should().Be(web ? HttpStatusCode.Redirect : HttpStatusCode.Unauthorized);
    }

    [Fact]
    public void HostsWireSessionValidationAndGateBeforeBusinessMiddleware()
    {
        foreach (var host in new[] { "Api", "Web" })
        {
            var program = File.ReadAllText(Path.Combine(RepoRoot(), "src", "NeoSTP." + host, "Program.cs"));
            program.Should().Contain(host == "Api" ? "options.EventsType = typeof(SessionJwtEvents);" : "options.EventsType = typeof(SessionCookieEvents);");
            var ordered = new[] { "app.UseRouting();", "app.UseRateLimiter();", "app.UseAuthentication();", "app.UseMiddleware<MfaChallengeMiddleware>();", "app.UseAuthorization();" };
            ordered.Select(s => program.IndexOf(s, StringComparison.Ordinal)).Should().OnlyContain(i => i >= 0).And.BeInAscendingOrder();
            if (host == "Web") program.Should().Contain("builder.Services.AddAuthRateLimiting(builder.Configuration);");
        }
    }
}
