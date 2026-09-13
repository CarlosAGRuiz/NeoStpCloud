using System.Data;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Dte;

public sealed record DteCertificateProtectionReport(
    int ActiveTotal,
    int HistoryTotal,
    int Protected,
    int Legacy,
    int Converted);

/// <summary>
/// Convierte certificados históricos al envelope Data Protection usando el mismo key ring
/// que los hosts. Es una operación explícita de deployment, nunca una migración SQL ni un
/// trabajo automático de arranque.
/// </summary>
public sealed class DteCertificateProtectionMigrator
{
    private const string LockSql = """
        DECLARE @result int;
        EXEC @result = sys.sp_getapplock
            @Resource = N'NeoSTP:DteCertificateProtection:v1',
            @LockMode = N'Exclusive',
            @LockOwner = N'Transaction',
            @LockTimeout = 30000;
        IF @result < 0 THROW 51000, 'DTE_CERTIFICATE_PROTECTION_LOCK_FAILED', 1;
        """;

    private readonly NeoStpDbContext _db;
    private readonly ISecretProtector _protector;
    private readonly ILogger<DteCertificateProtectionMigrator> _logger;

    public DteCertificateProtectionMigrator(
        NeoStpDbContext db,
        ISecretProtector protector,
        ILogger<DteCertificateProtectionMigrator> logger)
    {
        _db = db;
        _protector = protector;
        _logger = logger;
    }

    public async Task<DteCertificateProtectionReport> InspectAsync(
        bool requireAllProtected, CancellationToken ct = default)
    {
        var active = await _db.DteConfiguracion.AsNoTracking()
            .Where(x => x.CertificadoBlob != null)
            .Select(x => new { x.EmpresaId, x.CertificadoBlob })
            .ToListAsync(ct);
        var history = await _db.DteConfiguracionVersiones.AsNoTracking()
            .Where(x => x.CertificadoBlob != null)
            .Select(x => new { x.EmpresaId, x.CertificadoBlob })
            .ToListAsync(ct);

        var protectedCount = 0;
        var legacy = 0;
        foreach (var row in active.Concat(history))
        {
            var bytes = row.CertificadoBlob!;
            if (!_protector.IsProtectedBytes(bytes))
            {
                legacy++;
                continue;
            }

            var plaintext = _protector.UnprotectBytes(bytes, Context(row.EmpresaId));
            try
            {
                if (plaintext.Length == 0)
                    throw new CryptographicException("DTE_CERTIFICATE_EMPTY_AFTER_UNPROTECT");
                protectedCount++;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }

        if (requireAllProtected && legacy != 0)
            throw new InvalidOperationException($"DTE_CERTIFICATE_LEGACY_REMAINING:{legacy}");

        return new(active.Count, history.Count, protectedCount, legacy, 0);
    }

    public async Task<DteCertificateProtectionReport> MigrateAsync(CancellationToken ct = default)
    {
        if (!_db.Database.IsRelational())
            throw new InvalidOperationException("DTE_CERTIFICATE_MIGRATION_REQUIRES_RELATIONAL_DATABASE");

        await using var transaction = await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, ct);
        await _db.Database.ExecuteSqlRawAsync(LockSql, ct);

        var active = await _db.DteConfiguracion
            .Where(x => x.CertificadoBlob != null)
            .OrderBy(x => x.EmpresaId)
            .ToListAsync(ct);
        var history = await _db.DteConfiguracionVersiones
            .Where(x => x.CertificadoBlob != null)
            .OrderBy(x => x.EmpresaId).ThenBy(x => x.Id)
            .ToListAsync(ct);

        var converted = 0;
        foreach (var row in active)
            converted += ProtectIfLegacy(row.EmpresaId, row.CertificadoBlob!, x => row.CertificadoBlob = x);
        foreach (var row in history)
            converted += ProtectIfLegacy(row.EmpresaId, row.CertificadoBlob!, x => row.CertificadoBlob = x);

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        _db.ChangeTracker.Clear();

        var report = await InspectAsync(requireAllProtected: true, ct);
        _logger.LogWarning(
            "Custodia fiscal migrada: activos={Active}, historial={History}, convertidos={Converted}, legacy={Legacy}",
            report.ActiveTotal, report.HistoryTotal, converted, report.Legacy);
        return report with { Converted = converted };
    }

    private int ProtectIfLegacy(int empresaId, byte[] stored, Action<byte[]> assign)
    {
        if (_protector.IsProtectedBytes(stored))
        {
            var existing = _protector.UnprotectBytes(stored, Context(empresaId));
            CryptographicOperations.ZeroMemory(existing);
            return 0;
        }

        var encrypted = _protector.ProtectBytes(stored, Context(empresaId));
        var verification = _protector.UnprotectBytes(encrypted, Context(empresaId));
        try
        {
            if (!CryptographicOperations.FixedTimeEquals(stored, verification))
                throw new CryptographicException("DTE_CERTIFICATE_PROTECTION_VERIFY_FAILED");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(verification);
        }

        assign(encrypted);
        CryptographicOperations.ZeroMemory(stored);
        return 1;
    }

    internal static string Context(int empresaId) => $"Empresa:{empresaId}";
}
