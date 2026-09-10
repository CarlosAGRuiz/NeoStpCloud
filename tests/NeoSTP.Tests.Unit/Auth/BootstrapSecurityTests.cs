using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using NeoSTP.Application.Auth;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Domain.Core.Seguridad;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Persistence.Seed;
using NSubstitute;

namespace NeoSTP.Tests.Unit.Auth;

public sealed class BootstrapSecurityTests
{
    private const string SyntheticPassword = "Synthetic-Bootstrap#2026-QA";

    [Fact]
    public void DefaultOptionsDoNotProvideUsableCredentials()
    {
        var options = new SuperAdminOptions();
        options.Username.Should().BeEmpty();
        options.Password.Should().BeEmpty();
        options.Invoking(x => x.ValidateForBootstrap()).Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("superadmin")]
    [InlineData(" SUPERADMIN ")]
    [InlineData("ab")]
    [InlineData("qa\nadmin")]
    [InlineData("qa admin")]
    public async Task UnsafeBootstrapNameDoesNotCreateOrHash(string username)
    {
        using var db = Db();
        var hasher = Substitute.For<IPasswordHasher>();
        var logger = new CaptureLogger();
        var options = ValidOptions();
        options.Username = username;

        var run = () => DatabaseSeeder.EnsureSuperAdminAsync(db, hasher, options, logger);
        await run.Should().ThrowAsync<InvalidOperationException>();
        (await db.Usuarios.CountAsync()).Should().Be(0);
        hasher.DidNotReceiveWithAnyArgs().Hash(default!);
        logger.Values.Should().NotContain(x => x.Contains(SyntheticPassword));
    }

    public static IEnumerable<object[]> UnsafePasswords()
    {
        yield return [string.Empty];
        yield return ["ChangeMe!2026"];
        yield return ["    ChangeMe!2026    "];
        yield return ["Short1!"];
        yield return ["abcdefghijklmnopqrst"];
        yield return ["ABCDEFGHIJKLMNOPQRST"];
        yield return ["1234567890123456!"];
        yield return ["Synthetic#PasswordWithoutDigit"];
        yield return ["SyntheticPassword2026WithoutSymbol"];
        yield return ["Valid-Shape1!" + new string('ñ', 40)];
        yield return ["Synthetic-Long#2026\nValue"];
    }

    [Theory]
    [MemberData(nameof(UnsafePasswords))]
    public async Task UnsafeBootstrapPasswordDoesNotCreateOrHash(string password)
    {
        using var db = Db();
        var hasher = Substitute.For<IPasswordHasher>();
        var options = ValidOptions();
        options.Password = password;
        var run = () => DatabaseSeeder.EnsureSuperAdminAsync(db, hasher, options, new CaptureLogger());

        var exception = await run.Should().ThrowAsync<InvalidOperationException>();
        if (password.Length > 0) exception.Which.Message.Should().NotContain(password);
        (await db.Usuarios.CountAsync()).Should().Be(0);
        (await db.UsuarioRoles.CountAsync()).Should().Be(0);
        hasher.DidNotReceiveWithAnyArgs().Hash(default!);
    }

    [Fact]
    public async Task ExistingUsersAreNotResetOrBlockedByAbsentBootstrapConfiguration()
    {
        using var db = Db();
        db.Usuarios.Add(new Usuario {
            Id = 1, Username = "existing-admin", Email = "existing@example.test",
            NombreCompleto = "Existing synthetic admin", PasswordHash = "existing-hash", TipoUsuarioCodigo = "SUPERADMIN"
        });
        await db.SaveChangesAsync();
        var hasher = Substitute.For<IPasswordHasher>();

        await DatabaseSeeder.EnsureSuperAdminAsync(db, hasher, new SuperAdminOptions(), new CaptureLogger());

        var existing = await db.Usuarios.AsNoTracking().SingleAsync();
        existing.Username.Should().Be("existing-admin");
        existing.PasswordHash.Should().Be("existing-hash");
        hasher.DidNotReceiveWithAnyArgs().Hash(default!);
    }

