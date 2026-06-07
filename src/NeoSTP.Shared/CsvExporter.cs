using System.Text;

namespace NeoSTP.Shared;

/// <summary>
/// Generador de CSV reutilizable (M7.3) con escape RFC 4180 y BOM UTF-8 para Excel.
/// Uso: new CsvExporter(headers...).AddRow(...).ToBytes().
/// </summary>
public sealed class CsvExporter
{
    private readonly StringBuilder _sb = new();
    private readonly int _columnas;

    public CsvExporter(params string[] headers)
    {
        _columnas = headers.Length;
        AppendLine(headers);
    }

    /// <summary>Agrega una fila. Cada valor se escapa; null/empty quedan como celda vacía.</summary>
    public CsvExporter AddRow(params object?[] values)
    {
        AppendLine(values.Select(v => v?.ToString()).ToArray());
        return this;
    }

    public string ToCsv() => _sb.ToString();

    /// <summary>Bytes con BOM UTF-8 (para que Excel detecte la codificación).</summary>
    public byte[] ToBytes()
    {
        var enc = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        var preamble = enc.GetPreamble();          // GetBytes NO antepone el BOM; lo agregamos.
        var content = enc.GetBytes(_sb.ToString());
        var result = new byte[preamble.Length + content.Length];
        Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length);
        Buffer.BlockCopy(content, 0, result, preamble.Length, content.Length);
        return result;
    }

    private void AppendLine(string?[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (i > 0) _sb.Append(',');
            _sb.Append(Field(values[i]));
        }
        _sb.Append("\r\n"); // CRLF por RFC 4180
    }

    /// <summary>Escapa un campo: entre comillas si contiene coma, comilla o salto; duplica comillas.</summary>
    public static string Field(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var needsQuoting = value.IndexOfAny([',', '"', '\n', '\r']) >= 0;
        if (!needsQuoting) return value;
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
