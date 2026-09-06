using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Dte;

public static class HaciendaTokenProvider
{
    public static async Task<(bool Success, string? Token, string? Mensaje)> GetAsync(
        NeoStpDbContext db, DteConfiguracion config, IHaciendaAuthClient authClient,
        ISecretProtector protector, CancellationToken ct)
    {
        if (!DteAmbientes.EsValido(config.AmbienteCodigo))
            return (false, null, "Ambiente fiscal inválido. Revise Configuración DTE.");
        if (!await db.DteConfiguracion.AsNoTracking().AnyAsync(c => c.Id == config.Id
            && c.EmpresaId == config.EmpresaId && c.AmbienteCodigo == config.AmbienteCodigo
            && c.UsuarioMh == config.UsuarioMh && c.PasswordMhCifrado == config.PasswordMhCifrado, ct))
            return (false, null, "La configuración fiscal cambió durante la operación. Revise el ambiente y reintente.");

        var cached = HaciendaTokenCache.Read(config, protector);
        if (!string.IsNullOrWhiteSpace(cached)) return (true, cached, null);
        if (string.IsNullOrWhiteSpace(config.UsuarioMh) || string.IsNullOrWhiteSpace(config.PasswordMhCifrado))
            return (false, null, "Faltan credenciales MH en Configuración DTE.");
        string password;
        try { password = protector.Unprotect(config.PasswordMhCifrado); }
        catch { return (false, null, "No se pudo descifrar la contraseña MH. Reingrésela en Configuración DTE."); }
        var auth = await authClient.AutenticarAsync(config.UsuarioMh, password, config.AmbienteCodigo, ct);
        if (!auth.Success || string.IsNullOrWhiteSpace(auth.Token))
            return (false, null, $"Auth MH falló: [{auth.CodigoHttp}] {auth.Mensaje}");
        if (!await HaciendaTokenCache.StoreAsync(db, config, protector, auth.Token, auth.ExpiresAt, ct))
            return (false, null, "La configuración fiscal cambió durante la autenticación. No se transmitió; revise el ambiente.");
        return (true, HaciendaTokenCache.Clean(auth.Token), null);
    }
}
