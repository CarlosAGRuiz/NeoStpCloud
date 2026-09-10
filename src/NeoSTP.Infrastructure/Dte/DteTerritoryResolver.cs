using System.Globalization;
using System.Text;
using System.Text.Json;
using NeoSTP.Application.Common;
using NeoSTP.Application.Lookups;

namespace NeoSTP.Infrastructure.Dte;

public sealed record DteTerritory(string? Department, string? Municipality, string? District);

/// <summary>Resolves one fiscal address within its catalog parent chain, without geographic defaults or version guesses.</summary>
public static class DteTerritoryResolver
{
    public static Result<DteTerritory> Resolve(string? department, string? municipality, string? district,
        IReadOnlyList<LookupItem> departments, IReadOnlyList<LookupItem> municipalities,
        IReadOnlyList<LookupItem> districts, bool requireDistrict)
    {
        var dep = Find(departments, department);
        if (dep is null || Code(dep) is not { } departmentCode)
            return Fail("DEPARTAMENTO");
        var children = municipalities.Where(x => ParentMatches(x.Parent, dep)).ToArray();
        var mun = Find(children, municipality);
        // Historical numeric addresses without a district are retained for legacy schemas.
        // Absence from the new municipality catalog does not establish the old code's invalidity.
        if (mun is null && !requireDistrict && string.IsNullOrWhiteSpace(district) && IsCode(municipality)
            && !municipalities.Any(x => Matches(x, municipality)))
            return Result<DteTerritory>.Ok(new(departmentCode, municipality, null));
        if (mun is null || Code(mun) is not { } municipalityCode) return Fail("MUNICIPIO");
        if (string.IsNullOrWhiteSpace(district))
            return requireDistrict
                ? Result<DteTerritory>.Fail("Falta seleccionar el distrito en la ficha del cliente o en la dirección del emisor, según el origen indicado. Escribirlo en el texto de dirección no completa ese campo.", "DTE_TERRITORIO_DISTRITO")
                : Result<DteTerritory>.Ok(new(departmentCode, municipalityCode, null));
        var districtItem = Find(districts.Where(x => ParentMatches(x.Parent, mun)).ToArray(), district);
        if (districtItem is null || Code(districtItem) is not { } districtCode) return Fail("DISTRITO");
        return Result<DteTerritory>.Ok(new(departmentCode, municipalityCode, districtCode));
    }

    private static Result<DteTerritory> Fail(string field)
        => Result<DteTerritory>.Fail("La dirección fiscal requiere un código y una relación de catálogo verificables. Revise departamento, municipio y distrito.", "DTE_TERRITORIO_" + field);
    private static bool IsCode(string? value) => value is { Length: 2 } && value.All(char.IsAsciiDigit);
    private static string? Code(LookupItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.Meta))
        {
            try
            {
                using var json = JsonDocument.Parse(item.Meta);
                if (json.RootElement.ValueKind != JsonValueKind.Object) return null;
                if (json.RootElement.TryGetProperty("codigoMH", out var code) && code.ValueKind == JsonValueKind.String)
                    return IsCode(code.GetString()) ? code.GetString() : null;
            }
            catch (JsonException) { return null; }
        }
        return IsCode(item.Value) ? item.Value : null;
    }
    private static bool ParentMatches(string? parent, LookupItem item)
        => !string.IsNullOrWhiteSpace(parent) && Normalize(parent) == Normalize(item.Value);
    private static LookupItem? Find(IReadOnlyList<LookupItem> items, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var matches = items.Where(x => Matches(x, value)).Take(2).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }
    private static bool Matches(LookupItem item, string? value)
        => Normalize(item.Value) == Normalize(value) || Normalize(item.Label) == Normalize(value)
            || (IsCode(value) && Code(item) == value);
    private static string Normalize(string? value)
    {
        var normalized = (value ?? "").Trim().Replace('_', ' ').Replace('-', ' ').Normalize(NormalizationForm.FormD);
        var stripped = new string(normalized.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        return string.Join(' ', stripped.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
    }
}
