using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using Microsoft.Extensions.Configuration;
using NeoSTP.Application.Common;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Domain.Core.Auditoria;
using NeoSTP.Domain.Core.Billing;
using NeoSTP.Domain.Core.Licenciamiento;
using NeoSTP.Infrastructure.Billing;
using NeoSTP.Infrastructure.Persistence;

// Explicit, tenant-specific administrative reconciliation. Preview is the default.
// No HTTP/provider calls, schema updates, deletion, secret output or fabricated payments.
var apply = args.Contains("--apply");
string? Argument(string name) { var i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
var root = Argument("--settings-root") ?? throw new ArgumentException("--settings-root is required");
var expected = Argument("--expected-fingerprint");
var actorId = int.TryParse(Argument("--actor-id"), out var id) ? id : 0;
const string key = "CLI23-CALENDAR-2026-09-V1";
try
{
    var config = new ConfigurationBuilder().SetBasePath(Path.GetFullPath(root)).AddJsonFile("appsettings.json")
        .AddJsonFile("appsettings.Development.json", true).AddJsonFile("appsettings.Local.json", true).Build();
    var builder = new SqlConnectionStringBuilder(config.GetConnectionString("NeoStpDb"));
    if (builder.InitialCatalog != "NeoSTP_Cloud" || builder.AttachDBFilename.Length > 0
        || !new[] { ".", "(local)", "localhost", "127.0.0.1", Environment.MachineName }.Contains(builder.DataSource, StringComparer.OrdinalIgnoreCase))
        throw new InvalidOperationException("CLI23_SOURCE_DATABASE_TARGET_REJECTED");
    var rehearsalDatabase = Argument("--rehearsal-database");
    if (args.Contains("--allow-rehearsal"))
    {
        if (rehearsalDatabase is null || !System.Text.RegularExpressions.Regex.IsMatch(rehearsalDatabase, "^NeoClientBilling92_[a-f0-9]{32}$"))
            throw new InvalidOperationException("CLI23_REHEARSAL_TARGET_REJECTED");
        builder.InitialCatalog = rehearsalDatabase;
    }
    else if (rehearsalDatabase != null) throw new InvalidOperationException("CLI23_REHEARSAL_FLAG_REQUIRED");
    if ((builder.InitialCatalog != "NeoSTP_Cloud" && builder.InitialCatalog != rehearsalDatabase) || builder.AttachDBFilename.Length > 0
        || !new[] { ".", "(local)", "localhost", "127.0.0.1", Environment.MachineName }.Contains(builder.DataSource, StringComparer.OrdinalIgnoreCase))
        throw new InvalidOperationException("CLI23_DATABASE_TARGET_REJECTED");
    builder.ApplicationName = "NeoSTP CLI23 Calendar " + (apply ? "Apply" : "Preview");
    await using var db = new NeoStpDbContext(new DbContextOptionsBuilder<NeoStpDbContext>().UseSqlServer(builder.ConnectionString).Options);
    if (args.Contains("--verify-calendar-rehearsal"))
    {
        if (rehearsalDatabase == null || !apply) throw new InvalidOperationException("CLI23_REHEARSAL_REQUIRED");
        await CalendarRehearsal.VerifyAsync(builder.ConnectionString);
        return 0;
    }
    if (int.TryParse(Argument("--pay-period-id"), out var payPeriodId))
    {
        if (!apply || !decimal.TryParse(Argument("--amount"), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var amount)
            || !DateTime.TryParse(Argument("--paid-at-utc"), null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var paidAt))
            throw new InvalidOperationException("Use --apply --pay-period-id --amount --reference --paid-at-utc --actor-id for a verified real payment.");
        var identity = await db.Empresas.AsNoTracking().SingleAsync(x => x.Id == 23);
        if (identity.Nit.Replace("-", "").Replace(" ", "") != "06232705261148") throw new InvalidOperationException("CLI23_TENANT_DRIFT");
        var paymentResult = await new BillingCalendarService(db, new CliActor(actorId)).ApplyVerifiedPaymentAsync(23, payPeriodId,
            amount, "USD", Argument("--reference") ?? string.Empty, DateTime.SpecifyKind(paidAt, DateTimeKind.Utc));
        Console.WriteLine(paymentResult.IsSuccess ? "CLI23_PAYMENT_APPLIED" : paymentResult.ErrorCode);
        return paymentResult.IsSuccess ? 0 : 1;
    }
    var result = await BillingCompanyTransaction.RunAsync(db, 23, async () =>
    {
        var company = await db.Empresas.AsNoTracking().SingleAsync(x => x.Id == 23);
        var fiscal = await db.DteConfiguracion.AsNoTracking().SingleAsync(x => x.EmpresaId == 23);
        if (company.Nit.Replace("-", "").Replace(" ", "") != "06232705261148" || company.EstadoCodigo != "ACTIVA"
            || fiscal.AmbienteCodigo != "PRUEBAS" || fiscal.TiposDteAutorizadosCsv != "01,03,11,14")
            return Result.Fail("CLI23_TENANT_DRIFT");
        var licenses = await db.EmpresaPlanes.Where(x => x.EmpresaId == 23).ToListAsync();
        var subscriptions = await db.BillingSubscriptions.Include(x => x.Customer).Where(x => x.Customer.EmpresaId == 23).ToListAsync();
        var plan = await db.Planes.AsNoTracking().SingleAsync(x => x.Id == 207);
        if (licenses.Count != 1 || subscriptions.Count != 1 || licenses[0].Id != 29 || subscriptions[0].Id != 2
            || subscriptions[0].BillingCustomerId != 2 || licenses[0].PlanId != 207 || subscriptions[0].PlanId != 207
            || plan.Codigo != "STARTERFE" || plan.PrecioMensual != 15 || plan.MonedaCodigo != "USD" || !plan.Activo
            || plan.LimiteUsuarios != 3 || plan.LimiteSucursales != 1 || plan.LimitePuntosVenta != 2 || plan.LimiteDteMensual != 100)
            return Result.Fail("CLI23_COMMERCIAL_IDENTITY_DRIFT");
        var license = licenses[0]; var sub = subscriptions[0];
        var payments = await db.BillingPayments.AsNoTracking().Where(x => x.Subscription.Customer.EmpresaId == 23)
            .OrderBy(x => x.Id).Select(x => new { x.Id, x.Amount, x.Currency, x.Status, x.PaidAt }).ToListAsync();
        var invoices = await db.BillingInvoices.AsNoTracking().Where(x => x.Subscription.Customer.EmpresaId == 23)
            .OrderBy(x => x.Id).Select(x => new { x.Id, x.Amount, x.Currency, x.Status, x.DueDate, x.PaidAt }).ToListAsync();
        var checkoutCount = await db.BillingCheckoutIntents.CountAsync(x => x.EmpresaId == 23);
        var paymentApplicationCount = await db.BillingPaymentApplications.CountAsync(x => x.CheckoutIntent.EmpresaId == 23);
        var pendingOperations = await db.BillingProviderOperations.CountAsync(x => x.EmpresaId == 23 && x.Status != BillingProviderOperationStatuses.Completed);
        var modules = await db.EmpresaModulos.AsNoTracking().Where(x => x.EmpresaId == 23).OrderBy(x => x.ModuloId)
            .Select(x => new { x.ModuloId, x.Activo, x.FechaActivacion, x.FechaInactivacion, x.ComplementoAutorizado,
                x.ComplementoAutorizadoAt, x.ComplementoAutorizadoBy, x.ComplementoMotivo }).ToListAsync();
        var snapshot = new { EmpresaId = 23, License = new { license.Id, license.PlanId, license.EstadoCodigo, license.FechaInicio, license.FechaFin, license.UpdatedAt },
            Subscription = new { sub.Id, sub.PlanId, sub.Status, sub.TrialStart, sub.TrialEnd, sub.CurrentPeriodStart, sub.CurrentPeriodEnd,
                sub.CanceledAt, sub.CancelAtPeriodEnd, sub.UpdatedAt, sub.Customer.Provider, HasExternalSubscription = !string.IsNullOrEmpty(sub.ExternalSubscriptionId) },
            payments, invoices, checkoutCount, paymentApplicationCount, pendingOperations, modules };
        var serialized = JsonSerializer.Serialize(snapshot);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(serialized)));
        var existing = await db.BillingCalendarAgreements.SingleOrDefaultAsync(x => x.EmpresaId == 23);
        Console.WriteLine(JsonSerializer.Serialize(new { Mode = apply ? "APPLY" : "PREVIEW", Database = builder.InitialCatalog,
            Rehearsal = rehearsalDatabase != null, Fingerprint = fingerprint, Snapshot = snapshot,
            Proposal = new { AgreementKey = key, Status = "ACTIVE", MonthlyAmount = 15, Currency = "USD", Due = "Last local day of each month", TimeZone = "America/El_Salvador",
                FirstMonth = "2026-09", LicenseEnd = (DateTime?)null, AutomaticSuspension = false, Modules = new[] { "EVENTOSDTE", "CONTINGENCIA" }, PaymentsCreated = 0 } }));
        if (!apply) return Result.Ok();
        var actor = await db.Usuarios.Include(x => x.Roles).ThenInclude(x => x.Rol).SingleOrDefaultAsync(x => x.Id == actorId);
        if (actor is null || actor.EmpresaId != null || actor.EstadoCodigo != "ACTIVO"
            || !actor.Roles.Any(x => x.Rol.Activo && x.Rol.EsSistema && x.Rol.Codigo == "SUPERADMIN")) return Result.Fail("CLI23_ACTOR_REJECTED");
        if (existing != null)
        {
            var initialPeriod = await db.BillingCalendarPeriods.Include(x => x.Invoice).SingleOrDefaultAsync(x => x.AgreementId == existing.Id && x.PeriodStartLocal == new DateOnly(2026, 9, 1));
            var grants = await db.EmpresaModulos.Include(x => x.Modulo).Where(x => x.EmpresaId == 23 && (x.Modulo.Codigo == "EVENTOSDTE" || x.Modulo.Codigo == "CONTINGENCIA")).ToListAsync();
            var valid = existing.AgreementKey == key && existing.Active && !existing.AutomaticSuspension && existing.MonthlyAmount == 15
                && existing.Currency == "USD" && existing.PlanId == 207 && existing.EmpresaPlanId == 29 && existing.BillingSubscriptionId == 2
                && existing.FirstPeriodStartLocal == new DateOnly(2026, 9, 1) && existing.TimeZoneId == "America/El_Salvador"
                && sub.Status == "ACTIVE" && license.EstadoCodigo == "ACTIVO" && license.FechaFin == null
                && initialPeriod != null && initialPeriod.DueLocalDate == new DateOnly(2026, 9, 30)
                && initialPeriod.Invoice.BillingSubscriptionId == 2 && initialPeriod.Invoice.Amount == 15 && initialPeriod.Invoice.Currency == "USD"
                && initialPeriod.Invoice.Status is "OPEN" or "PAID"
                && grants.Count == 2 && grants.All(x => x.Activo && x.FechaInactivacion == null && x.Modulo.Activo && EmpresaModuloEntitlements.HasGrant(x));
            Console.WriteLine(valid ? "CLI23_ALREADY_APPLIED" : "CLI23_AGREEMENT_DRIFT");
            return valid ? Result.Ok() : Result.Fail("CLI23_AGREEMENT_DRIFT");
        }
        if (expected != fingerprint) return Result.Fail("CLI23_FINGERPRINT_DRIFT");
        if (payments.Count != 0 || invoices.Count != 0 || checkoutCount != 0 || paymentApplicationCount != 0 || pendingOperations != 0
            || sub.Status != "TRIALING" || sub.Customer.Provider != "Mock" || !string.IsNullOrEmpty(sub.ExternalSubscriptionId)
            || sub.CurrentPeriodStart != null || sub.CurrentPeriodEnd != null || sub.CancelAtPeriodEnd || sub.CanceledAt != null
            || license.EstadoCodigo != "ACTIVO" || license.FechaFin?.Date != new DateTime(2026, 9, 20))
            return Result.Fail("CLI23_BASELINE_DRIFT");
        var grantModules = await db.Modulos.Where(x => x.Activo && (x.Codigo == "EVENTOSDTE" || x.Codigo == "CONTINGENCIA")).ToListAsync();
        if (grantModules.Count != 2) return Result.Fail("CLI23_MODULE_CATALOG_DRIFT");
        var now = DateTime.UtcNow;
        var agreement = new BillingCalendarAgreement { EmpresaId = 23, BillingSubscriptionId = 2, EmpresaPlanId = 29, PlanId = 207,
            MonthlyAmount = 15, Currency = "USD", FirstPeriodStartLocal = new DateOnly(2026, 9, 1), AgreementKey = key,
            Reason = "Acuerdo autorizado: cobro mensual vencido a fin de mes, sin agosto/prorrateo/recargos ni suspensión automática.", CreatedBy = actor.Id.ToString() };
        db.BillingCalendarAgreements.Add(agreement);
        db.BillingCalendarPeriods.Add(BillingCalendarCycle.NewPeriod(agreement, agreement.FirstPeriodStartLocal, now));
        sub.Status = SubscriptionStatus.Active;
        sub.CurrentPeriodStart = BillingCalendar.StartUtc(agreement.FirstPeriodStartLocal);
        sub.CurrentPeriodEnd = BillingCalendar.StartUtc(agreement.FirstPeriodStartLocal.AddMonths(1));
        sub.UpdatedAt = now; sub.UpdatedBy = actor.Id.ToString();
        license.FechaFin = null; license.UpdatedAt = now; license.UpdatedBy = actor.Id.ToString();
        foreach (var module in grantModules)
        {
            var assignment = await db.EmpresaModulos.SingleOrDefaultAsync(x => x.EmpresaId == 23 && x.ModuloId == module.Id);
            if (assignment == null) { assignment = new EmpresaModulo { EmpresaId = 23, ModuloId = module.Id }; db.EmpresaModulos.Add(assignment); }
            assignment.Activo = true; assignment.FechaInactivacion = null; assignment.FechaActivacion = now;
            assignment.ComplementoAutorizado = true; assignment.ComplementoAutorizadoAt = now; assignment.ComplementoAutorizadoBy = actor.Id.ToString();
            assignment.ComplementoMotivo = "CLI23: eventos fiscales y contingencia autorizados por acuerdo de servicio.";
        }
        await db.SaveChangesAsync();
        db.Auditoria.Add(new Auditoria { EmpresaId = 23, UsuarioId = actor.Id, Username = actor.Username, Modulo = "BILLING",
            Accion = key, Entidad = "BillingCalendarAgreement", EntidadId = agreement.Id.ToString(), DatosAntes = serialized,
            DatosDespues = JsonSerializer.Serialize(new { agreement.Id, Status = sub.Status, license.FechaFin, InvoiceStatus = "OPEN", Amount = 15, PaymentsCreated = 0,
                Period = "2026-09", DueLocalDate = "2026-09-30", Grants = new[] { "EVENTOSDTE", "CONTINGENCIA" } }), Detalle = agreement.Reason });
        await db.SaveChangesAsync();
        return Result.Ok();
    }, CancellationToken.None);
    Console.WriteLine(result.IsSuccess ? "CLI23_OK" : result.Error);
    return result.IsSuccess ? 0 : 1;
}
catch (Exception ex) { Console.Error.WriteLine("CLI23_FAILED: " + ex.GetType().Name); return 1; }

