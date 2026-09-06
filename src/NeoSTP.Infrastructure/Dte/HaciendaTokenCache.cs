using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Dte;

/// <summary>Un token solo es reutilizable por la empresa, ambiente y credenciales que lo obtuvieron.</summary>
public static class HaciendaTokenCache
{
    private sealed record Envelope(string Context, string Token);

    private static string Context(DteConfiguracion c) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new object?[]
            { c.EmpresaId, c.AmbienteCodigo, c.UsuarioMh, c.PasswordMhCifrado }))));

    public static string? Read(DteConfiguracion config, ISecretProtector protector)
    {
        if (!DteAmbientes.EsValido(config.AmbienteCodigo) || string.IsNullOrEmpty(config.TokenMhCifrado)
            || config.TokenMhExpiraAt <= DateTime.UtcNow.AddMinutes(5) || config.TokenMhExpiraAt is null) return null;
        try
        {
            var cached = JsonSerializer.Deserialize<Envelope>(protector.Unprotect(config.TokenMhCifrado));
            return cached?.Context == Context(config) ? Clean(cached.Token) : null;
        }
        catch { return null; } // Cachés antiguas sin contexto o cifrado ilegible requieren autenticación nueva.
    }

    public static async Task<bool> StoreAsync(NeoStpDbContext db, DteConfiguracion config,
        ISecretProtector protector, string token, DateTime? expiresAt, CancellationToken ct)
    {
        var cipher = protector.Protect(JsonSerializer.Serialize(new Envelope(Context(config), Clean(token))));
        var expiry = expiresAt ?? DateTime.UtcNow.AddHours(8);
        var matching = db.DteConfiguracion.Where(c => c.Id == config.Id && c.EmpresaId == config.EmpresaId
            && c.AmbienteCodigo == config.AmbienteCodigo && c.UsuarioMh == config.UsuarioMh
            && c.PasswordMhCifrado == config.PasswordMhCifrado);
        if (db.Database.IsRelational())
        {
            // CAS: una respuesta de auth tardía no puede escribir un token sobre credenciales nuevas.
            if (await matching.ExecuteUpdateAsync(s => s.SetProperty(c => c.TokenMhCifrado, cipher)
                .SetProperty(c => c.TokenMhExpiraAt, expiry), ct) != 1) return false;
        }
        else if (!await matching.AsNoTracking().AnyAsync(ct)) return false;
        config.TokenMhCifrado = cipher;
        config.TokenMhExpiraAt = expiry;
        if (db.Database.IsRelational())
        {
            db.Entry(config).Property(c => c.TokenMhCifrado).OriginalValue = cipher;
            db.Entry(config).Property(c => c.TokenMhExpiraAt).OriginalValue = expiry;
        }
        else await db.SaveChangesAsync(ct);
        return true;
    }

    public static string Clean(string token) => token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
        ? token[7..].Trim() : token.Trim();
}
