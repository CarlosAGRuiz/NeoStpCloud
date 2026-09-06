using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Common;
using NeoSTP.Application.Dte.Dtos;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Dte;

/// <summary>Autorización local por empresa. No consulta MH ni convierte el catálogo global en permisos.</summary>
internal static class DteTypeAuthorization
{
    internal static bool CatalogItemAllowed(string? metadataJson, IReadOnlyList<TipoDteDisponibleDto> allowed)
    {
        try
        {
            using var json = System.Text.Json.JsonDocument.Parse(metadataJson ?? "null");
            if (json.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object) return false;
            var properties = json.RootElement.EnumerateObject().Where(p => p.Name == "codigoMH").ToArray();
            return properties.Length == 1 && properties[0].Value.ValueKind == System.Text.Json.JsonValueKind.String
                && allowed.Any(t => t.Codigo == properties[0].Value.GetString());
        }
        catch (System.Text.Json.JsonException) { return false; }
    }
    private static readonly TipoDteDisponibleDto[] Supported =
    [
        new("01", "Factura"), new("03", "Crédito fiscal"), new("04", "Nota de remisión"),
        new("05", "Nota de crédito"), new("06", "Nota de débito"), new("07", "Retención"),
        new("08", "Liquidación"), new("09", "Documento contable de liquidación"),
        new("11", "Factura de exportación"), new("14", "Sujeto excluido"), new("15", "Donación")
    ];

    internal static IReadOnlyList<TipoDteDisponibleDto> Resolve(string? csv)
    {
        if (csv is null) return Supported.ToArray();
        var codes = csv.Split(',');
        // A malformed restriction must never widen access or silently discard invalid entries.
        if (csv.Length > 100 || codes.Distinct(StringComparer.Ordinal).Count() != codes.Length
            || codes.Any(c => !Supported.Any(s => s.Codigo == c))) return [];
        return Supported.Where(s => codes.Contains(s.Codigo, StringComparer.Ordinal)).ToArray();
    }

    internal static async Task<IReadOnlyList<TipoDteDisponibleDto>> ReadAsync(NeoStpDbContext db, int empresaId, CancellationToken ct)
    {
        var config = await db.DteConfiguracion.AsNoTracking()
            .Where(c => c.EmpresaId == empresaId)
            .Select(c => new { c.TiposDteAutorizadosCsv }).SingleOrDefaultAsync(ct);
        return config is null ? [] : Resolve(config.TiposDteAutorizadosCsv);
    }

    internal static async Task<Result> ValidateAsync(NeoStpDbContext db, int empresaId, string tipo, CancellationToken ct)
        => (await ReadAsync(db, empresaId, ct)).Any(t => t.Codigo == tipo)
            ? Result.Ok()
            : Result.Fail("El tipo de DTE no está habilitado para esta empresa. Consulte a soporte.", "DTE_TIPO_NO_AUTORIZADO");
}
