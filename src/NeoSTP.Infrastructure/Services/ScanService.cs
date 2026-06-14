using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Profit;
using NeoSTP.Application.Profit.Dtos;
using NeoSTP.Application.Scan;
using NeoSTP.Application.Scan.Dtos;
using NeoSTP.Domain.Core.Scan;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

/// <summary>
/// NeoScanAI: bandeja de documentos, extracción (pluggable), revisión y conversión a
/// gasto/compra/DTE recibido. Confirmar como gasto/compra reusa NeoProfit (alimenta Profit).
/// </summary>
public class ScanService : IScanService
{
    private const string AuditModule = "NEOSCANAI";
    private const int MaxBytes = 8_388_608; // 8 MB
    private const decimal DefaultConfianzaMinimaProcesado = 0.8m;
    private static readonly string[] DefaultAllowedContentTypes = ["image/jpeg", "image/png", "application/pdf"];

    private readonly NeoStpDbContext _db;
    private readonly IScanExtractionService _extraction;
    private readonly IProfitService _profit;
    private readonly IAuditoriaService _auditoria;
    private readonly int _limiteMensual;
    private readonly decimal _confianzaMinimaProcesado;
    private readonly TimeSpan _ocrTimeout;
    private readonly HashSet<string> _allowedContentTypes;

    private readonly NeoSTP.Infrastructure.Scan.IScanBlobStorage? _blobStorage;

    public ScanService(NeoStpDbContext db, IScanExtractionService extraction, IProfitService profit,
        IAuditoriaService auditoria, IConfiguration? configuration = null,
        NeoSTP.Infrastructure.Scan.IScanBlobStorage? blobStorage = null)
    {
        _db = db;
        _extraction = extraction;
        _profit = profit;
        _auditoria = auditoria;
        _blobStorage = blobStorage;
        // Límite mensual de escaneos por empresa (0/ausente = sin límite). Configurable: Scan:LimiteMensual.
        _limiteMensual = configuration?.GetValue("Scan:LimiteMensual", 0) ?? 0;
        _confianzaMinimaProcesado = Math.Clamp(
            configuration?.GetValue<decimal?>("Scan:ConfianzaMinimaProcesado") ?? DefaultConfianzaMinimaProcesado,
            0m,
            1m);
        _ocrTimeout = TimeSpan.FromSeconds(Math.Clamp(
            configuration?.GetValue<int?>("Scan:OcrTimeoutSeconds") ?? 25,
            1,
            25));
        _allowedContentTypes = ParseAllowedContentTypes(configuration?["Scan:AllowedContentTypes"]);
    }

    public async Task<Result<PagedResult<ScanDocumentoDto>>> ListAsync(int empresaId, ScanQuery query, CancellationToken ct = default)
    {
        var q = _db.ScanDocumentos.AsNoTracking().Where(s => s.EmpresaId == empresaId);
        if (!string.IsNullOrWhiteSpace(query.EstadoCodigo)) q = q.Where(s => s.EstadoCodigo == query.EstadoCodigo);
        if (!string.IsNullOrWhiteSpace(query.TipoClasificacion)) q = q.Where(s => s.TipoClasificacion == query.TipoClasificacion);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(x => EF.Functions.Like(x.EmisorNombre ?? string.Empty, $"%{s}%")
                          || EF.Functions.Like(x.NumeroControl ?? string.Empty, $"%{s}%")
                          || EF.Functions.Like(x.ArchivoNombre ?? string.Empty, $"%{s}%"));
        }

        var total = await q.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var items = await q.OrderByDescending(s => s.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => ToDto(s)).ToListAsync(ct);

