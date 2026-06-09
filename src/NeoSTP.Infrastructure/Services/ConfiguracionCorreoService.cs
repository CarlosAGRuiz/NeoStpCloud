using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Application.Common;
using NeoSTP.Application.Comunicaciones;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Domain.Core.Comunicaciones;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

/// <summary>
/// Configuración de correo SMTP por empresa. Cifra la contraseña con ISecretProtector y
/// permite enviar un correo de prueba con el sender por tenant. Aislado por EmpresaId.
/// </summary>
public class ConfiguracionCorreoService : IConfiguracionCorreoService
{
    private const string AuditModule = "CORE";

    private readonly NeoStpDbContext _db;
    private readonly IAuditoriaService _auditoria;
    private readonly ISecretProtector _protector;
    private readonly ITenantEmailSender _sender;

    public ConfiguracionCorreoService(NeoStpDbContext db, IAuditoriaService auditoria, ISecretProtector protector, ITenantEmailSender sender)
    {
        _db = db;
        _auditoria = auditoria;
        _protector = protector;
        _sender = sender;
    }

    public async Task<Result<ConfiguracionCorreoDto>> GetAsync(int empresaId, CancellationToken ct = default)
    {
        var c = await _db.ConfiguracionesCorreo.AsNoTracking().FirstOrDefaultAsync(x => x.EmpresaId == empresaId, ct);
        if (c is null)
            return Result<ConfiguracionCorreoDto>.Ok(new ConfiguracionCorreoDto { Configurado = false });
        return Result<ConfiguracionCorreoDto>.Ok(new ConfiguracionCorreoDto
        {
            Configurado = true, Activo = c.Activo, Host = c.Host, Puerto = c.Puerto, UsarStartTls = c.UsarStartTls,
            Usuario = c.Usuario, TienePassword = !string.IsNullOrEmpty(c.PasswordProtegida),
            FromNombre = c.FromNombre, FromEmail = c.FromEmail,
        });
    }

    public async Task<Result<ConfiguracionCorreoDto>> GuardarAsync(int empresaId, GuardarConfiguracionCorreoRequest request, string? actor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Host)) return Result<ConfiguracionCorreoDto>.Fail("El host SMTP es obligatorio.", "VALIDATION");
        if (string.IsNullOrWhiteSpace(request.FromEmail)) return Result<ConfiguracionCorreoDto>.Fail("El remitente es obligatorio.", "VALIDATION");

        var c = await _db.ConfiguracionesCorreo.FirstOrDefaultAsync(x => x.EmpresaId == empresaId, ct);
        var nuevo = c is null;
        c ??= new ConfiguracionCorreo { EmpresaId = empresaId, CreatedBy = actor };

        c.Activo = request.Activo;
        c.Host = request.Host.Trim();
        c.Puerto = request.Puerto <= 0 ? 587 : request.Puerto;
        c.UsarStartTls = request.UsarStartTls;
        c.Usuario = request.Usuario?.Trim();
        c.FromNombre = request.FromNombre.Trim();
        c.FromEmail = request.FromEmail.Trim();
        if (!string.IsNullOrWhiteSpace(request.Password))
            c.PasswordProtegida = _protector.Protect(request.Password); // cifra; vacío = conserva la anterior

        if (nuevo) _db.ConfiguracionesCorreo.Add(c);
        else { c.UpdatedAt = DateTime.UtcNow; c.UpdatedBy = actor; }
        await _db.SaveChangesAsync(ct);
        await Audit(empresaId, actor, nuevo ? "CREAR_CORREO" : "EDITAR_CORREO", $"{c.Host}:{c.Puerto} ({(c.Activo ? "activo" : "inactivo")})", c.Id);

        return await GetAsync(empresaId, ct);
    }

    public async Task<Result> ProbarAsync(int empresaId, string destino, string? actor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(destino)) return Result.Fail("Indica un correo de destino.", "VALIDATION");
        var existe = await _db.ConfiguracionesCorreo.AsNoTracking().AnyAsync(c => c.EmpresaId == empresaId && c.Activo, ct);
        if (!existe) return Result.Fail("No hay configuración de correo activa para la empresa.", "INVALID_STATE");

        var res = await _sender.EnviarAsync(empresaId, new EmailMessage
        {
            To = destino.Trim(),
            Subject = "Prueba de correo · NeoSTP",
            HtmlBody = "<p>Este es un correo de <strong>prueba</strong> enviado con la configuración SMTP de tu empresa.</p>",
            TextBody = "Correo de prueba enviado con la configuración SMTP de tu empresa.",
        }, ct);
        if (!res.Success) return Result.Fail(res.Detalle ?? res.Mensaje ?? "No se pudo enviar.", "EMAIL_FAILED");
        await Audit(empresaId, actor, "PROBAR_CORREO", destino, 0);
        return Result.Ok();
    }

    private Task Audit(int empresaId, string? actor, string accion, string detalle, int entidadId)
        => _auditoria.RegistrarAsync(new AuditoriaEvent
        {
            EmpresaId = empresaId, Username = actor, Modulo = AuditModule, Accion = accion,
            Entidad = "ConfiguracionCorreo", EntidadId = entidadId.ToString(), Resultado = "OK", Detalle = detalle,
        });
}
