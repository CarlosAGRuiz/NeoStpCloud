using Microsoft.Extensions.Options;
using NeoSTP.Application.Auth;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;

namespace NeoSTP.Infrastructure.Auth;

/// <summary>Política de complejidad de contraseña configurable (M6.2).</summary>
public sealed class PasswordPolicy : IPasswordPolicy
{
    private readonly PasswordPolicyOptions _opts;

    public PasswordPolicy(IOptions<SecurityOptions> options) => _opts = options.Value.Password;

    public Result Validate(string? password)
    {
        var errors = new List<string>();
        var pwd = password ?? string.Empty;

        if (pwd.Length < _opts.MinLength)
            errors.Add($"Debe tener al menos {_opts.MinLength} caracteres.");
        if (_opts.RequireUppercase && !pwd.Any(char.IsUpper))
            errors.Add("Debe incluir al menos una mayúscula.");
        if (_opts.RequireLowercase && !pwd.Any(char.IsLower))
            errors.Add("Debe incluir al menos una minúscula.");
        if (_opts.RequireDigit && !pwd.Any(char.IsDigit))
            errors.Add("Debe incluir al menos un número.");
        if (_opts.RequireNonAlphanumeric && pwd.All(char.IsLetterOrDigit))
            errors.Add("Debe incluir al menos un símbolo.");

        return errors.Count == 0
            ? Result.Ok()
            : Result.Fail("La contraseña no cumple la política de seguridad.", "PWD_WEAK", errors);
    }

    public string Describe()
    {
        var parts = new List<string> { $"mínimo {_opts.MinLength} caracteres" };
        if (_opts.RequireUppercase) parts.Add("una mayúscula");
        if (_opts.RequireLowercase) parts.Add("una minúscula");
        if (_opts.RequireDigit) parts.Add("un número");
        if (_opts.RequireNonAlphanumeric) parts.Add("un símbolo");
        return "La contraseña debe tener " + string.Join(", ", parts) + ".";
    }
}
