using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Auth;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Auth.Dtos;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Seguridad;
using NeoSTP.Infrastructure.Auth;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;

// Deliberately no connection-string argument, user-secrets, app config, migrations or host startup.
const string server = @"(localdb)\NeoStpAuthAudit_20260904";
var suffix = Guid.NewGuid().ToString("N");
var database = "NeoStpAuthAudit_" + suffix;
var connection = new SqlConnectionStringBuilder { DataSource = server, InitialCatalog = database,
    IntegratedSecurity = true, TrustServerCertificate = true, ConnectTimeout = 10 }.ConnectionString;
NeoStpDbContext Db(SaveChangesInterceptor? interceptor = null)
{
    var options = new DbContextOptionsBuilder<NeoStpDbContext>().UseSqlServer(connection);
    if (interceptor is not null) options.AddInterceptors(interceptor);
    return new(options.Options);
}
var audit = Substitute.For<IAuditoriaService>();
var protector = Substitute.For<ISecretProtector>();
protector.Protect(Arg.Any<string>()).Returns(x => (string)x[0]);
protector.Unprotect(Arg.Any<string>()).Returns(x => (string)x[0]);
var totp = new TotpService();
MfaService Mfa(NeoStpDbContext db) => new(db, totp, protector, audit);
AuthService Auth(NeoStpDbContext db)
{
    var hasher = Substitute.For<IPasswordHasher>();
    hasher.Verify("valid", "test-only-hash").Returns(true);
    hasher.Hash(Arg.Any<string>()).Returns("test-only-hash");
    var jwt = Substitute.For<IJwtTokenService>();
    jwt.CreateAccessToken(Arg.Any<UserInfo>()).Returns(("test.jwt", DateTime.UtcNow.AddMinutes(10)));
    jwt.CreateRefreshToken().Returns(_ => Guid.NewGuid().ToString("N"));
    return new(db, hasher, jwt, audit, Mfa(db), Options.Create(new JwtOptions { RefreshTokenExpiryDays = 14 }),
        Options.Create(new SecurityOptions()), NullLogger<AuthService>.Instance);
}
void Check(bool value, string test) { if (!value) throw new InvalidOperationException("FAIL: " + test); Console.WriteLine("PASS: " + test); }
async Task<Usuario> User()
{
    await using var db = Db();
    var company = new Empresa { Nit = Guid.NewGuid().ToString("N")[..14], RazonSocial = "SYNTHETIC AUTH AUDIT" };
    var user = new Usuario { Empresa = company, Username = "audit-" + Guid.NewGuid().ToString("N"), Email = "fixture@example.test",
        NombreCompleto = "SYNTHETIC", PasswordHash = "test-only-hash", TipoUsuarioCodigo = "OPERADOR" };
    db.Usuarios.Add(user);
    await db.SaveChangesAsync();
    return user;
}
async Task<string> Begin(int id) { await using var db = Db(); return (await Mfa(db).IniciarEnrolamientoAsync(id)).Value!.Secret; }
async Task<string> Enable(int id)
{
    var secret = await Begin(id);
    await using var db = Db();
    var result = await Mfa(db).ConfirmarEnrolamientoAsync(id, totp.GenerarCodigo(secret, DateTimeOffset.UtcNow));
    return result.Value!.RecoveryCodes[0];
}
async Task<T[]> Race<T>(params Func<NeoStpDbContext, Task<T>>[] actions)
{
    var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var count = 0;
    return await Task.WhenAll(actions.Select(async action =>
    {
        await using var db = Db(new FirstSaveBarrier(async () =>
        {
            if (Interlocked.Increment(ref count) == actions.Length) barrier.TrySetResult();
            await barrier.Task.WaitAsync(TimeSpan.FromSeconds(20));
        }));
        return await action(db);
    }));
}

