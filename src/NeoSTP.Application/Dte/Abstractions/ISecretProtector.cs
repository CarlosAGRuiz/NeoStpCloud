namespace NeoSTP.Application.Dte.Abstractions;

/// <summary>
/// Cifra/descifra valores sensibles (passwords MH, tokens, etc.) usando una llave
/// rotable. Implementado con IDataProtector en Infrastructure.
/// </summary>
public interface ISecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string ciphertext);
    string? ProtectOrNull(string? plaintext);
    string? UnprotectOrNull(string? ciphertext);
}
