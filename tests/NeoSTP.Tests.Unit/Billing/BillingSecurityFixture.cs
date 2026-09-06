using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Billing;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Domain.Core.Billing;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Licenciamiento;
using NeoSTP.Domain.Core.Seguridad;
using NeoSTP.Infrastructure.Billing;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Web.Auth;
using NSubstitute;

namespace NeoSTP.Tests.Unit.Billing;

internal sealed class BillingSecurityFixture : IAsyncDisposable
{
    public const int EmpresaA = 991001, EmpresaB = 991002, Basic = 993001, Pro = 993002;
    public const int Central = 992001, AdminA = 992002, AdminB = 992003, OperatorA = 992004;
    public string DatabaseName { get; } = "billing-security-" + Guid.NewGuid().ToString("N");
    public InMemoryDatabaseRoot Store { get; } = new();
    public NeoStpDbContext Db { get; }
    public IOptions<BillingOptions> Options { get; } = Microsoft.Extensions.Options.Options.Create(
        new BillingOptions { Provider = "Mock", TrialDays = 14 });
    public IEmailSender Email { get; } = Substitute.For<IEmailSender>();
    public IPaymentProviderResolver Payments { get; }

    public BillingSecurityFixture()
    {
        Db = new(new DbContextOptionsBuilder<NeoStpDbContext>().UseInMemoryDatabase(DatabaseName, Store).Options);
        Db.Empresas.AddRange(new Empresa { Id = EmpresaA, Nit = "AUDIT-A", RazonSocial = "Synthetic A", EstadoCodigo = "ACTIVA" },
            new Empresa { Id = EmpresaB, Nit = "AUDIT-B", RazonSocial = "Synthetic B", EstadoCodigo = "ACTIVA" });
        Db.Planes.AddRange(new Plan { Id = Basic, Codigo = "AUDIT_BASIC", Nombre = "Basic", PrecioMensual = 10 },
            new Plan { Id = Pro, Codigo = "AUDIT_PRO", Nombre = "Pro", PrecioMensual = 50 });
        var centralRole = new Rol { Id = 994001, Codigo = "SUPERADMIN", Nombre = "Central", EsSistema = true, Activo = true };
        Db.Roles.Add(centralRole);
        Db.Usuarios.AddRange(User(Central, "SUPERADMIN", null, centralRole), User(AdminA, "ADMIN", EmpresaA),
            User(AdminB, "ADMIN", EmpresaB), User(OperatorA, "OPERADOR", EmpresaA));
        Db.SaveChanges();
        Payments = new PaymentProviderResolver(new IPaymentProvider[] { new MockPaymentProvider(), new TransferenciaPaymentProvider() }, Options);
    }

    public BillingService Service(int userId) => new(Db, Payments, Email, Options, Identity(userId));
    public ICurrentUser Identity(int userId)
    {
        var user = Db.Usuarios.Include(u => u.Roles).ThenInclude(r => r.Rol).Single(u => u.Id == userId);
        return CurrentUser(user.Id, user.TipoUsuarioCodigo, user.EmpresaId);
    }
    public static ICurrentUser CurrentUser(int id, string type, int? empresaId)
        => new CookieCurrentUser(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = Principal(id, type, empresaId) } });
    public static ClaimsPrincipal Principal(int id, string type, int? empresaId)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, id.ToString()), new(ClaimTypes.Name, "claims-name"),
            new("tipo_usuario", type), new(ClaimTypes.Role, type) };
        if (empresaId.HasValue) claims.Add(new("empresa_id", empresaId.Value.ToString()));
        return new(new ClaimsIdentity(claims, "SyntheticBilling"));
    }
    public static Usuario User(int id, string type, int? empresaId, Rol? role = null) => new()
    {
        Id = id, EmpresaId = empresaId, Username = "audit-user-" + id, Email = "audit@example.invalid",
        PasswordHash = "unused-synthetic", NombreCompleto = "Synthetic Billing User", TipoUsuarioCodigo = type,
        EstadoCodigo = "ACTIVO", Roles = role is null ? [] : [new UsuarioRol { Rol = role, RolId = role.Id, UsuarioId = id }],
    };
    public async Task<BillingPayment> Pending(int empresaId)
    {
        var customer = new BillingCustomer { EmpresaId = empresaId, Email = "audit@example.invalid", Provider = "Transferencia" };
        var subscription = new BillingSubscription { Customer = customer, PlanId = Basic, Status = SubscriptionStatus.Incomplete };
        var payment = new BillingPayment { Subscription = subscription, Amount = 10, Currency = "USD", Metodo = "TRANSFERENCIA", Status = "PENDIENTE_VERIFICACION" };
        Db.BillingPayments.Add(payment);
        await Db.SaveChangesAsync();
        return payment;
    }
    public ValueTask DisposeAsync() => Db.DisposeAsync();
}
