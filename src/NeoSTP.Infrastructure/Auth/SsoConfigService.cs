using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Auth.Dtos;
using NeoSTP.Application.Common;
using NeoSTP.Domain.Core.Seguridad;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Auth;

/// <summary>
/// CRUD de la configuración SSO por empresa (E3): mapeo dominio→empresa,
/// proveedor OIDC, auto-aprovisionamiento y rol por defecto.
/// </summary>
public sealed class SsoConfigService : ISsoConfigService
{
    private const string AuditModule = "AUTH";

    private readonly NeoStpDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public SsoConfigService(NeoStpDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    public async Task<Result<EmpresaSsoDto>> GetAsync(int empresaId, CancellationToken ct = default)
    {
        var config = await _db.EmpresaSso.AsNoTracking()
            .Include(c => c.RolPorDefecto)
            .FirstOrDefaultAsync(c => c.EmpresaId == empresaId, ct);
        return Result<EmpresaSsoDto>.Ok(config is null
            ? new EmpresaSsoDto { EmpresaId = empresaId, ProveedorCodigo = SsoProveedores.Entra, Configurado = false }
            : ToDto(config));
    }

    public async Task<Result<EmpresaSsoDto>> GuardarAsync(int empresaId, GuardarEmpresaSsoRequest request, string? actor, CancellationToken ct = default)
    {
        if (!SsoProveedores.EsValido(request.ProveedorCodigo))
            return Result<EmpresaSsoDto>.Fail("Proveedor de SSO no soportado.", "VALIDATION");

        var dominio = request.DominioCorreo?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(dominio) || !EsDominioValido(dominio))
            return Result<EmpresaSsoDto>.Fail("El dominio de correo no es válido (ej. contoso.com).", "VALIDATION");

        // El dominio no puede pertenecer a otra empresa.
        var dominioTomado = await _db.EmpresaSso
            .AnyAsync(c => c.DominioCorreo == dominio && c.EmpresaId != empresaId, ct);
        if (dominioTomado)
            return Result<EmpresaSsoDto>.Fail("Ese dominio ya está asignado a otra empresa.", "SSO_DOMINIO_EN_USO");

        if (request.AutoProvisionar)
        {
            if (request.RolPorDefectoId is not int rolId)
                return Result<EmpresaSsoDto>.Fail("El auto-aprovisionamiento requiere un rol por defecto.", "VALIDATION");
            var rolValido = await _db.Roles.AnyAsync(r => r.Id == rolId, ct);
            if (!rolValido)
                return Result<EmpresaSsoDto>.Fail("El rol por defecto no existe.", "VALIDATION");
        }

        var config = await _db.EmpresaSso
            .Include(c => c.RolPorDefecto)
            .FirstOrDefaultAsync(c => c.EmpresaId == empresaId, ct);
        var esNueva = config is null;
        if (config is null)
        {
            config = new EmpresaSso { EmpresaId = empresaId, CreatedBy = actor };
            _db.EmpresaSso.Add(config);
        }

        config.ProveedorCodigo = request.ProveedorCodigo;
        config.Habilitado = request.Habilitado;
        config.DominioCorreo = dominio;
        config.TenantIdExterno = string.IsNullOrWhiteSpace(request.TenantIdExterno) ? null : request.TenantIdExterno.Trim();
        config.AutoProvisionar = request.AutoProvisionar;
        config.RolPorDefectoId = request.AutoProvisionar ? request.RolPorDefectoId : null;
        config.Notas = string.IsNullOrWhiteSpace(request.Notas) ? null : request.Notas.Trim();
        if (!esNueva)
        {
            config.UpdatedAt = DateTime.UtcNow;
            config.UpdatedBy = actor;
        }

        await _db.SaveChangesAsync(ct);
        await _auditoria.RegistrarAsync(new AuditoriaEvent
        {
            EmpresaId = empresaId,
            Username = actor,
            Modulo = AuditModule,
            Accion = "SSO_CONFIG",
            Entidad = "EmpresaSso",
            EntidadId = config.Id.ToString(),
            Resultado = "OK",
            Detalle = $"{config.ProveedorCodigo} · {config.DominioCorreo} · {(config.Habilitado ? "habilitado" : "deshabilitado")}",
        });

        await _db.Entry(config).Reference(c => c.RolPorDefecto).LoadAsync(ct);
        return Result<EmpresaSsoDto>.Ok(ToDto(config));
    }

    private static bool EsDominioValido(string dominio) =>
        dominio.Contains('.') && !dominio.Contains('@') && !dominio.Contains(' ') && dominio.Length <= 200;

    private static EmpresaSsoDto ToDto(EmpresaSso c) => new()
    {
        EmpresaId = c.EmpresaId,
        ProveedorCodigo = c.ProveedorCodigo,
        Habilitado = c.Habilitado,
        DominioCorreo = c.DominioCorreo,
        TenantIdExterno = c.TenantIdExterno,
        AutoProvisionar = c.AutoProvisionar,
        RolPorDefectoId = c.RolPorDefectoId,
        RolPorDefectoNombre = c.RolPorDefecto?.Nombre,
        Notas = c.Notas,
        Configurado = true,
    };
}
