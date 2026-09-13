namespace NeoSTP.Application.Dte.Abstractions;

/// <summary>
/// Cifra/descifra valores sensibles (passwords MH, tokens, certificados, etc.) usando una llave
/// rotable. Implementado con IDataProtector en Infrastructure.
/// </summary>
public interface ISecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string ciphertext);
    string? ProtectOrNull(string? plaintext);
    string? UnprotectOrNull(string? ciphertext);

    /// <summary>Protege material binario sensible ligado a un propósito contextual/tenant.</summary>
    byte[] ProtectBytes(byte[] plaintext, string discriminator)\n        => throw new NotSupportedException("Protección binaria no configurada.");

    /// <summary>Descifra un envelope binario para el mismo propósito contextual/tenant.</summary>
    byte[] UnprotectBytes(byte[] ciphertext, string discriminator)\n        => throw new NotSupportedException("Protección binaria no configurada.");

    /// <summary>Indica si el valor tiene un envelope binario NeoSTP reconocido.</summary>
    bool IsProtectedBytes(byte[] value) => false;
}
