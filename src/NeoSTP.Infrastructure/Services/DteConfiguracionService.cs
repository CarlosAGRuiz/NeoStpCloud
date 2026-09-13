using Microsoft.EntityFrameworkCore;
using NeoSTP.Infrastructure.Dte;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Application.Dte.Dtos;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

public class DteConfiguracionService : IDteConfiguracionService
{
    private const string AuditModule = "DTE_CONFIG";
    private const int MaxVersionesPorEmpresa = 10;
    private static readonly string[] AmbientesValidos = { "PRUEBAS", "PRODUCCION" };

    private readonly NeoStpDbContext _db;
    private readonly ISecretProtector _protector;
    private readonly IHaciendaAuthClient _hacienda;
    private readonly IAuditoriaService _auditoria;

    public DteConfiguracionService(
        NeoStpDbContext db,
        ISecretProtector protector,
        IHaciendaAuthClient hacienda,
        IAuditoriaService auditoria)
    {
        _db = db;
        _protector = protector;
        _hacienda = hacienda;
        _auditoria = auditoria;
    }

    public async Task<Result<DteConfiguracionDto>> GetAsync(int empresaId, CancellationToken ct = default)
    {
        var config = await Load(empresaId, ct, track: false);
        return Result<DteConfiguracionDto>.Ok(MapToDto(config ?? new DteConfiguracion { EmpresaId = empresaId }));
    }

    public async Task<Result<DteConfiguracionDto>> SaveAsync(int empresaId, SaveDteConfiguracionRequest request, string? actor, CancellationToken ct = default)
    {
        var ambiente = (request.AmbienteCodigo ?? "PRUEBAS").Trim().ToUpperInvariant();
        if (!AmbientesValidos.Contains(ambiente))
            return Result<DteConfiguracionDto>.Fail($"Ambiente inválido: {request.AmbienteCodigo}. Debe ser PRUEBAS o PRODUCCION.", "VALIDATION");

        if (!DteTiposEstablecimiento.TryNormalize(request.TipoEstablecimientoCodigo, out var tipoEstablecimientoMh))
            return Result<DteConfiguracionDto>.Fail(
                $"Tipo de establecimiento inválido: {request.TipoEstablecimientoCodigo}. Debe usar un código oficial MH CAT-009.",
                "VALIDATION");

        var empresaExiste = await _db.Empresas.AnyAsync(e => e.Id == empresaId, ct);
        if (!empresaExiste)
            return Result<DteConfiguracionDto>.Fail("Empresa no encontrada.", "EMPRESA_NOT_FOUND");

        var config = await Load(empresaId, ct, track: true);
        var creando = config is null;
        config ??= new DteConfiguracion
        {
            EmpresaId = empresaId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actor,
        };
        if (!creando) await SnapshotAsync(config, "ANTES_GUARDAR", actor, ct);

        var ambienteCambio = config.AmbienteCodigo != ambiente;
        var usuario = string.IsNullOrWhiteSpace(request.UsuarioMh) ? null : request.UsuarioMh.Trim();
        var usuarioCambio = config.UsuarioMh != usuario;
        if (!creando && (ambienteCambio || usuarioCambio) && string.IsNullOrWhiteSpace(request.PasswordMh))
            return Result<DteConfiguracionDto>.Fail("Al cambiar ambiente o usuario de Hacienda, ingrese explícitamente la contraseña correspondiente al nuevo contexto.", "DTE_CREDENCIALES_REQUERIDAS");
        config.AmbienteCodigo = ambiente;
        config.UsuarioMh = usuario;

        if (!string.IsNullOrEmpty(request.PasswordMh))
        {
            config.PasswordMhCifrado = _protector.Protect(request.PasswordMh);
            // Invalidar token cacheado al cambiar la password
            config.TokenMhCifrado = null;
            config.TokenMhExpiraAt = null;
        }

        config.TipoEstablecimientoCodigo = tipoEstablecimientoMh;
        config.CodigoEstablecimientoMh = request.CodigoEstablecimientoMh?.Trim();
        config.CodigoPuntoVentaMh = request.CodigoPuntoVentaMh?.Trim();

        if (!creando)
        {
            config.UpdatedAt = DateTime.UtcNow;
            config.UpdatedBy = actor;
            if (ambienteCambio || usuarioCambio)
            {
                // Cambiar de ambiente invalida cualquier token cacheado
                config.TokenMhCifrado = null;
                config.TokenMhExpiraAt = null;
            }
        }

        if (creando) _db.DteConfiguracion.Add(config);
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, creando ? "CREATE" : "UPDATE", "OK",
            $"Configuracion DTE {(creando ? "creada" : "actualizada")} (ambiente={ambiente})", config.Id);

