using System.Text;
using ClosedXML.Excel;
using NeoSTP.Application.Common;

namespace NeoSTP.Infrastructure.Services;

/// <summary>Fila parseada de un archivo tabular: número de fila + celdas por encabezado (minúsculas).</summary>
public sealed class TabularRow
{
    public int RowNumber { get; init; }
    public IReadOnlyDictionary<string, string> Cells { get; init; } = new Dictionary<string, string>();

    public string? Get(string header)
        => Cells.TryGetValue(header.ToLowerInvariant(), out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;
}

/// <summary>
/// Parser tabular genérico (CSV / XLSX) → filas con celdas indexadas por encabezado.
/// Reutilizable por las cargas masivas (clientes, productos, …).
/// </summary>
public static class TabularParser
{
    public static IReadOnlyList<TabularRow> Parse(Stream content, BulkFileFormat format)
        => format == BulkFileFormat.Csv ? ParseCsv(content) : ParseXlsx(content);

    private static IReadOnlyList<TabularRow> ParseXlsx(Stream content)
    {
        var rows = new List<TabularRow>();
        using var wb = new XLWorkbook(content);
        var ws = wb.Worksheets.FirstOrDefault()
            ?? throw new FormatException("El archivo Excel no contiene hojas.");

        var header = ws.FirstRowUsed();
        if (header is null) return rows;

        var headers = header.Cells().Select(c => (c.GetString() ?? string.Empty).Trim().ToLowerInvariant()).ToList();
        foreach (var row in ws.RowsUsed().Skip(1))
        {
            var cells = row.Cells(1, headers.Count).Select(c => c.GetString()?.Trim() ?? string.Empty).ToList();
            if (cells.All(string.IsNullOrEmpty)) continue;
            rows.Add(new TabularRow { RowNumber = row.RowNumber(), Cells = ToDict(headers, cells) });
        }
        return rows;
    }

    private static IReadOnlyList<TabularRow> ParseCsv(Stream content)
    {
        using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var rows = new List<TabularRow>();

        var headerLine = ReadLogicalLine(reader);
        if (headerLine is null) return rows;
        var headers = ParseCsvLine(headerLine).Select(h => h.Trim().ToLowerInvariant()).ToList();

        var rowNumber = 1;
        string? line;
        while ((line = ReadLogicalLine(reader)) is not null)
        {
            rowNumber++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cells = ParseCsvLine(line);
            rows.Add(new TabularRow { RowNumber = rowNumber, Cells = ToDict(headers, cells) });
        }
        return rows;
    }

    private static Dictionary<string, string> ToDict(List<string> headers, List<string> cells)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Count; i++)
        {
            if (string.IsNullOrEmpty(headers[i])) continue;
            dict[headers[i]] = i < cells.Count ? cells[i] : string.Empty;
        }
        return dict;
    }

    private static string? ReadLogicalLine(TextReader reader)
    {
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
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                else inQuotes = !inQuotes;
            }
            else if (ch == ',' && !inQuotes) { result.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(ch);
        }
        result.Add(sb.ToString());
        return result;
    }
}