sealed class CliActor(int id) : ICurrentUser
{
    public bool IsAuthenticated => id > 0;
    public int? UserId => id;
    public int? EmpresaId => null;
    public string? Username => null;
    public string? Email => null;
    public string? TipoUsuarioCodigo => null;
    public IReadOnlyList<string> Roles => [];
    public IReadOnlyList<string> Permisos => [];
    public bool HasPermiso(string codigo) => false;
    public bool IsInRole(string codigo) => false;
}

static class CalendarRehearsal
{
    public static async Task VerifyAsync(string connection)
    {
        NeoStpDbContext Db(params IInterceptor[] interceptors) => new(new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseSqlServer(connection).AddInterceptors(interceptors).Options);
        await using var baseline = Db();
        var agreement = await baseline.BillingCalendarAgreements.AsNoTracking().SingleAsync(x => x.EmpresaId == 23 && x.Active);
        if (agreement.AgreementKey != "CLI23-CALENDAR-2026-09-V1") throw new InvalidOperationException("CLI23_REHEARSAL_AGREEMENT_REQUIRED");
        var paymentsBefore = await baseline.BillingPayments.CountAsync();
        var licenseBefore = await baseline.EmpresaPlanes.AsNoTracking().SingleAsync(x => x.Id == 29);
        var october = new DateOnly(2026, 10, 1);
        var november = new DateOnly(2026, 11, 1);
        if (await baseline.BillingCalendarPeriods.AnyAsync(x => x.AgreementId == agreement.Id && x.PeriodStartLocal >= november))
            throw new InvalidOperationException("CLI23_REHEARSAL_PERIOD_DRIFT");
        await using var first = Db(); await using var second = Db();
        var at = new DateTime(2026, 10, 5, 15, 0, 0, DateTimeKind.Utc);
        var results = await Task.WhenAll(BillingCalendarCycle.AdvanceAsync(first, 23, at), BillingCalendarCycle.AdvanceAsync(second, 23, at));
        if (results.Any(x => x.IsFailure)) throw new InvalidOperationException("CLI23_REHEARSAL_CONCURRENCY_FAILED");
        await using var check = Db();
        var periods = await check.BillingCalendarPeriods.AsNoTracking().Include(x => x.Invoice)
            .Where(x => x.AgreementId == agreement.Id && x.PeriodStartLocal == october).ToListAsync();
        if (periods.Count != 1 || periods[0].Invoice.Status != "OPEN" || periods[0].Invoice.Amount != 15
            || periods[0].DueLocalDate != new DateOnly(2026, 10, 31) || await check.BillingPayments.CountAsync() != paymentsBefore)
            throw new InvalidOperationException("CLI23_REHEARSAL_CONCURRENCY_ASSERTION");
        var count = await check.BillingInvoices.CountAsync();
        await BillingCalendarCycle.AdvanceAsync(check, 23, at);
        if (await check.BillingInvoices.CountAsync() != count) throw new InvalidOperationException("CLI23_REHEARSAL_REPLAY_FAILED");
        var rolledBack = false;
        await using (var fail = Db(new FailBeforeCommit()))
        {
            try { await BillingCalendarCycle.AdvanceAsync(fail, 23, new DateTime(2026, 11, 5, 15, 0, 0, DateTimeKind.Utc)); }
            catch (RehearsalRollbackException) { rolledBack = true; }
        }
        await using var final = Db();
        var licenseAfter = await final.EmpresaPlanes.AsNoTracking().SingleAsync(x => x.Id == 29);
        if (!rolledBack || await final.BillingCalendarPeriods.AnyAsync(x => x.AgreementId == agreement.Id && x.PeriodStartLocal == november)
            || await final.BillingInvoices.CountAsync() != count || await final.BillingPayments.CountAsync() != paymentsBefore
            || licenseAfter.FechaFin != licenseBefore.FechaFin || licenseAfter.EstadoCodigo != licenseBefore.EstadoCodigo)
            throw new InvalidOperationException("CLI23_REHEARSAL_ROLLBACK_ASSERTION");
        Console.WriteLine("CLI23_CALENDAR_SQL_VERIFIED: concurrent_month_unique,replay_no_duplicate,rollback_atomic,access_preserved,no_payments_created");
    }
    sealed class RehearsalRollbackException : Exception;
    sealed class FailBeforeCommit : DbTransactionInterceptor
    {
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
            => throw new RehearsalRollbackException();
    }
}
