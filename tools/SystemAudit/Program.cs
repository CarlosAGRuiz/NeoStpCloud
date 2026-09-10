using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Auth;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Auth.Dtos;
using NeoSTP.Application.Common;
using NeoSTP.Application.Ops;
using NeoSTP.Application.Usuarios.Dtos;
using NeoSTP.Application.Roles.Dtos;
using NeoSTP.Application.Connect;
using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Seguridad;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Auth;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NeoSTP.Api.Authorization;
using NSubstitute;

// This harness uses only isolated EF InMemory databases. No app startup, real
// connection strings, network calls, production credentials or business data.
var output = Path.GetFullPath(args.Single());
Directory.CreateDirectory(output);
var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
var routes = new List<object>();
foreach (var assembly in new[] { typeof(NeoSTP.Api.Controllers.DteController).Assembly, typeof(NeoSTP.Web.Controllers.AccountController).Assembly })
foreach (var type in assembly.GetTypes().Where(t => !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t)).OrderBy(t => t.Name))
{
    var classAuth = type.GetCustomAttributes<AuthorizeAttribute>(true).ToArray();
    var classRoutes = type.GetCustomAttributes<RouteAttribute>(true).Select(r => r.Template).DefaultIfEmpty("[controller]/[action]").ToArray();
    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
    {
        if (method.IsSpecialName || method.IsDefined(typeof(NonActionAttribute), true)) continue;
        var verbs = method.GetCustomAttributes<HttpMethodAttribute>(true).ToArray();
        var combinedAuth = classAuth.Concat(method.GetCustomAttributes<AuthorizeAttribute>(true)).ToArray();
        var anonymous = type.IsDefined(typeof(AllowAnonymousAttribute), true) || method.IsDefined(typeof(AllowAnonymousAttribute), true);
        routes.Add(new {
            Surface = assembly.GetName().Name, Controller = type.Name, Action = method.Name,
            ClassRoutes = classRoutes, Http = verbs.Select(v => new { Methods = v.HttpMethods, Route = v.Template }).ToArray(),
            Anonymous = anonymous, Authorization = combinedAuth.Select(a => new { a.Policy, a.Roles, a.AuthenticationSchemes }).ToArray(),
            OtherFilters = type.GetCustomAttributes(true).Concat(method.GetCustomAttributes(true)).Select(a => a.GetType().Name)
                .Where(n => n.Contains("Modulo") || n.Contains("Forgery")).Distinct().ToArray()
        });
    }
}
await File.WriteAllTextAsync(Path.Combine(output, "inventario-controladores.json"), JsonSerializer.Serialize(routes, jsonOptions));

var observations = new List<object>();
var actor = "auditoria-aislada";
var password = "Audit-Only!2026-Strong";
using var db = new NeoStpDbContext(new DbContextOptionsBuilder<NeoStpDbContext>().UseInMemoryDatabase($"audit-{Guid.NewGuid()}").Options);
var audit = Substitute.For<IAuditoriaService>();
var hasher = new BcryptPasswordHasher();
var policy = Substitute.For<IPasswordPolicy>();
policy.Validate(Arg.Any<string>()).Returns(Result.Ok());
db.Empresas.AddRange(new Empresa { Id = 1001, Nit = "00000000000001", RazonSocial = "Audit A" }, new Empresa { Id = 1002, Nit = "00000000000002", RazonSocial = "Audit B" });
db.Roles.AddRange(new Rol { Id=501, Codigo="SUPERADMIN", Nombre="Global admin", EmpresaId=null, EsSistema=true, Activo=true }, new Rol { Id=502, Codigo="MEMBER", Nombre="Member", EmpresaId=1002, Activo=true });
await db.SaveChangesAsync();
var current = Substitute.For<ICurrentUser>();
current.IsAuthenticated.Returns(true);
current.UserId.Returns(99);
current.EmpresaId.Returns(1001);
current.TipoUsuarioCodigo.Returns("ADMIN");
var users = new UsuariosService(db, hasher, audit, policy, currentUser: current);
var created = await users.CreateAsync(1001, new CreateUsuarioRequest { Username="audit-user", Email="audit-user@example.invalid", NombreCompleto="Audit user", Password=password, TipoUsuarioCodigo="SUPERADMIN", RoleIds=new List<int>{501} }, actor);
observations.Add(new { Id="SEC-01", Test="Tenant can assign global SUPERADMIN type and role", Accepted=created.IsSuccess, Type=created.Value?.TipoUsuarioCodigo, Roles=created.Value?.RoleCodigos });
var claims = new ClaimsPrincipal(new ClaimsIdentity(new[] {new Claim(ClaimTypes.Role, created.Value?.RoleCodigos.FirstOrDefault() ?? "NONE")}, "audit"));
var requirement = new PermisoRequirement("SuperAdmin.Planes.Administrar");
var authorization = new AuthorizationHandlerContext(new[] {requirement}, claims, null);
await new PermisoAuthorizationHandler().HandleAsync(authorization);
observations.Add(new { Id="SEC-01", Test="Assigned role passes global plan permission without explicit grant", Granted=authorization.HasSucceeded });
var roles = new RolesService(db, audit, current);
var reserved = await roles.CreateAsync(1001, new CreateRolRequest { Codigo="SUPERADMIN", Nombre="Tenant-defined reserved role" }, actor);
observations.Add(new { Id="SEC-01", Test="Tenant can create reserved role name", Accepted=reserved.IsSuccess });

