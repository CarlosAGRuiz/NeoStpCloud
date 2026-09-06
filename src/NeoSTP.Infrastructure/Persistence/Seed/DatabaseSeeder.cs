using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Auth;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Seguridad;

namespace NeoSTP.Infrastructure.Persistence.Seed;

/// <summary>
/// Datos sembrados en runtime al startup. A diferencia de HasData (que se materializa
/// en la migración), DatabaseSeeder se ejecuta cada arranque y crea solo lo que falte.
/// Útil para el SuperAdmin inicial (cuyo hash de password debe calcularse dinámicamente).
/// </summary>
public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NeoStpDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<NeoStpDbContext>>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<SuperAdminOptions>>().Value;

        await db.Database.MigrateAsync(ct);

        await EnsureSuperAdminAsync(db, hasher, options, logger, ct);
    }

    /// <summary>
    /// Crea únicamente el primer SuperAdmin. Separado de las migraciones para verificar
    /// el bootstrap sin iniciar hosts ni tocar una base configurada por la aplicación.
    /// </summary>
    public static async Task EnsureSuperAdminAsync(NeoStpDbContext db, IPasswordHasher hasher,
        SuperAdminOptions options, ILogger logger, CancellationToken ct = default)
    {

        var existeAlgunUsuario = await db.Usuarios.AnyAsync(ct);
        if (existeAlgunUsuario)
        {
            logger.LogInformation("DatabaseSeeder: ya existen usuarios, no se siembra SuperAdmin.");
            return;
        }

        options.ValidateForBootstrap();

        var rolSuperAdmin = await db.Roles.FirstOrDefaultAsync(r => r.Codigo == "SUPERADMIN"
            && r.EmpresaId == null && r.EsSistema && r.Activo, ct);
        if (rolSuperAdmin is null)
        {
            logger.LogWarning("DatabaseSeeder: no se encontró un rol SUPERADMIN central, de sistema y activo; saltando seed.");
            return;
        }

        var superAdmin = new Usuario
        {
            Username = options.Username.Trim(),
            Email = options.Email,
            PasswordHash = hasher.Hash(options.Password),
            NombreCompleto = options.NombreCompleto,
            TipoUsuarioCodigo = "SUPERADMIN",
            EstadoCodigo = EstadoCodes.Activo,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "SEEDER",
        };

        // Un solo SaveChanges persiste el usuario y su asignación dentro de la misma
        // transacción del proveedor relacional, sin dejar una cuenta huérfana de rol.
        superAdmin.Roles.Add(new UsuarioRol
        {
            Usuario = superAdmin,
            RolId = rolSuperAdmin.Id,
            CreatedAt = DateTime.UtcNow,
        });

        db.Usuarios.Add(superAdmin);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "DatabaseSeeder: SuperAdmin inicial creado con username '{Username}'. Las credenciales no se registran.",
            superAdmin.Username);
    }
}
