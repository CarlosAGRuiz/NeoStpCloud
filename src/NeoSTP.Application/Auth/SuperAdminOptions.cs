namespace NeoSTP.Application.Auth;

/// <summary>
/// Configuración del SuperAdmin inicial creado por DatabaseSeeder cuando
/// la base no tiene ningún usuario. Cambiar la contraseña al primer login.
/// </summary>
public class SuperAdminOptions
{
    public const string SectionName = "SuperAdmin";

    public string Username { get; set; } = "superadmin";
    public string Email { get; set; } = "superadmin@neostp.local";
    public string NombreCompleto { get; set; } = "SuperAdmin NeoSTP";
    public string Password { get; set; } = "ChangeMe!2026";
}