        return Result<PagedResult<ScanDocumentoDto>>.Ok(PagedResult<ScanDocumentoDto>.Create(items, total, page, pageSize));
    }

    public async Task<Result<ScanDocumentoDto>> GetAsync(int empresaId, int id, CancellationToken ct = default)
    {
        var s = await _db.ScanDocumentos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        return s is null ? Result<ScanDocumentoDto>.Fail("Escaneo no encontrado.", "SCAN_NOT_FOUND") : Result<ScanDocumentoDto>.Ok(ToDto(s));
    }

    public async Task<ScanArchivo?> GetArchivoAsync(int empresaId, int id, CancellationToken ct = default)
    {
        var s = await _db.ScanDocumentos.AsNoTracking()
            .Where(x => x.Id == id && x.EmpresaId == empresaId && (x.ArchivoBlob != null || x.ArchivoPath != null))
            .Select(x => new { x.ArchivoBlob, x.ArchivoPath, x.ArchivoContentType, x.ArchivoNombre })
            .FirstOrDefaultAsync(ct);
        if (s is null) return null;

        var bytes = s.ArchivoBlob;
        if (bytes is not { Length: > 0 } && s.ArchivoPath is not null && _blobStorage is not null)
            bytes = await _blobStorage.LeerAsync(s.ArchivoPath, ct);

        return bytes is { Length: > 0 }
            ? new ScanArchivo(bytes, s.ArchivoContentType ?? "application/octet-stream", s.ArchivoNombre ?? "captura")
            : null;
    }

    public async Task<Result<ScanDocumentoDto>> SubirAsync(int empresaId, SubirScanRequest request, string? actor, CancellationToken ct = default)
    {
        byte[] bytes;
        try { bytes = Convert.FromBase64String(request.ContenidoBase64 ?? string.Empty); }
        catch { return Result<ScanDocumentoDto>.Fail("Contenido base64 inválido.", "VALIDATION"); }
        if (bytes.Length == 0) return Result<ScanDocumentoDto>.Fail("El archivo está vacío.", "VALIDATION");
        if (bytes.Length > MaxBytes) return Result<ScanDocumentoDto>.Fail($"El archivo excede {MaxBytes / 1024 / 1024} MB.", "VALIDATION");

        var contentType = NormalizeContentType(request.ContentType);
        if (!_allowedContentTypes.Contains(contentType))
            return Result<ScanDocumentoDto>.Fail("Tipo de archivo no permitido. Usa JPEG, PNG o PDF.", "VALIDATION");

        if (_limiteMensual > 0)
        {
            var inicioMes = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var usados = await _db.ScanDocumentos.CountAsync(s => s.EmpresaId == empresaId && s.CreatedAt >= inicioMes, ct);
            if (usados >= _limiteMensual)
                return Result<ScanDocumentoDto>.Fail($"Alcanzaste el límite mensual de {_limiteMensual} escaneos.", "LIMIT_EXCEEDED");
        }

        var entity = new ScanDocumento
        {
            EmpresaId = empresaId,
            EstadoCodigo = ScanEstados.Procesando,
            Origen = string.IsNullOrWhiteSpace(request.Origen) ? "MOBILE" : request.Origen.Trim().ToUpperInvariant(),
            ArchivoContentType = contentType,
            ArchivoNombre = request.Nombre,
            CreatedBy = actor,
        };

        // V2.5-S4: con storage externo configurado los bytes no entran a la BD.
        if (_blobStorage is not null)
            entity.ArchivoPath = await _blobStorage.GuardarAsync(empresaId, request.Nombre, bytes, ct);
        else
            entity.ArchivoBlob = bytes;

        var ext = await ExtraerConTimeoutAsync(bytes, contentType, ct);
        AplicarResultadoOcr(entity, ext);

        _db.ScanDocumentos.Add(entity);
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "SUBIR", $"Captura {entity.ArchivoNombre} ({bytes.Length} bytes, conf {entity.Confianza:0.##}, ocr {entity.OcrProveedor}/{entity.OcrModelo})", entity.Id);
        return Result<ScanDocumentoDto>.Ok(ToDto(entity));
    }

    public async Task<Result<ScanDocumentoDto>> ReprocesarAsync(int empresaId, int id, string? actor, CancellationToken ct = default)
    {
        var s = await _db.ScanDocumentos.FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        if (s is null) return Result<ScanDocumentoDto>.Fail("Escaneo no encontrado.", "SCAN_NOT_FOUND");
        if (s.EstadoCodigo is ScanEstados.Confirmado or ScanEstados.Rechazado)
            return Result<ScanDocumentoDto>.Fail("El escaneo ya esta confirmado o rechazado.", "INVALID_STATE");

        var bytes = s.ArchivoBlob;
        if (bytes is not { Length: > 0 } && s.ArchivoPath is not null && _blobStorage is not null)
            bytes = await _blobStorage.LeerAsync(s.ArchivoPath, ct);
        if (bytes is not { Length: > 0 })
            return Result<ScanDocumentoDto>.Fail("Archivo no encontrado para reprocesar.", "SCAN_FILE_NOT_FOUND");

        var contentType = NormalizeContentType(s.ArchivoContentType);
        if (!_allowedContentTypes.Contains(contentType))
            return Result<ScanDocumentoDto>.Fail("Tipo de archivo no permitido. Usa JPEG, PNG o PDF.", "VALIDATION");

        s.EstadoCodigo = ScanEstados.Procesando;
        s.UpdatedAt = DateTime.UtcNow;
        s.UpdatedBy = actor;

        var ext = await ExtraerConTimeoutAsync(bytes, contentType, ct);
        AplicarResultadoOcr(s, ext);
        s.UpdatedAt = DateTime.UtcNow;
        s.UpdatedBy = actor;

        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "REPROCESAR", $"Scan #{id} reprocesado (conf {s.Confianza:0.##}, ocr {s.OcrProveedor}/{s.OcrModelo})", id);
        return Result<ScanDocumentoDto>.Ok(ToDto(s));
    }

    public Task<Result<ScanDocumentoDto>> CorregirAsync(int empresaId, int id, CorregirScanRequest request, string? actor, CancellationToken ct = default)
        => MutarAsync(empresaId, id, actor, "CORREGIR", s =>
        {
            AplicarCorreccion(s, request);
            if (s.EstadoCodigo is ScanEstados.Recibido or ScanEstados.Procesando)
                s.EstadoCodigo = ScanEstados.RequiereRevision;
        }, ct);

    public Task<Result<ScanDocumentoDto>> SetResultadoAsync(int empresaId, int id, ScanResultadoRequest request, string? actor, CancellationToken ct = default)
        => MutarAsync(empresaId, id, actor, "RESULTADO", s =>
        {
            AplicarCorreccion(s, request);
            s.Confianza = Math.Clamp(request.Confianza, 0m, 1m);
            s.OcrProveedor = Truncar(request.OcrProveedor ?? "External", 50);
            s.OcrModelo = Truncar(request.OcrModelo, 100);
            s.OcrDuracionMs = request.OcrDuracionMs;
            s.OcrErrorResumen = Truncar(request.OcrErrorResumen, 500);
            s.OcrIntentos += 1;
            s.OcrUltimoIntentoAt = DateTime.UtcNow;
            s.EstadoCodigo = request.Completo ? ScanEstados.Procesado : ScanEstados.RequiereRevision;
        }, ct);

    public async Task<Result<ScanDocumentoDto>> ConfirmarComoGastoAsync(int empresaId, int id, CreateProfitGastoRequest request, string? actor, CancellationToken ct = default)
    {
        var (s, err) = await CargarConfirmable(empresaId, id, ct);
        if (err is not null) return err;

        var gasto = await _profit.CreateGastoAsync(empresaId, request, actor, ct);
        if (gasto.IsFailure) return Result<ScanDocumentoDto>.Fail(gasto.Error ?? "Error al crear gasto.", gasto.ErrorCode, gasto.ValidationErrors);

        s!.TipoClasificacion = ScanTipos.Gasto;
        s.ProfitGastoId = gasto.Value!.Id;
        s.EstadoCodigo = ScanEstados.Confirmado;
        s.UpdatedAt = DateTime.UtcNow; s.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "CONFIRMAR_GASTO", $"Scan #{id} → ProfitGasto #{gasto.Value.Id}", id);
        return Result<ScanDocumentoDto>.Ok(ToDto(s));
    }

    public async Task<Result<ScanDocumentoDto>> ConfirmarComoCompraAsync(int empresaId, int id, CreateProfitCompraRequest request, string? actor, CancellationToken ct = default)
    {
        var (s, err) = await CargarConfirmable(empresaId, id, ct);
        if (err is not null) return err;

        var compra = await _profit.CreateCompraAsync(empresaId, request, actor, ct);
        if (compra.IsFailure) return Result<ScanDocumentoDto>.Fail(compra.Error ?? "Error al crear compra.", compra.ErrorCode, compra.ValidationErrors);

        s!.TipoClasificacion = ScanTipos.Compra;
        s.ProfitCompraId = compra.Value!.Id;
        s.EstadoCodigo = ScanEstados.Confirmado;
        s.UpdatedAt = DateTime.UtcNow; s.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "CONFIRMAR_COMPRA", $"Scan #{id} → ProfitCompra #{compra.Value.Id}", id);
        return Result<ScanDocumentoDto>.Ok(ToDto(s));
    }

    public async Task<Result<ScanDocumentoDto>> RegistrarDteRecibidoAsync(int empresaId, int id, RegistrarDteRecibidoRequest request, string? actor, CancellationToken ct = default)
    {
        var (s, err) = await CargarConfirmable(empresaId, id, ct);
        if (err is not null) return err;
        if (string.IsNullOrWhiteSpace(request.EmisorNombre))
            return Result<ScanDocumentoDto>.Fail("El emisor es obligatorio.", "VALIDATION");

        var recibido = new DteDocumentoRecibido
        {
            EmpresaId = empresaId,
            EmisorNombre = request.EmisorNombre.Trim(),
            EmisorNit = request.EmisorNit?.Trim(),
            EmisorNrc = request.EmisorNrc?.Trim(),
            Fecha = request.Fecha ?? DateOnly.FromDateTime(DateTime.UtcNow),
            TipoDteCodigo = request.TipoDteCodigo,
            NumeroControl = request.NumeroControl?.Trim(),
            SelloRecibido = request.SelloRecibido?.Trim(),
            Subtotal = request.Subtotal, Iva = request.Iva, Total = request.Total,
            ScanDocumentoId = id,
            CreatedBy = actor,
        };
        _db.DteDocumentosRecibidos.Add(recibido);
        await _db.SaveChangesAsync(ct);

        s!.TipoClasificacion = ScanTipos.DteRecibido;
        s.DteRecibidoId = recibido.Id;
        s.EstadoCodigo = ScanEstados.Confirmado;
        s.UpdatedAt = DateTime.UtcNow; s.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "CONFIRMAR_DTE_RECIBIDO", $"Scan #{id} → DteRecibido #{recibido.Id}", id);
        return Result<ScanDocumentoDto>.Ok(ToDto(s));
    }

    public async Task<Result> RechazarAsync(int empresaId, int id, string? motivo, string? actor, CancellationToken ct = default)
    {
        var s = await _db.ScanDocumentos.FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        if (s is null) return Result.Fail("Escaneo no encontrado.", "SCAN_NOT_FOUND");
        if (s.EstadoCodigo is ScanEstados.Confirmado or ScanEstados.Rechazado)
            return Result.Fail("El escaneo ya está confirmado o rechazado.", "INVALID_STATE");
        s.EstadoCodigo = ScanEstados.Rechazado;
        if (!string.IsNullOrWhiteSpace(motivo)) s.Notas = motivo.Trim();
        s.UpdatedAt = DateTime.UtcNow; s.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "RECHAZAR", motivo ?? "Sin motivo", id);
        return Result.Ok();
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────

    private async Task<ScanExtraccion> ExtraerConTimeoutAsync(byte[] bytes, string contentType, CancellationToken ct)
    {
        var startedAt = DateTime.UtcNow;
        var sw = Stopwatch.StartNew();

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(_ocrTimeout);
            var ext = await _extraction.ExtraerAsync(bytes, contentType, timeoutCts.Token);
            ext.OcrProveedor ??= InferOcrProveedor();
            ext.OcrDuracionMs ??= sw.ElapsedMilliseconds;
            ext.OcrIntentoAt ??= startedAt;
            return ext;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return OcrFallback(startedAt, sw, "OCR_TIMEOUT");
        }
        catch (Exception ex)
        {
            return OcrFallback(startedAt, sw, ex.GetType().Name);
        }
    }

    private ScanExtraccion OcrFallback(DateTime startedAt, Stopwatch sw, string error)
        => new()
        {
            Confianza = 0m,
            OcrProveedor = InferOcrProveedor(),
            OcrDuracionMs = sw.ElapsedMilliseconds,
            OcrErrorResumen = Truncar(error, 500),
            OcrIntentoAt = startedAt,
        };

    private void AplicarResultadoOcr(ScanDocumento s, ScanExtraccion ext)
    {
        Aplicar(s, ext);
        s.Confianza = Math.Clamp(ext.Confianza, 0m, 1m);
        s.OcrProveedor = Truncar(ext.OcrProveedor ?? InferOcrProveedor(), 50);
        s.OcrModelo = Truncar(ext.OcrModelo, 100);
        s.OcrDuracionMs = ext.OcrDuracionMs;
        s.OcrErrorResumen = Truncar(ext.OcrErrorResumen, 500);
        s.OcrIntentos += 1;
        s.OcrUltimoIntentoAt = ext.OcrIntentoAt ?? DateTime.UtcNow;
        s.EstadoCodigo = s.Confianza >= _confianzaMinimaProcesado
            ? ScanEstados.Procesado
            : ScanEstados.RequiereRevision;
    }

    private string InferOcrProveedor()
    {
        var name = _extraction.GetType().Name;
        return name.EndsWith("ScanExtractionService", StringComparison.Ordinal)
            ? name[..^"ScanExtractionService".Length]
            : name;
    }

    private async Task<(ScanDocumento? scan, Result<ScanDocumentoDto>? error)> CargarConfirmable(int empresaId, int id, CancellationToken ct)
    {
        var s = await _db.ScanDocumentos.FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        if (s is null) return (null, Result<ScanDocumentoDto>.Fail("Escaneo no encontrado.", "SCAN_NOT_FOUND"));
        if (s.EstadoCodigo is ScanEstados.Confirmado or ScanEstados.Rechazado)
            return (null, Result<ScanDocumentoDto>.Fail("El escaneo ya está confirmado o rechazado.", "INVALID_STATE"));
        return (s, null);
    }

    private async Task<Result<ScanDocumentoDto>> MutarAsync(int empresaId, int id, string? actor, string accion, Action<ScanDocumento> mutar, CancellationToken ct)
    {
        var s = await _db.ScanDocumentos.FirstOrDefaultAsync(x => x.Id == id && x.EmpresaId == empresaId, ct);
        if (s is null) return Result<ScanDocumentoDto>.Fail("Escaneo no encontrado.", "SCAN_NOT_FOUND");
        if (s.EstadoCodigo is ScanEstados.Confirmado or ScanEstados.Rechazado)
            return Result<ScanDocumentoDto>.Fail("El escaneo ya está confirmado o rechazado.", "INVALID_STATE");
        mutar(s);
        s.UpdatedAt = DateTime.UtcNow; s.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, accion, $"Scan #{id}", id);
        return Result<ScanDocumentoDto>.Ok(ToDto(s));
    }

    private static void Aplicar(ScanDocumento s, ScanExtraccion e)
    {
        s.EmisorNombre = e.EmisorNombre; s.EmisorNit = e.EmisorNit; s.EmisorNrc = e.EmisorNrc;
        s.Fecha = e.Fecha; s.TipoDocumento = e.TipoDocumento; s.NumeroControl = e.NumeroControl;
        s.SelloRecibido = e.SelloRecibido; s.Subtotal = e.Subtotal; s.Iva = e.Iva; s.Total = e.Total;
    }

    private static void AplicarCorreccion(ScanDocumento s, CorregirScanRequest r)
    {
        s.EmisorNombre = r.EmisorNombre?.Trim(); s.EmisorNit = r.EmisorNit?.Trim(); s.EmisorNrc = r.EmisorNrc?.Trim();
        s.Fecha = r.Fecha; s.TipoDocumento = r.TipoDocumento?.Trim(); s.NumeroControl = r.NumeroControl?.Trim();
        s.SelloRecibido = r.SelloRecibido?.Trim(); s.Subtotal = r.Subtotal; s.Iva = r.Iva; s.Total = r.Total;
        s.Notas = r.Notas?.Trim();
    }

    private static string NormalizeContentType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "image/jpeg";
        var separator = value.IndexOf(';');
        var normalized = separator >= 0 ? value[..separator] : value;
        return normalized.Trim().ToLowerInvariant();
    }

    private static string? Truncar(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static HashSet<string> ParseAllowedContentTypes(string? raw)
    {
        var values = string.IsNullOrWhiteSpace(raw)
            ? DefaultAllowedContentTypes
            : raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var parsed = values
            .Select(NormalizeContentType)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return parsed.Count > 0
            ? parsed
            : DefaultAllowedContentTypes.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static ScanDocumentoDto ToDto(ScanDocumento s) => new()
    {
        Id = s.Id, EstadoCodigo = s.EstadoCodigo, TipoClasificacion = s.TipoClasificacion, Origen = s.Origen,
        ArchivoNombre = s.ArchivoNombre, ArchivoContentType = s.ArchivoContentType,
        TieneArchivo = (s.ArchivoBlob != null && s.ArchivoBlob.Length > 0) || s.ArchivoPath != null,
        EmisorNombre = s.EmisorNombre, EmisorNit = s.EmisorNit, EmisorNrc = s.EmisorNrc, Fecha = s.Fecha,
        TipoDocumento = s.TipoDocumento, NumeroControl = s.NumeroControl, SelloRecibido = s.SelloRecibido,
        Subtotal = s.Subtotal, Iva = s.Iva, Total = s.Total, Confianza = s.Confianza, Notas = s.Notas,
        OcrProveedor = s.OcrProveedor, OcrModelo = s.OcrModelo, OcrDuracionMs = s.OcrDuracionMs,
        OcrErrorResumen = s.OcrErrorResumen, OcrIntentos = s.OcrIntentos, OcrUltimoIntentoAt = s.OcrUltimoIntentoAt,
        ProfitGastoId = s.ProfitGastoId, ProfitCompraId = s.ProfitCompraId, DteRecibidoId = s.DteRecibidoId,
        CreatedAt = s.CreatedAt,
    };

    private Task Audit(int empresaId, string? actor, string accion, string detalle, int entidadId)
        => _auditoria.RegistrarAsync(new AuditoriaEvent
        {
            EmpresaId = empresaId, Username = actor,
            Modulo = AuditModule, Accion = accion,
            Entidad = "ScanDocumento", EntidadId = entidadId.ToString(),
            Resultado = "OK", Detalle = detalle,
        });
}
