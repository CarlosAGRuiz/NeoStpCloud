namespace NeoSTP.Application.Auth;

/// <summary>
/// Configuración del SuperAdmin inicial creado por DatabaseSeeder cuando
/// la base no tiene ningún usuario. Las credenciales deben configurarse explícitamente;
/// nunca se usan para modificar usuarios existentes.
/// </summary>
public class SuperAdminOptions
{
    public const string SectionName = "SuperAdmin";

    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = "superadmin@neostp.local";
    public string NombreCompleto { get; set; } = "SuperAdmin NeoSTP";
    public string Password { get; set; } = string.Empty;

    /// <summary>Validar solo al crear el primer usuario, no como validación global de arranque.</summary>
    public void ValidateForBootstrap()
    {
        var username = Username?.Trim();
        if (string.IsNullOrWhiteSpace(username) || username.Length is < 3 or > 100
            || string.Equals(username, "superadmin", StringComparison.OrdinalIgnoreCase)
            || username.Any(c => !char.IsLetterOrDigit(c) && c is not '.' and not '_' and not '-' and not '@'))
        {
            throw new InvalidOperationException(
                "Bootstrap SuperAdmin bloqueado: configure SuperAdmin:Username con un nombre no predeterminado de 3 a 100 caracteres, sin espacios.");
        }

        var password = Password;
        if (string.IsNullOrWhiteSpace(password) || password.Length < 16
            || System.Text.Encoding.UTF8.GetByteCount(password) > 72
            || string.Equals(password.Trim(), "ChangeMe!2026", StringComparison.OrdinalIgnoreCase)
            || password.Any(char.IsControl)
            || !password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit)
            || !password.Any(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c)))
        {
            throw new InvalidOperationException(
                "Bootstrap SuperAdmin bloqueado: configure SuperAdmin:Password con una clave nueva de al menos 16 caracteres y hasta 72 bytes UTF-8, con mayúsculas, minúsculas, números y símbolos.");
        }
    }
}