    [Fact]
    public async Task ExplicitStrongBootstrapCreatesRoleWithoutLoggingPasswordOrHash()
    {
        var saveObserver = new BootstrapSaveObserver();
        using var db = Db(saveObserver);
        db.Roles.Add(new Rol { Id = 1, EmpresaId = 10, Codigo = "SUPERADMIN", Nombre = "Tenant impostor", EsSistema = true });
        db.Roles.Add(new Rol { Id = 7, Codigo = "SUPERADMIN", Nombre = "Synthetic root", EsSistema = true });
        await db.SaveChangesAsync();
        saveObserver.Reset();
        var hasher = Substitute.For<IPasswordHasher>();
        hasher.Hash(SyntheticPassword).Returns("synthetic-stored-hash");
        var logger = new CaptureLogger();
        var options = ValidOptions();
        options.Username = "  qa-bootstrap  ";

        await DatabaseSeeder.EnsureSuperAdminAsync(db, hasher, options, logger);

        var created = await db.Usuarios.AsNoTracking().SingleAsync();
        created.Username.Should().Be("qa-bootstrap");
        created.PasswordHash.Should().Be("synthetic-stored-hash");
        created.TipoUsuarioCodigo.Should().Be("SUPERADMIN");
        created.EmpresaId.Should().BeNull();
        (await db.UsuarioRoles.SingleAsync()).RolId.Should().Be(7);
        saveObserver.Calls.Should().Be(1, "user and role assignment must share a single save boundary");
        saveObserver.UserAndRoleWereAddedTogether.Should().BeTrue();
        hasher.Received(1).Hash(SyntheticPassword);
        logger.Values.Should().NotContain(x => x.Contains(SyntheticPassword) || x.Contains("synthetic-stored-hash"));
    }

    [Theory]
    [InlineData(10, true, true)]
    [InlineData(null, false, true)]
    [InlineData(null, true, false)]
    public async Task TenantNonSystemOrInactiveSuperAdminRoleCannotBootstrap(int? empresaId, bool system, bool active)
    {
        using var db = Db();
        db.Roles.Add(new Rol {
            Id = 7, EmpresaId = empresaId, Codigo = "SUPERADMIN", Nombre = "Ineligible synthetic role",
            EsSistema = system, Activo = active
        });
        await db.SaveChangesAsync();
        var hasher = Substitute.For<IPasswordHasher>();

        await DatabaseSeeder.EnsureSuperAdminAsync(db, hasher, ValidOptions(), new CaptureLogger());

        (await db.Usuarios.CountAsync()).Should().Be(0);
        (await db.UsuarioRoles.CountAsync()).Should().Be(0);
        hasher.DidNotReceiveWithAnyArgs().Hash(default!);
    }

    private static SuperAdminOptions ValidOptions() => new() {
        Username = "qa-bootstrap", Password = SyntheticPassword,
        Email = "qa-bootstrap@example.test", NombreCompleto = "Synthetic bootstrap"
    };

    private static NeoStpDbContext Db(SaveChangesInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase("bootstrap-security-" + Guid.NewGuid());
        if (interceptor is not null) options.AddInterceptors(interceptor);
        return new NeoStpDbContext(options.Options);
    }

    private sealed class BootstrapSaveObserver : SaveChangesInterceptor
    {
        public int Calls { get; private set; }
        public bool UserAndRoleWereAddedTogether { get; private set; }
        public void Reset() { Calls = 0; UserAndRoleWereAddedTogether = false; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Calls++;
            var entries = eventData.Context!.ChangeTracker.Entries().ToList();
            UserAndRoleWereAddedTogether = entries.Any(e => e.Entity is Usuario && e.State == EntityState.Added)
                && entries.Any(e => e.Entity is UsuarioRol && e.State == EntityState.Added);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class CaptureLogger : ILogger
    {
        public List<string> Values { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Values.Add(formatter(state, exception));
            if (state is IEnumerable<KeyValuePair<string, object?>> fields)
                Values.AddRange(fields.Select(x => x.Value?.ToString() ?? string.Empty));
        }
    }
}
