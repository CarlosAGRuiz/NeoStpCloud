using System.Text;
using System.Text.Json;
using NeoSTP.Application.Common;
using NeoSTP.Domain.Core.Dte;

namespace NeoSTP.Infrastructure.Dte;

/// <summary>Comprueba el contexto persistido antes de firmar o contactar Hacienda.</summary>
public static class DteFiscalContext
{
    /// <summary>Obtiene la versión efectiva del JSON, comprobando su identidad antes de persistirlo.</summary>
    public static bool TryGetGeneratedVersion(string? json, DteDocumento document, out int version)
    {
        version = 0;
        if (!TryReadIdentity(json, out var identity)) return false;
        if (!DteAmbientes.EsValido(document.AmbienteCodigo)
            || identity.Ambiente != DteAmbientes.CodigoMh(document.AmbienteCodigo)
            || identity.Tipo != document.TipoDteCodigo || identity.Generacion != document.CodigoGeneracion
            || identity.Control != document.NumeroControl) return false;
        version = identity.Version;
        return true;
    }

    /// <summary>Verifica contexto y versión del JWS contra el sobre; no verifica la firma criptográfica.</summary>
    public static bool CoincideRecepcion(NeoSTP.Application.Dte.Abstractions.HaciendaReceptionRequest request)
    {
        if (!DteAmbientes.EsValido(request.AmbienteCodigo) || request.Ambiente != DteAmbientes.CodigoMh(request.AmbienteCodigo)
            || request.Version < 1 || string.IsNullOrWhiteSpace(request.TipoDte) || string.IsNullOrWhiteSpace(request.CodigoGeneracion)) return false;
        var parts = request.Documento?.Split('.');
        if (parts?.Length != 3) return false;
        try
        {
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            return TryReadIdentity(Encoding.UTF8.GetString(Convert.FromBase64String(payload)), out var identity)
                && identity.Ambiente == request.Ambiente && identity.Version == request.Version
                && identity.Tipo == request.TipoDte && identity.Generacion == request.CodigoGeneracion;
        }
        catch (FormatException) { return false; }
    }

    private sealed record PayloadIdentity(int Version, string Ambiente, string Tipo, string Generacion, string Control);

    private static bool TryReadIdentity(string? json, out PayloadIdentity identity)
    {
        identity = null!;
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            using var parsed = JsonDocument.Parse(json);
            if (parsed.RootElement.ValueKind != JsonValueKind.Object
                || parsed.RootElement.EnumerateObject().Count(p => p.Name == "identificacion") != 1) return false;
            var id = parsed.RootElement.GetProperty("identificacion");
            if (id.ValueKind != JsonValueKind.Object) return false;
            foreach (var key in new[] { "version", "ambiente", "tipoDte", "codigoGeneracion", "numeroControl" })
                if (id.EnumerateObject().Count(p => p.Name == key) != 1) return false;
            if (!id.GetProperty("version").TryGetInt32(out var version) || version < 1) return false;
            var ambiente = id.GetProperty("ambiente").GetString();
            var tipo = id.GetProperty("tipoDte").GetString();
            var generation = id.GetProperty("codigoGeneracion").GetString();
            var control = id.GetProperty("numeroControl").GetString();
            if (string.IsNullOrWhiteSpace(ambiente) || string.IsNullOrWhiteSpace(tipo)
                || string.IsNullOrWhiteSpace(generation) || string.IsNullOrWhiteSpace(control)) return false;
            identity = new(version, ambiente, tipo, generation, control);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException) { return false; }
    }

    public static bool CoincideBodyEvento(string json, string ambiente)
    {
        if (!DteAmbientes.EsValido(ambiente)) return false;
        try
        {
            using var parsed = JsonDocument.Parse(json);
            return parsed.RootElement.GetProperty("ambiente").GetString() == DteAmbientes.CodigoMh(ambiente)
                && CoincideJws(parsed.RootElement.GetProperty("documento").GetString(), ambiente);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        { return false; }
    }

    public static Result Validar(string? ambiente, DteConfiguracion? config)
    {
        if (config is null)
            return Result.Fail("Configure Hacienda para esta empresa antes de continuar.", "CONFIG_NOT_FOUND");
        if (!DteAmbientes.EsValido(ambiente) || !DteAmbientes.EsValido(config.AmbienteCodigo))
            return Result.Fail("Ambiente fiscal inválido. Configure PRUEBAS o PRODUCCION explícitamente.", "DTE_AMBIENTE_INVALIDO");
        if (ambiente != config.AmbienteCodigo)
            return Result.Fail($"El documento o evento pertenece a {ambiente}, pero la empresa está en {config.AmbienteCodigo}. No se puede trasladar entre ambientes. Revise la configuración fiscal; no renumere ni reenvíe este documento en otro ambiente.", "DTE_AMBIENTE_INCOMPATIBLE");
        return Result.Ok();
    }

    public static bool CoincideJson(string? json, string ambiente, DteDocumento? documento = null)
    {
        if (!DteAmbientes.EsValido(ambiente) || string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            using var parsed = JsonDocument.Parse(json);
            var id = parsed.RootElement.GetProperty("identificacion");
            return id.GetProperty("ambiente").GetString() == DteAmbientes.CodigoMh(ambiente)
                && (documento is null || (
                    id.GetProperty("codigoGeneracion").GetString() == documento.CodigoGeneracion
                    && id.GetProperty("numeroControl").GetString() == documento.NumeroControl
                    && id.GetProperty("tipoDte").GetString() == documento.TipoDteCodigo));
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        { return false; }
    }

    // Esto no verifica la firma criptográfica: verifica el contexto del payload que se enviará.
    public static bool CoincideJws(string? jws, string ambiente, DteDocumento? documento = null)
    {
        var parts = jws?.Split('.');
        if (parts?.Length != 3) return false;
        try
        {
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            return CoincideJson(Encoding.UTF8.GetString(Convert.FromBase64String(payload)), ambiente, documento);
        }
        catch (FormatException) { return false; }
    }
}
