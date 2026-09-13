using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Infrastructure.Dte;

namespace NeoSTP.Infrastructure.Services;

public partial class DteDocumentosService
{
    private async Task<DteSignResult> FirmarConCertificadoProtegidoAsync(
        int empresaId,
        string json,
        DteConfiguracion config,
        string? certificatePassword,
        CancellationToken ct)
    {
        var stored = config.CertificadoBlob;
        if (stored is null || stored.Length == 0)
            return CertificateFailure("CERT_VACIO", "No hay certificado fiscal configurado.");

        byte[] plaintext;
        try
        {
            if (_protector.IsProtectedBytes(stored))
            {
                plaintext = _protector.UnprotectBytes(
                    stored, DteCertificateProtectionMigrator.Context(empresaId));
            }
            else if (_allowLegacyCertificatePlaintext)
            {
                plaintext = stored.ToArray();
                _logger?.LogWarning(
                    "Firma fiscal usando compatibilidad temporal explícita para certificado legacy. EmpresaId={EmpresaId}",
                    empresaId);
            }
            else
            {
                _logger?.LogCritical(
                    "Firma fiscal bloqueada: certificado legacy sin cifrar. EmpresaId={EmpresaId}",
                    empresaId);
                return CertificateFailure(
                    "CERTIFICADO_LEGACY_BLOQUEADO",
                    "El certificado fiscal requiere la conversión de seguridad antes de firmar.");
            }
        }
        catch (CryptographicException)
        {
            _logger?.LogCritical(
                "Firma fiscal bloqueada: no fue posible descifrar el certificado. EmpresaId={EmpresaId}",
                empresaId);
            return CertificateFailure(
                "CERTIFICADO_DESCIFRADO_FALLO",
                "No se pudo abrir el certificado fiscal protegido. Verifique el key ring de Data Protection.");
        }

        var resolvedPassword = certificatePassword;
        if (resolvedPassword is null && !string.IsNullOrEmpty(config.PasswordCertificadoCifrado))
        {
            try
            {
                resolvedPassword = _protector.Unprotect(config.PasswordCertificadoCifrado);
            }
            catch (CryptographicException)
            {
                CryptographicOperations.ZeroMemory(plaintext);
                return CertificateFailure(
                    "CERTIFICADO_PASSWORD_DESCIFRADO_FALLO",
                    "No se pudo descifrar la contraseña del certificado fiscal.");
            }
        }

        try
        {
            return await _signer.FirmarAsync(json, plaintext, resolvedPassword, ct);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private static DteSignResult CertificateFailure(string code, string detail) => new()
    {
        Success = false,
        Mensaje = code,
        Detalle = detail,
    };
}