var jwt = Substitute.For<IJwtTokenService>();
jwt.CreateAccessToken(Arg.Any<UserInfo>()).Returns(("audit-no-real-token",DateTime.UtcNow.AddMinutes(5)));
jwt.CreateRefreshToken().Returns(_=>Guid.NewGuid().ToString("N"));
var mfa = Substitute.For<IMfaService>();
mfa.VerificarCodigoLoginAsync(Arg.Any<int>(),Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(Result.Fail("Invalid", "MFA_INVALID"));
var auth = new AuthService(db, hasher, jwt, audit, mfa, Options.Create(new JwtOptions { RefreshTokenExpiryDays=1 }), Options.Create(new SecurityOptions()), NullLogger<AuthService>.Instance);
if (created.IsFailure)
{
    var baseline = await users.CreateAsync(1001, new CreateUsuarioRequest
    {
        Username="audit-baseline", Email="audit-baseline@example.invalid", NombreCompleto="Audit baseline",
        Password=password, TipoUsuarioCodigo="OPERADOR"
    }, actor);
    if (baseline.IsFailure) throw new InvalidOperationException("The legitimate baseline user could not be created.");
}
var u = await db.Usuarios.SingleAsync();
u.TipoUsuarioCodigo="OPERADOR";
u.Roles.Clear();
u.EstadoCodigo=EstadoCodes.Bloqueado;
u.BloqueadoHasta=DateTime.UtcNow.AddMinutes(-1);
await db.SaveChangesAsync();
var expired = await auth.LoginAsync(new LoginRequest {UsernameOrEmail=u.Username, Password=password}, new AuthContext());
observations.Add(new { Id="AUTH-01", Test="Correct password after temporary lock expires", Accepted=expired.IsSuccess, expired.ErrorCode });
u.EstadoCodigo=EstadoCodes.Activo;
u.BloqueadoHasta=null;
u.MfaHabilitado=true;
u.IntentosFallidos=0;
await db.SaveChangesAsync();
for(var i=0;i<6;i++) await auth.LoginAsync(new LoginRequest {UsernameOrEmail=u.Username, Password=password, MfaCode="000000"},new AuthContext());
observations.Add(new { Id="AUTH-02", Test="Six incorrect MFA attempts", FailedCounter=u.IntentosFallidos, Locked=u.BloqueadoHasta.HasValue });
// Nueva reproducción independiente: el bloqueo MFA anterior ya fue observado.
u.MfaHabilitado=false;
u.EstadoCodigo=EstadoCodes.Activo;
u.BloqueadoHasta=null;
u.IntentosFallidos=0;
db.UsuarioEmpresas.Add(new UsuarioEmpresa { UsuarioId=u.Id, EmpresaId=1002, RolId=502, EstadoCodigo="ACTIVO" });
await db.SaveChangesAsync();
var switched=await auth.CambiarEmpresaAsync(u.Id,1002,new AuthContext());
var refreshed=await auth.RefreshAsync(switched.Value!.RefreshToken,new AuthContext());
observations.Add(new {Id="TENANT-01",Test="Refresh preserves selected tenant", Selected=switched.Value.User.EmpresaId, AfterRefresh=refreshed.Value?.User.EmpresaId});

var httpFactory=Substitute.For<IHttpClientFactory>();
var webhooks=new ConnectWebhookService(db,httpFactory,NullLogger<ConnectWebhookService>.Instance);
var internalTarget=await webhooks.CrearAsync(new CrearWebhookRequest {EmpresaId=1001,Url="http://127.0.0.1:5058/health",Eventos=new[]{"TEST"}},actor);
observations.Add(new {Id="CONNECT-01",Test="Internal webhook destination accepted (no HTTP request sent)",Accepted=internalTarget.IsSuccess});

await File.WriteAllTextAsync(Path.Combine(output,"reproducciones-aisladas.json"),JsonSerializer.Serialize(observations,jsonOptions));
Console.WriteLine(JsonSerializer.Serialize(new { ControllerActions=routes.Count,Observations=observations },jsonOptions));
