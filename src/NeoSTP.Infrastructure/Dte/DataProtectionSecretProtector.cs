using Microsoft.AspNetCore.DataProtection;
using NeoSTP.Application.Dte.Abstractions;

namespace NeoSTP.Infrastructure.Dte;

/// <summary>
/// Implementación de ISecretProtector con ASP.NET Core DataProtection.
/// Usa el purpose "NeoSTP.DteSecrets" para aislar las llaves de otros usos.
/// Las llaves se guardan automáticamente en %LOCALAPPDATA%\ASP.NET\DataProtection-Keys
/// (Windows) o /var/aspnet/DataProtection-Keys (Linux).
/// </summary>
public class DataProtectionSecretProtector : ISecretProtector
{
    public const string Purpose = "NeoSTP.DteSecrets.v1";

    private readonly IDataProtector _protector;

    public DataProtectionSecretProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public string Protect(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            throw new ArgumentException("plaintext requerido", nameof(plaintext));
        return _protector.Protect(plaintext);
    }

    public string Unprotect(string ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext))
            throw new ArgumentException("ciphertext requerido", nameof(ciphertext));
        return _protector.Unprotect(ciphertext);
    }

    public string? ProtectOrNull(string? plaintext)
        => string.IsNullOrEmpty(plaintext) ? null : _protector.Protect(plaintext);

    public string? UnprotectOrNull(string? ciphertext)
        => string.IsNullOrEmpty(ciphertext) ? null : _protector.Unprotect(ciphertext);
}
