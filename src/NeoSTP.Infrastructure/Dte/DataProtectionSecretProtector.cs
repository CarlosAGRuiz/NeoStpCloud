using Microsoft.AspNetCore.DataProtection;
using NeoSTP.Application.Dte.Abstractions;

namespace NeoSTP.Infrastructure.Dte;

/// <summary>
/// Implementación de ISecretProtector con ASP.NET Core DataProtection.
/// Los secretos de texto conservan el purpose histórico. Los certificados usan
/// un purpose hijo y un discriminador por empresa, con envelope versionado.
/// </summary>
public class DataProtectionSecretProtector : ISecretProtector
{
    public const string Purpose = "NeoSTP.DteSecrets.v1";
    public const string BinaryPurpose = "DteCertificate.v1";

    private static readonly byte[] BinaryEnvelope = "NSTPCERT1"u8.ToArray();

    private readonly IDataProtectionProvider _provider;
    private readonly IDataProtector _protector;

    public DataProtectionSecretProtector(IDataProtectionProvider provider)
    {
        _provider = provider;
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

    public byte[] ProtectBytes(byte[] plaintext, string discriminator)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        if (plaintext.Length == 0)
            throw new ArgumentException("plaintext requerido", nameof(plaintext));
        if (string.IsNullOrWhiteSpace(discriminator))
            throw new ArgumentException("discriminator requerido", nameof(discriminator));

        var protectedBytes = BinaryProtector(discriminator).Protect(plaintext);
        var envelope = new byte[BinaryEnvelope.Length + protectedBytes.Length];
        BinaryEnvelope.CopyTo(envelope, 0);
        protectedBytes.CopyTo(envelope, BinaryEnvelope.Length);
        return envelope;
    }

    public byte[] UnprotectBytes(byte[] ciphertext, string discriminator)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);
        if (!IsProtectedBytes(ciphertext))
            throw new System.Security.Cryptography.CryptographicException(
                "El secreto binario no usa un envelope NeoSTP reconocido.");
        if (string.IsNullOrWhiteSpace(discriminator))
            throw new ArgumentException("discriminator requerido", nameof(discriminator));

        return BinaryProtector(discriminator)
            .Unprotect(ciphertext.AsSpan(BinaryEnvelope.Length).ToArray());
    }

    public bool IsProtectedBytes(byte[] value)
        => value is not null
           && value.Length > BinaryEnvelope.Length
           && value.AsSpan(0, BinaryEnvelope.Length).SequenceEqual(BinaryEnvelope);

    private IDataProtector BinaryProtector(string discriminator)
        => _provider.CreateProtector(Purpose, BinaryPurpose, discriminator.Trim());
}
