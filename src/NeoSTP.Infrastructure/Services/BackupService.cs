using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Ops;
using NeoSTP.Domain.Core.Ops;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

/// <summary>
/// Genera un respaldo lógico (manifiesto con conteos de tablas clave + metadatos),
/// lo sube al storage configurado con checksum SHA-256 y registra el trabajo en
/// <c>Ops_BackupJobs</c>. El respaldo físico completo de SQL Server es una tarea de
/// operación documentada en el runbook de Disaster Recovery.
/// </summary>
public class BackupService : IBackupService
{
    private const string AuditModule = "HARDENING";

    private readonly NeoStpDbContext _db;
    private readonly IStorageService _storage;
    private readonly IAuditoriaService _auditoria;
    private readonly ILogger<BackupService> _logger;

    public BackupService(NeoStpDbContext db, IStorageService storage, IAuditoriaService auditoria, ILogger<BackupService> logger)
    {
        _db = db;
        _storage = storage;
        _auditoria = auditoria;
        _logger = logger;
    }

    public async Task<Result<BackupJobDto>> EjecutarBackupAsync(int? empresaId, string origen, string? actor, CancellationToken ct = default)
    {
        var job = new BackupJob
        {
            EmpresaId = empresaId,
            TipoBackup = BackupTipos.Logico,
            EstadoCodigo = BackupEstados.EnProgreso,
            Origen = origen,
            StorageProvider = _storage.Provider,
            IniciadoAt = DateTime.UtcNow,
            CreatedBy = actor,
        };
        _db.BackupJobs.Add(job);
        await _db.SaveChangesAsync(ct);

        try
        {
            var contenido = await ConstruirManifiestoAsync(empresaId, ct);
            var bytes = Encoding.UTF8.GetBytes(contenido);
            var checksum = Convert.ToHexString(SHA256.HashData(bytes));
            var objectName = $"neostp-backup-{job.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}.json";

            using var ms = new MemoryStream(bytes);
            var stored = await _storage.GuardarAsync(objectName, ms, ct);

            job.EstadoCodigo = BackupEstados.Completado;
            job.StoragePath = stored.Path;
            job.TamanoBytes = stored.SizeBytes;
            job.Checksum = checksum;
            job.FinalizadoAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            await Audit(actor, job.Id, empresaId, "OK", $"Backup {origen} en {_storage.Provider}");
            return Result<BackupJobDto>.Ok(ToDto(job));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup {Id} falló", job.Id);
            job.EstadoCodigo = BackupEstados.Fallido;
            job.Error = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
            job.FinalizadoAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            await Audit(actor, job.Id, empresaId, "FAIL", ex.Message);
            return Result<BackupJobDto>.Fail("El respaldo falló: " + ex.Message, "BACKUP_FAILED");
        }
    }

    public async Task<IReadOnlyList<BackupJobDto>> ListarAsync(int max = 50, CancellationToken ct = default)
        => await _db.BackupJobs.AsNoTracking()
            .OrderByDescending(b => b.IniciadoAt)
            .Take(Math.Clamp(max, 1, 500))
            .Select(b => new BackupJobDto
            {
                Id = b.Id, EmpresaId = b.EmpresaId, TipoBackup = b.TipoBackup, EstadoCodigo = b.EstadoCodigo,
                Origen = b.Origen, StorageProvider = b.StorageProvider, StoragePath = b.StoragePath,
                TamanoBytes = b.TamanoBytes, Checksum = b.Checksum, IniciadoAt = b.IniciadoAt,
                FinalizadoAt = b.FinalizadoAt, Error = b.Error,
            })
            .ToListAsync(ct);

    private async Task<string> ConstruirManifiestoAsync(int? empresaId, CancellationToken ct)
    {
        var manifiesto = new
        {
            generadoAt = DateTime.UtcNow,
            empresaId,
            tipo = empresaId is null ? "SISTEMA" : "EMPRESA",
            conteos = new
            {
                empresas = await _db.Empresas.CountAsync(ct),
                usuarios = await _db.Usuarios.CountAsync(ct),
                dteDocumentos = empresaId is int e1
                    ? await _db.DteDocumentos.CountAsync(d => d.EmpresaId == e1, ct)
                    : await _db.DteDocumentos.CountAsync(ct),
                dteEventos = empresaId is int e2
                    ? await _db.DteEventos.CountAsync(d => d.EmpresaId == e2, ct)
                    : await _db.DteEventos.CountAsync(ct),
                catalogos = await _db.Catalogos.CountAsync(ct),
            },
        };
        return JsonSerializer.Serialize(manifiesto, new JsonSerializerOptions { WriteIndented = true });
    }

    private static BackupJobDto ToDto(BackupJob b) => new()
    {
        Id = b.Id, EmpresaId = b.EmpresaId, TipoBackup = b.TipoBackup, EstadoCodigo = b.EstadoCodigo,
        Origen = b.Origen, StorageProvider = b.StorageProvider, StoragePath = b.StoragePath,
        TamanoBytes = b.TamanoBytes, Checksum = b.Checksum, IniciadoAt = b.IniciadoAt,
        FinalizadoAt = b.FinalizadoAt, Error = b.Error,
    };

    private Task Audit(string? actor, int jobId, int? empresaId, string resultado, string detalle)
        => _auditoria.RegistrarAsync(new AuditoriaEvent
        {
            EmpresaId = empresaId,
            Username = actor,
            Modulo = AuditModule,
            Accion = "BACKUP_RUN",
            Entidad = "BackupJob",
            EntidadId = jobId.ToString(),
            Resultado = resultado,
            Detalle = detalle,
        });
}
