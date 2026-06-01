using System.Globalization;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using NeoSTP.Application.Catalogos.Dtos;

namespace NeoSTP.Infrastructure.Services.Catalogos;

/// <summary>
/// Parsers de archivos de import (CSV / JSON / XLSX) hacia <see cref="CatalogoItemRow"/>.
/// Internos de Infrastructure — visibles a los tests vía InternalsVisibleTo.
/// </summary>
internal static class CatalogoItemParsers
{
    private static readonly string[] HeaderColumns =
    {
        "codigo", "valor", "descripcion", "orden", "parentcodigo", "metadatajson", "activo",
    };

    public static IReadOnlyList<CatalogoItemRow> ParseCsv(Stream content)
    {
        using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var rows = new List<CatalogoItemRow>();

        var headerLine = ReadLogicalLine(reader);
        if (headerLine is null) return rows;

        var headers = ParseCsvLine(headerLine)
            .Select(h => h.Trim().ToLowerInvariant())
            .ToList();

        var idx = HeaderColumns.ToDictionary(c => c, c => headers.IndexOf(c));
        // Mínimo: codigo + valor.
        if (idx["codigo"] < 0 || idx["valor"] < 0)
        {
            throw new FormatException("El CSV debe incluir al menos las columnas 'Codigo' y 'Valor' en el encabezado.");
        }

        var rowNumber = 1; // contamos header como fila 1
        string? line;
        while ((line = ReadLogicalLine(reader)) is not null)
        {
            rowNumber++;
            if (string.IsNullOrWhiteSpace(line)) continue;

            var cells = ParseCsvLine(line);
            rows.Add(new CatalogoItemRow
            {
                RowNumber = rowNumber,
                Codigo = Field(cells, idx["codigo"]),
                Valor = Field(cells, idx["valor"]),
                Descripcion = Field(cells, idx["descripcion"]),
                Orden = ParseInt(Field(cells, idx["orden"])),
                ParentCodigo = Field(cells, idx["parentcodigo"]),
                MetadataJson = Field(cells, idx["metadatajson"]),
                Activo = ParseBool(Field(cells, idx["activo"])),
            });
        }
        return rows;
    }

    public static IReadOnlyList<CatalogoItemRow> ParseJson(Stream content)
    {
        var rows = new List<CatalogoItemRow>();
        using var doc = JsonDocument.Parse(content);

        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("El JSON debe ser un arreglo de objetos.");
        }

        var rowNumber = 0;
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            rowNumber++;
            if (el.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException($"Elemento {rowNumber} no es un objeto JSON.");
            }

            rows.Add(new CatalogoItemRow
            {
                RowNumber = rowNumber,
                Codigo = ReadString(el, "codigo"),
                Valor = ReadString(el, "valor"),
                Descripcion = ReadString(el, "descripcion"),
                Orden = ReadInt(el, "orden"),
                ParentCodigo = ReadString(el, "parentCodigo") ?? ReadString(el, "parentcodigo"),
                MetadataJson = ReadMetadataJson(el),
                Activo = ReadBool(el, "activo"),
            });
        }
        return rows;
    }

    public static IReadOnlyList<CatalogoItemRow> ParseXlsx(Stream content)
    {
        var rows = new List<CatalogoItemRow>();
        using var wb = new XLWorkbook(content);
        var ws = wb.Worksheets.FirstOrDefault()
            ?? throw new FormatException("El archivo Excel no contiene hojas.");

        var header = ws.FirstRowUsed();
        if (header is null) return rows;

        var headers = header.Cells().Select(c => (c.GetString() ?? string.Empty).Trim().ToLowerInvariant()).ToList();
        var idx = HeaderColumns.ToDictionary(c => c, c => headers.IndexOf(c));
        if (idx["codigo"] < 0 || idx["valor"] < 0)
        {
            throw new FormatException("El Excel debe incluir al menos las columnas 'Codigo' y 'Valor' en la primera fila.");
        }

        foreach (var row in ws.RowsUsed().Skip(1))
        {
            var rowNumber = row.RowNumber();
            var cells = row.Cells(1, headers.Count).Select(c => c.GetString()?.Trim() ?? string.Empty).ToList();
            if (cells.All(string.IsNullOrEmpty)) continue;

            rows.Add(new CatalogoItemRow
            {
                RowNumber = rowNumber,
                Codigo = Field(cells, idx["codigo"]),
                Valor = Field(cells, idx["valor"]),
                Descripcion = Field(cells, idx["descripcion"]),
                Orden = ParseInt(Field(cells, idx["orden"])),
                ParentCodigo = Field(cells, idx["parentcodigo"]),
                MetadataJson = Field(cells, idx["metadatajson"]),
                Activo = ParseBool(Field(cells, idx["activo"])),
            });
        }
        return rows;
    }

    // ----------------------------------------------------- Helpers CSV crudos

    private static string? ReadLogicalLine(TextReader reader)
    {
        // Lee una línea respetando comillas dobles que pueden envolver saltos de línea.
        var first = reader.Read();
        if (first == -1) return null;

        var sb = new StringBuilder();
        var inQuotes = false;
        var ch = first;
        while (ch != -1)
        {
            if (ch == '"') inQuotes = !inQuotes;
            if (!inQuotes && (ch == '\r' || ch == '\n'))
            {
                if (ch == '\r' && reader.Peek() == '\n') reader.Read();
                break;
            }
            sb.Append((char)ch);
            ch = reader.Read();
        }
        return sb.ToString();
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"'); i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (ch == ',' && !inQuotes)
            {
                result.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(ch);
            }
        }
        result.Add(sb.ToString());
        return result;
    }

    private static string? Field(IReadOnlyList<string> cells, int idx)
    {
        if (idx < 0 || idx >= cells.Count) return null;
        var v = cells[idx]?.Trim();
        return string.IsNullOrEmpty(v) ? null : v;
    }

    private static int? ParseInt(string? raw)
        => int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;

    private static bool? ParseBool(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return raw.Trim().ToLowerInvariant() switch
        {
            "true" or "1" or "si" or "sí" or "yes" or "y" => true,
            "false" or "0" or "no" or "n" => false,
            _ => null,
        };
    }

    private static string? ReadString(JsonElement el, string name)
        => el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? ReadInt(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var v)) return null;
        return v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) ? n : null;
    }

    private static bool? ReadBool(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    private static string? ReadMetadataJson(JsonElement el)
    {
        if (!el.TryGetProperty("metadataJson", out var v) && !el.TryGetProperty("metadatajson", out v))
        {
            return null;
        }
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Object or JsonValueKind.Array => v.GetRawText(),
            _ => null,
        };
    }
}
