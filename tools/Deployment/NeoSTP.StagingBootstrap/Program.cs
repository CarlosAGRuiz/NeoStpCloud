using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NeoSTP.Application.Auth;
using NeoSTP.Infrastructure.Auth;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Persistence.Seed;

const string ExpectedDatabase = "NeoSTP_Staging";

var connectionString = Required("ConnectionStrings__NeoStpDb");
var username = Required("NEOSTP_STAGING_ADMIN_USERNAME");
var email = Required("NEOSTP_STAGING_ADMIN_EMAIL");
var password = Required("NEOSTP_STAGING_ADMIN_PASSWORD");

var connection = new SqlConnectionStringBuilder(connectionString);
if (!string.Equals(connection.InitialCatalog, ExpectedDatabase, StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("STAGING_BOOTSTRAP_DATABASE_INVALID");

var options = new DbContextOptionsBuilder<NeoStpDbContext>()
    .UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(NeoStpDbContext).Assembly.FullName))
    .Options;

await using var db = new NeoStpDbContext(options);
var known = db.Database.GetMigrations().ToHashSet(StringComparer.Ordinal);
var applied = (await db.Database.GetAppliedMigrationsAsync()).ToHashSet(StringComparer.Ordinal);
if (known.Count == 0 || !known.SetEquals(applied))
    throw new InvalidOperationException("STAGING_BOOTSTRAP_SCHEMA_NOT_READY");

var hadUsers = await db.Usuarios.AnyAsync();
await DatabaseSeeder.EnsureSuperAdminAsync(
    db,
    new BcryptPasswordHasher(),
    new SuperAdminOptions
    {
        Username = username,
        Email = email,
        NombreCompleto = "Administrador STAGING",
        Password = password,
    },
    NullLogger.Instance);

var exists = await db.Usuarios.AnyAsync(user => user.Username == username);
if (!exists)
    throw new InvalidOperationException(hadUsers
        ? "STAGING_BOOTSTRAP_SKIPPED_DATABASE_ALREADY_HAS_USERS"
        : "STAGING_BOOTSTRAP_USER_NOT_CREATED");

Console.WriteLine(hadUsers ? "STAGING_ADMIN_ALREADY_PRESENT" : "STAGING_ADMIN_CREATED");

static string Required(string name)
{
    var value = Environment.GetEnvironmentVariable(name);
    return string.IsNullOrWhiteSpace(value)
        ? throw new InvalidOperationException($"STAGING_BOOTSTRAP_INPUT_MISSING: {name}")
        : value;
}
