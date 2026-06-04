using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Empresas;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

/// <summary>
/// Branding por empresa (logo y firma). Valida tipo/tamaño y persiste las imágenes
/// como blobs en <c>Core_Empresas</c>. Aislado por EmpresaId; acciones auditadas.
/// </summary>
public class BrandingService : IBrandingService
{
    private const string AuditModule = "EMPRESAS";
    private const int MaxBytes = 1_048_576; // 1 MB
    private static readonly string[] TiposPermitidos = ["image/png", "image/jpeg", "image/jpg", "image/webp"];

    private readonly NeoStpDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public BrandingService(NeoStpDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    public async Task<BrandingDto> GetAsync(int empresaId, CancellationToken ct = default)
    {
        var e = await _db.Empresas.AsNoTracking()
            .Where(x => x.Id == empresaId)
            .Select(x => new
            {
                TieneLogo = x.LogoBlob != null && x.LogoBlob.Length > 0,
                x.LogoContentType,
                TieneFirma = x.FirmaBlob != null && x.FirmaBlob.Length > 0,
                x.FirmaContentType,
                x.FirmaTexto,
            })
            .FirstOrDefaultAsync(ct);

        if (e is null) return new BrandingDto();
        return new BrandingDto
        {
            TieneLogo = e.TieneLogo,
            LogoContentType = e.LogoContentType,
            TieneFirma = e.TieneFirma,
            FirmaContentType = e.FirmaContentType,
            FirmaTexto = e.FirmaTexto,
        };
    }

    public async Task<BrandingImagen?> GetLogoAsync(int empresaId, CancellationToken ct = default)
    {
        var e = await _db.Empresas.AsNoTracking()
            .Where(x => x.Id == empresaId && x.LogoBlob != null)
            .Select(x => new { x.LogoBlob, x.LogoContentType })
            .FirstOrDefaultAsync(ct);
        return e?.LogoBlob is { Length: > 0 }
            ? new BrandingImagen(e.LogoBlob, e.LogoContentType ?? "image/png")
            : null;
    }

    public async Task<BrandingImagen?> GetFirmaAsync(int empresaId, CancellationToken ct = default)
    {
        var e = await _db.Empresas.AsNoTracking()
            .Where(x => x.Id == empresaId && x.FirmaBlob != null)
            .Select(x => new { x.FirmaBlob, x.FirmaContentType })
            .FirstOrDefaultAsync(ct);
        return e?.FirmaBlob is { Length: > 0 }
            ? new BrandingImagen(e.FirmaBlob, e.FirmaContentType ?? "image/png")
            : null;
    }

    public Task<Result> GuardarLogoAsync(int empresaId, byte[] contenido, string contentType, string fileName, string? actor, CancellationToken ct = default)
        => GuardarImagenAsync(empresaId, contenido, contentType, esLogo: true, actor, ct);

    public Task<Result> GuardarFirmaAsync(int empresaId, byte[] contenido, string contentType, string fileName, string? actor, CancellationToken ct = default)
        => GuardarImagenAsync(empresaId, contenido, contentType, esLogo: false, actor, ct);

    private async Task<Result> GuardarImagenAsync(int empresaId, byte[] contenido, string contentType, bool esLogo, string? actor, CancellationToken ct)
    {
        if (contenido is null || contenido.Length == 0)
            return Result.Fail("El archivo está vacío.", "VALIDATION");
        if (contenido.Length > MaxBytes)
            return Result.Fail($"La imagen excede el máximo de {MaxBytes / 1024} KB.", "VALIDATION");

        var ct2 = (contentType ?? string.Empty).ToLowerInvariant();
        if (!TiposPermitidos.Contains(ct2))
            return Result.Fail("Formato no soportado. Usa PNG, JPG o WEBP.", "VALIDATION");

        var e = await _db.Empresas.FirstOrDefaultAsync(x => x.Id == empresaId, ct);
        if (e is null) return Result.Fail("Empresa no encontrada.", "EMPRESA_NOT_FOUND");

        if (esLogo) { e.LogoBlob = contenido; e.LogoContentType = ct2; }
        else { e.FirmaBlob = contenido; e.FirmaContentType = ct2; }
        e.UpdatedAt = DateTime.UtcNow; e.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, esLogo ? "BRANDING_LOGO" : "BRANDING_FIRMA", $"{contenido.Length} bytes ({ct2})");
        return Result.Ok();
    }

    public async Task<Result> GuardarFirmaTextoAsync(int empresaId, string? texto, string? actor, CancellationToken ct = default)
    {
        var limpio = string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
        if (limpio is { Length: > 300 })
            return Result.Fail("El texto de firma no puede exceder 300 caracteres.", "VALIDATION");

        var e = await _db.Empresas.FirstOrDefaultAsync(x => x.Id == empresaId, ct);
        if (e is null) return Result.Fail("Empresa no encontrada.", "EMPRESA_NOT_FOUND");
        e.FirmaTexto = limpio;
        e.UpdatedAt = DateTime.UtcNow; e.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "BRANDING_FIRMA_TEXTO", limpio ?? "(vacío)");
        return Result.Ok();
    }

    public async Task<Result> EliminarLogoAsync(int empresaId, string? actor, CancellationToken ct = default)
    {
        var e = await _db.Empresas.FirstOrDefaultAsync(x => x.Id == empresaId, ct);
        if (e is null) return Result.Fail("Empresa no encontrada.", "EMPRESA_NOT_FOUND");
        e.LogoBlob = null; e.LogoContentType = null;
        e.UpdatedAt = DateTime.UtcNow; e.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "BRANDING_LOGO_QUITAR", "logo eliminado");
        return Result.Ok();
    }

    public async Task<Result> EliminarFirmaAsync(int empresaId, string? actor, CancellationToken ct = default)
    {
        var e = await _db.Empresas.FirstOrDefaultAsync(x => x.Id == empresaId, ct);
        if (e is null) return Result.Fail("Empresa no encontrada.", "EMPRESA_NOT_FOUND");
        e.FirmaBlob = null; e.FirmaContentType = null;
        e.UpdatedAt = DateTime.UtcNow; e.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "BRANDING_FIRMA_QUITAR", "firma eliminada");
        return Result.Ok();
    }

    private Task Audit(int empresaId, string? actor, string accion, string detalle)
        => _auditoria.RegistrarAsync(new AuditoriaEvent
        {
            EmpresaId = empresaId, Username = actor,
            Modulo = AuditModule, Accion = accion,
            Entidad = "Empresa", EntidadId = empresaId.ToString(),
            Resultado = "OK", Detalle = detalle,
        });
}
