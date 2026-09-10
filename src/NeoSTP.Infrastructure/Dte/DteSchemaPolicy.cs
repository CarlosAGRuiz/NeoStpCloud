using System.Globalization;
using Microsoft.Extensions.Configuration;
using NeoSTP.Application.Common;
using NeoSTP.Domain.Core.Dte;

namespace NeoSTP.Infrastructure.Dte;

/// <summary>Operational document schema selection. Does not grant fiscal authorization or select event schemas.</summary>
internal sealed class DteSchemaPolicy
{
    internal const string ModernProfile = "MH_20260825";
    private readonly bool _globalDefault;
    private readonly Dictionary<int, Entry> _entries = [];
    private readonly bool _invalid;
    private sealed record Entry(string? Nit, string? Ambiente, string? Profile, bool Invalid);

    internal DteSchemaPolicy(IConfiguration? configuration)
    {
        _globalDefault = configuration?.GetValue<bool>("Dte:EsquemaNuevo") ?? false;
        var section = configuration?.GetSection("Dte:TenantSchemas");
        if (section is null) return;
        if (section.Value is not null) _invalid = true;
        foreach (var child in section.GetChildren())
        {
            if (!int.TryParse(child.Key, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id < 1
                || child.Key != id.ToString(CultureInfo.InvariantCulture)) { _invalid = true; continue; }
            var malformed = child.Value is not null || child.GetChildren().Any(p =>
                p.Key is not ("Nit" or "Ambiente" or "Profile") || p.GetChildren().Any());
            _entries[id] = new(child["Nit"], child["Ambiente"], child["Profile"], malformed);
        }
    }

    internal Result<bool> Resolve(int empresaId, string? nit, string? ambiente)
    {
        if (_invalid) return Invalid();
        if (!_entries.TryGetValue(empresaId, out var entry)) return Result<bool>.Ok(_globalDefault);
        var normalized = NormalizeNit(nit);
        if (entry.Invalid || normalized is null || normalized != NormalizeNit(entry.Nit)
            || !DteAmbientes.EsValido(ambiente) || entry.Ambiente != ambiente || entry.Profile != ModernProfile)
            return Invalid();
        return Result<bool>.Ok(true);
    }

    private static string? NormalizeNit(string? value)
    {
        var nit = value?.Replace("-", "").Trim();
        return nit is { Length: 14 } && nit.All(c => c is >= '0' and <= '9') ? nit : null;
    }

    private static Result<bool> Invalid() => Result<bool>.Fail(
        "La política operacional de esquema DTE no coincide con la identidad fiscal de la empresa. Contacte a soporte.",
        "DTE_SCHEMA_POLICY_INVALID");
}
