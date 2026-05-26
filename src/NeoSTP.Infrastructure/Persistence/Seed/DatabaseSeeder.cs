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

        var existeAlgunUsuario = await db.Usuarios.AnyAsync(ct);
        if (existeAlgunUsuario)
        {
            logger.LogInformation("DatabaseSeeder: ya existen usuarios, no se siembra SuperAdmin.");
            return;
        }

        var rolSuperAdmin = await db.Roles.FirstOrDefaultAsync(r => r.Codigo == "SUPERADMIN", ct);
        if (rolSuperAdmin is null)
        {
            logger.LogWarning("DatabaseSeeder: no se encontró rol SUPERADMIN; saltando seed.");
            return;
        }

        var superAdmin = new Usuario
        {
            Username = options.Username,
            Email = options.Email,
            PasswordHash = hasher.Hash(options.Password),
            NombreCompleto = options.NombreCompleto,
            TipoUsuarioCodigo = "SUPERADMIN",
            EstadoCodigo = EstadoCodes.Activo,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "SEEDER",
        };

        db.Usuarios.Add(superAdmin);
        await db.SaveChangesAsync(ct);

        db.UsuarioRoles.Add(new UsuarioRol
        {
            UsuarioId = superAdmin.Id,
            RolId = rolSuperAdmin.Id,
            CreatedAt = DateTime.UtcNow,
        });

        await db.SaveChangesAsync(ct);

        logger.LogWarning(
            "DatabaseSeeder: SuperAdmin creado con username '{Username}'. CONTRASEÑA POR DEFECTO: '{Password}'. CÁMBIALA AL PRIMER LOGIN.",
            options.Username, options.Password);
    }
}