await using var schema = Db();
try
{
    if (args.Contains("--migration-chain"))
    {
        await schema.Database.MigrateAsync();
        Check(!(await schema.Database.GetPendingMigrationsAsync()).Any(), "Full migration chain applies to empty isolated SQL database");
    }
    else await schema.Database.EnsureCreatedAsync();
    Console.WriteLine("Isolated SQL database: " + database);
    var user = await User();
    var secret = await Begin(user.Id);
    var code = totp.GenerarCodigo(secret, DateTimeOffset.UtcNow);
    var confirm = await Race(
        async db => (await Mfa(db).ConfirmarEnrolamientoAsync(user.Id, code)).IsSuccess,
        async db => (await Mfa(db).ConfirmarEnrolamientoAsync(user.Id, code)).IsSuccess);
    Check(confirm.Count(x => x) == 1, "Only one concurrent confirmation delivers recovery codes");

    user = await User();
    var recovery = await Enable(user.Id);
    var consume = await Race(
        async db => (await Mfa(db).VerificarCodigoLoginAsync(user.Id, recovery)).IsSuccess,
        async db => (await Mfa(db).VerificarCodigoLoginAsync(user.Id, recovery)).IsSuccess);
    Check(consume.Count(x => x) == 1, "Recovery code consumed once across SQL connections");

    user = await User(); secret = await Begin(user.Id); code = totp.GenerarCodigo(secret, DateTimeOffset.UtcNow);
    var enrollment = await Race(
        async db => (await Mfa(db).IniciarEnrolamientoAsync(user.Id)).IsSuccess,
        async db => (await Mfa(db).ConfirmarEnrolamientoAsync(user.Id, code)).IsSuccess);
    Check(enrollment.Count(x => x) == 1, "Begin/confirm race cannot overwrite MFA configuration");

    user = await User();
    LoginResponse login;
    await using (var db = Db()) login = (await Auth(db).LoginAsync(new LoginRequest { UsernameOrEmail = user.Username, Password = "valid" }, new())).Value!;
    var rotations = await Race(
        async db => await Auth(db).RefreshAsync(login.RefreshToken, new()),
        async db => await Auth(db).RefreshAsync(login.RefreshToken, new()));
    Check(rotations.Count(x => x.IsSuccess) == 1, "Concurrent refresh has exactly one winner");
    await using (var db = Db()) Check(await db.RefreshTokens.CountAsync(t => t.UsuarioId == user.Id && t.RevokedAt == null) == 1,
        "Losing refresh transaction leaves no orphan token");
    await using (var db = Db()) await Auth(db).LogoutAsync(login.RefreshToken, new());
    await using (var db = Db()) Check((await Auth(db).RefreshAsync(rotations.Single(x => x.IsSuccess).Value!.RefreshToken, new())).IsFailure,
        "Logout with rotated token revokes parent session");

    user = await User();
    var failures = await Race(Enumerable.Range(0, 5).Select<int, Func<NeoStpDbContext, Task<bool>>>(_ => async db =>
        (await Auth(db).LoginAsync(new LoginRequest { UsernameOrEmail = user.Username, Password = "wrong" }, new())).IsFailure).ToArray());
    await using (var db = Db())
    {
        var persisted = await db.Usuarios.SingleAsync(u => u.Id == user.Id);
        Check(failures.All(x => x) && persisted.IntentosFallidos == 5 && persisted.EstadoCodigo == "BLOQUEADO",
            "Concurrent failed logins preserve all attempts and lock account");
    }
    user = await User();
    await using (var db = Db()) login = (await Auth(db).LoginAsync(new LoginRequest { UsernameOrEmail = user.Username, Password = "valid" }, new())).Value!;
    var logoutRace = await Race(
        async db => (await Auth(db).RefreshAsync(login.RefreshToken, new())).Value?.RefreshToken,
        async db => { await Auth(db).LogoutAsync(null, new() { SessionId = login.User.SessionId }); return (string?)null; });
    await using (var db = Db()) Check((await db.AuthSessions.SingleAsync(s => s.Id == login.User.SessionId)).RevokedAt != null,
        "Logout wins over simultaneous refresh at parent-session level");
    if (logoutRace[0] is { } rotated)
    {
        await using var db = Db();
        Check((await Auth(db).RefreshAsync(rotated, new())).IsFailure, "Concurrent refresh cannot resurrect logged-out session");
    }

    user = await User();
    secret = await Begin(user.Id);
    code = totp.GenerarCodigo(secret, DateTimeOffset.UtcNow);
    const string tenant = "11111111-1111-1111-1111-111111111111";
    var info = new ExternalLoginInfo { Proveedor = "ENTRA", Subject = Guid.NewGuid().ToString("D"),
        Issuer = $"https://login.microsoftonline.com/{tenant}/v2.0", TenantIdExterno = tenant, Email = user.Email };
    await using (var db = Db())
    {
        await Mfa(db).ConfirmarEnrolamientoAsync(user.Id, code);
        var linked = await db.Usuarios.SingleAsync(u => u.Id == user.Id);
        linked.SsoProveedor = info.Proveedor; linked.SsoSubject = info.Subject; linked.SsoIssuer = info.Issuer;
        db.EmpresaSso.Add(new EmpresaSso { EmpresaId = user.EmpresaId!.Value, ProveedorCodigo = "ENTRA",
            Habilitado = true, DominioCorreo = "example.test", TenantIdExterno = tenant });
        await db.SaveChangesAsync();
        login = (await Auth(db).LoginExternoAsync(info, new())).Value!;
    }
    var challenge = await Race(
        async db => (await Auth(db).VerifyMfaChallengeAsync(code, new() { SessionId = login.User.SessionId })).IsSuccess,
        async db => (await Auth(db).VerifyMfaChallengeAsync(code, new() { SessionId = login.User.SessionId })).IsSuccess);
    Check(challenge.Count(x => x) == 1, "Concurrent MFA challenge consumption issues only one full session");
    await using (var db = Db()) Check(await db.AuthSessions.CountAsync(s => s.UsuarioId == user.Id && s.Purpose == "FULL") == 1,
        "Losing MFA transaction leaves no full session");

    Console.WriteLine("SQL verification completed; only synthetic data was used.");
}
finally
{
    // The name is generated in this invocation and never sourced from configuration or user input.
    if (database == "NeoStpAuthAudit_" + suffix && Guid.TryParseExact(suffix, "N", out _)
        && schema.Database.GetDbConnection().DataSource == server)
    {
        await schema.Database.EnsureDeletedAsync();
        Console.WriteLine("Removed task-owned synthetic database: " + database);
    }
}

sealed class FirstSaveBarrier(Func<Task> wait) : SaveChangesInterceptor
{
    private bool used;
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (!used) { used = true; await wait(); }
        return result;
    }
}
