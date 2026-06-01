using System.Globalization;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using NeoSTP.Application.Catalogos.Dtos;

namespace NeoSTP.Infrastructure.Services.Catalogos;

internal static class CatalogoItemExporters
{
    private static readonly string[] HeaderColumns =
    {
        "Codigo", "Valor", "Descripcion", "Orden", "ParentCodigo", "MetadataJson", "Activo",
    };

    public static byte[] ToCsv(IEnumerable<CatalogoItemDto> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", HeaderColumns));
        foreach (var i in items)
        {
            sb.Append(Csv(i.Codigo)).Append(',');
            sb.Append(Csv(i.Valor)).Append(',');
            sb.Append(Csv(i.Descripcion)).Append(',');
            sb.Append(i.Orden.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(Csv(i.ParentCodigo)).Append(',');
            sb.Append(Csv(i.MetadataJson)).Append(',');
            sb.Append(i.Activo ? "true" : "false");
            sb.AppendLine();
        }
        // UTF8 con BOM para que Excel reconozca acentos.
        return new UTF8Encoding(true).GetBytes(sb.ToString());
    }

    public static byte[] ToJson(IEnumerable<CatalogoItemDto> items)
    {
        var payload = items.Select(i => new
        {
            codigo = i.Codigo,
            valor = i.Valor,
            descripcion = i.Descripcion,
            orden = i.Orden,
            parentCodigo = i.ParentCodigo,
            metadataJson = i.MetadataJson,
            activo = i.Activo,
        });
        var json = JsonSerializer.SerializeToUtf8Bytes(payload, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        });
        return json;
    }

    public static byte[] ToXlsx(IEnumerable<CatalogoItemDto> items, string sheetName)
    {
        using var wb = new XLWorkbook();
        var name = (sheetName ?? "Items").Trim();
        if (name.Length > 31) name = name[..31];
        var ws = wb.AddWorksheet(string.IsNullOrEmpty(name) ? "Items" : name);

        for (var c = 0; c < HeaderColumns.Length; c++)
        {
            ws.Cell(1, c + 1).Value = HeaderColumns[c];
            ws.Cell(1, c + 1).Style.Font.Bold = true;
        }

        var row = 2;
        foreach (var i in items)
        {
            ws.Cell(row, 1).Value = i.Codigo;
            ws.Cell(row, 2).Value = i.Valor;
            ws.Cell(row, 3).Value = i.Descripcion;
            ws.Cell(row, 4).Value = i.Orden;
            ws.Cell(row, 5).Value = i.ParentCodigo;
            ws.Cell(row, 6).Value = i.MetadataJson;
            ws.Cell(row, 7).Value = i.Activo;
            row++;
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static string Csv(string? raw)
    {
        if (raw is null) return string.Empty;
        var needsQuotes = raw.Contains(',') || raw.Contains('"') || raw.Contains('\n') || raw.Contains('\r');
        var escaped = raw.Replace("\"", "\"\"");
        return needsQuotes ? $"\"{escaped}\"" : escaped;
    }
}