        return Result<DteConfiguracionDto>.Ok(MapToDto(config));
    }

    public async Task<Result<DteConfiguracionDto>> UploadCertificadoAsync(int empresaId, UploadCertificadoRequest request, string? actor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.ContenidoBase64))
            return Result<DteConfiguracionDto>.Fail("Contenido del certificado requerido.", "VALIDATION");

        byte[] bytes;
        try { bytes = Convert.FromBase64String(request.ContenidoBase64); }
        catch
        {
            return Result<DteConfiguracionDto>.Fail("El contenido no es base64 válido.", "VALIDATION");
        }

        if (bytes.Length == 0 || bytes.Length > 5 * 1024 * 1024)
            return Result<DteConfiguracionDto>.Fail("El certificado debe pesar entre 1 byte y 5 MB.", "VALIDATION");

        var config = await Load(empresaId, ct, track: true)
            ?? new DteConfiguracion { EmpresaId = empresaId, CreatedAt = DateTime.UtcNow, CreatedBy = actor };
        var creando = config.Id == 0;
        if (!creando) await SnapshotAsync(config, "ANTES_CERTIFICADO", actor, ct);

        config.CertificadoBlob = bytes;
        config.CertificadoNombre = string.IsNullOrWhiteSpace(request.Nombre) ? "certificado.pfx" : request.Nombre.Trim();
        config.CertificadoEmitido = request.Emitido;
        config.CertificadoVence = request.Vence;
        config.CertificadoHuella = ComputeSha1(bytes);
        config.PasswordCertificadoCifrado = string.IsNullOrEmpty(request.Password)
            ? null
            : _protector.Protect(request.Password);
        config.UpdatedAt = DateTime.UtcNow;
        config.UpdatedBy = actor;

        if (creando) _db.DteConfiguracion.Add(config);
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "UPLOAD_CERT", "OK",
            $"Certificado {config.CertificadoNombre} cargado ({bytes.Length} bytes, huella={config.CertificadoHuella})", config.Id);

        return Result<DteConfiguracionDto>.Ok(MapToDto(config));
    }

    public async Task<Result> EliminarCertificadoAsync(int empresaId, string? actor, CancellationToken ct = default)
    {
        var config = await Load(empresaId, ct, track: true);
        if (config is null) return Result.Fail("Configuración DTE no encontrada.", "CONFIG_NOT_FOUND");

        await SnapshotAsync(config, "ANTES_ELIMINAR_CERT", actor, ct);
        config.CertificadoBlob = null;
        config.CertificadoNombre = null;
        config.CertificadoHuella = null;
        config.CertificadoEmitido = null;
        config.CertificadoVence = null;
        config.PasswordCertificadoCifrado = null;
        config.UpdatedAt = DateTime.UtcNow;
        config.UpdatedBy = actor;
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "DELETE_CERT", "OK", "Certificado eliminado", config.Id);
        return Result.Ok();
    }

    public async Task<Result<ProbarConexionResultadoDto>> ProbarConexionAsync(int empresaId, string? actor, CancellationToken ct = default)
    {
        var config = await Load(empresaId, ct, track: true);
        if (config is null)
            return Result<ProbarConexionResultadoDto>.Fail("Configuración DTE no encontrada.", "CONFIG_NOT_FOUND");

        if (!DteAmbientes.EsValido(config.AmbienteCodigo))
            return Result<ProbarConexionResultadoDto>.Fail("Ambiente fiscal inválido.", "DTE_AMBIENTE_INVALIDO");
        if (string.IsNullOrEmpty(config.UsuarioMh) || string.IsNullOrEmpty(config.PasswordMhCifrado))
            return Result<ProbarConexionResultadoDto>.Fail("Faltan usuario o password de Hacienda.", "VALIDATION");

        string password;
        try { password = _protector.Unprotect(config.PasswordMhCifrado); }
        catch
        {
            return Result<ProbarConexionResultadoDto>.Fail(
                "No se pudo descifrar el password (¿llave de DataProtection cambió?). Reingresar la contraseña.",
                "DECRYPT_FAILED");
        }

        var resp = await _hacienda.AutenticarAsync(config.UsuarioMh, password, config.AmbienteCodigo, ct);
        config.UltimaPruebaAt = DateTime.UtcNow;
        config.UltimaPruebaResultado = resp.Success ? "OK" : "FAIL";
        config.UltimaPruebaDetalle = $"[{resp.CodigoHttp}] {resp.Mensaje}: {resp.Detalle}";

        if (resp.Success && resp.Token is not null)
        {
            if (!await HaciendaTokenCache.StoreAsync(_db, config, _protector, resp.Token, resp.ExpiresAt, ct))
                return Result<ProbarConexionResultadoDto>.Fail("La configuración fiscal cambió durante la autenticación. Reintente con el ambiente correcto.", "DTE_CONFIG_CAMBIO");
        }

        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "PROBAR_CONEXION", resp.Success ? "OK" : "FAIL",
            config.UltimaPruebaDetalle, config.Id);

        return Result<ProbarConexionResultadoDto>.Ok(new ProbarConexionResultadoDto
        {
            Exitoso = resp.Success,
            Mensaje = resp.Mensaje,
            CodigoHttp = resp.CodigoHttp,
            Detalle = resp.Detalle,
            ProbadoAt = config.UltimaPruebaAt.Value,
        });
    }

    public async Task<Result<IReadOnlyList<DteConfiguracionVersionDto>>> GetVersionesAsync(
        int empresaId, CancellationToken ct = default)
    {
        var versiones = await _db.DteConfiguracionVersiones.AsNoTracking()
            .Where(x => x.EmpresaId == empresaId)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Take(MaxVersionesPorEmpresa)
            .Select(x => new DteConfiguracionVersionDto
            {
                Id = x.Id,
                CreatedAt = x.CreatedAt,
                CreatedBy = x.CreatedBy,
                Motivo = x.Motivo,
                AmbienteCodigo = x.AmbienteCodigo,
                UsuarioMh = x.UsuarioMh,
                TipoEstablecimientoCodigo = x.TipoEstablecimientoCodigo,
                CodigoEstablecimientoMh = x.CodigoEstablecimientoMh,
                CodigoPuntoVentaMh = x.CodigoPuntoVentaMh,
                TieneCertificado = x.CertificadoBlob != null,
                CertificadoNombre = x.CertificadoNombre,
            })
            .ToListAsync(ct);
        return Result<IReadOnlyList<DteConfiguracionVersionDto>>.Ok(versiones);
    }

    public async Task<Result<DteConfiguracionDto>> RecuperarVersionAsync(
        int empresaId, int versionId, string? actor, CancellationToken ct = default)
    {
        var config = await Load(empresaId, ct, track: true);
        if (config is null)
            return Result<DteConfiguracionDto>.Fail("Configuración DTE no encontrada.", "CONFIG_NOT_FOUND");

        var version = await _db.DteConfiguracionVersiones.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == versionId && x.EmpresaId == empresaId, ct);
        if (version is null)
            return Result<DteConfiguracionDto>.Fail("La versión no existe para esta empresa.", "VERSION_NOT_FOUND");

        await SnapshotAsync(config, "ANTES_RECUPERAR", actor, ct);
        config.AmbienteCodigo = version.AmbienteCodigo;
        config.TiposDteAutorizadosCsv = version.TiposDteAutorizadosCsv;
        config.UsuarioMh = version.UsuarioMh;
        config.PasswordMhCifrado = version.PasswordMhCifrado;
        config.TipoEstablecimientoCodigo = version.TipoEstablecimientoCodigo;
        config.CodigoEstablecimientoMh = version.CodigoEstablecimientoMh;
        config.CodigoPuntoVentaMh = version.CodigoPuntoVentaMh;
        config.CertificadoBlob = version.CertificadoBlob?.ToArray();
        config.CertificadoNombre = version.CertificadoNombre;
        config.CertificadoHuella = version.CertificadoHuella;
        config.CertificadoEmitido = version.CertificadoEmitido;
        config.CertificadoVence = version.CertificadoVence;
        config.PasswordCertificadoCifrado = version.PasswordCertificadoCifrado;
        config.TokenMhCifrado = null;
        config.TokenMhExpiraAt = null;
        config.UltimaPruebaAt = null;
        config.UltimaPruebaResultado = null;
        config.UltimaPruebaDetalle = null;
        config.UpdatedAt = DateTime.UtcNow;
        config.UpdatedBy = actor;

        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, "RECUPERAR_VERSION", "OK",
            $"Configuración DTE recuperada desde versión {version.Id}; token MH invalidado.", config.Id);
        return Result<DteConfiguracionDto>.Ok(MapToDto(config));
    }

    // -- helpers -------------------------------------------------------

    private async Task<DteConfiguracion?> Load(int empresaId, CancellationToken ct, bool track)
    {
        var q = _db.DteConfiguracion.AsQueryable();
        if (!track) q = q.AsNoTracking();
        return await q.FirstOrDefaultAsync(c => c.EmpresaId == empresaId, ct);
    }

    private async Task SnapshotAsync(DteConfiguracion config, string motivo, string? actor, CancellationToken ct)
    {
        var obsoletas = await _db.DteConfiguracionVersiones
            .Where(x => x.EmpresaId == config.EmpresaId)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip(MaxVersionesPorEmpresa - 1)
            .ToListAsync(ct);
        _db.DteConfiguracionVersiones.RemoveRange(obsoletas);
        _db.DteConfiguracionVersiones.Add(new DteConfiguracionVersion
        {
            EmpresaId = config.EmpresaId,
            ConfiguracionId = config.Id,
            Motivo = motivo,
            AmbienteCodigo = config.AmbienteCodigo,
            TiposDteAutorizadosCsv = config.TiposDteAutorizadosCsv,
            UsuarioMh = config.UsuarioMh,
            PasswordMhCifrado = config.PasswordMhCifrado,
            TipoEstablecimientoCodigo = config.TipoEstablecimientoCodigo,
            CodigoEstablecimientoMh = config.CodigoEstablecimientoMh,
            CodigoPuntoVentaMh = config.CodigoPuntoVentaMh,
            CertificadoBlob = config.CertificadoBlob?.ToArray(),
            CertificadoNombre = config.CertificadoNombre,
            CertificadoHuella = config.CertificadoHuella,
            CertificadoEmitido = config.CertificadoEmitido,
            CertificadoVence = config.CertificadoVence,
            PasswordCertificadoCifrado = config.PasswordCertificadoCifrado,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actor,
        });
    }

    private static string ComputeSha1(byte[] bytes)
    {
        var hash = System.Security.Cryptography.SHA1.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static DteConfiguracionDto MapToDto(DteConfiguracion c) => new()
    {
        EmpresaId = c.EmpresaId,
        AmbienteCodigo = c.AmbienteCodigo,
        TiposDteAutorizadosCsv = c.TiposDteAutorizadosCsv,
        UsuarioMh = c.UsuarioMh,
        TienePasswordMh = !string.IsNullOrEmpty(c.PasswordMhCifrado),
        TipoEstablecimientoCodigo = c.TipoEstablecimientoCodigo,
        CodigoEstablecimientoMh = c.CodigoEstablecimientoMh,
        CodigoPuntoVentaMh = c.CodigoPuntoVentaMh,
        TieneCertificado = c.CertificadoBlob is { Length: > 0 },
        CertificadoNombre = c.CertificadoNombre,
        CertificadoHuella = c.CertificadoHuella,
        CertificadoEmitido = c.CertificadoEmitido,
        CertificadoVence = c.CertificadoVence,
        CertificadoTienePassword = !string.IsNullOrEmpty(c.PasswordCertificadoCifrado),
        UltimaPruebaAt = c.UltimaPruebaAt,
        UltimaPruebaResultado = c.UltimaPruebaResultado,
        UltimaPruebaDetalle = c.UltimaPruebaDetalle,
        EsCompleto = c.EsCompleto,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt,
    };

    private Task Audit(int empresaId, string? actor, string accion, string resultado, string? detalle, int entidadId)
        => _auditoria.RegistrarAsync(new AuditoriaEvent
        {
            EmpresaId = empresaId, Username = actor,
            Modulo = AuditModule, Accion = accion,
            Entidad = "DteConfiguracion", EntidadId = entidadId.ToString(),
            Resultado = resultado, Detalle = detalle,
        });
}
