using Microsoft.Data.SqlClient;

namespace ClientCertificationSchemaUpgrade;

internal sealed record OriginalTable(string Schema, string Name, string[] Columns, string[] Keys);
internal sealed record TableDigest(string Schema, string Name, long Rows, string? Sha256);
internal static class SqlEvidence
{
    internal static string Quote(string identifier) => "[" + identifier.Replace("]", "]]") + "]";
    internal static async Task<OriginalTable[]> Definitions(SqlConnection sql)
    {
        await using var command = sql.CreateCommand();
        command.CommandText = """
            SELECT SCHEMA_NAME(t.schema_id),t.name,c.name,
              ISNULL((SELECT TOP(1) ic.key_ordinal FROM sys.indexes i JOIN sys.index_columns ic ON i.object_id=ic.object_id AND i.index_id=ic.index_id
                WHERE i.object_id=t.object_id AND i.is_primary_key=1 AND ic.column_id=c.column_id),0)
            FROM sys.tables t JOIN sys.columns c ON c.object_id=t.object_id
            WHERE t.is_ms_shipped=0 AND t.name<>'__EFMigrationsHistory' AND c.system_type_id<>189
            ORDER BY SCHEMA_NAME(t.schema_id),t.name,c.column_id
            """;
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<(string Schema, string Table, string Column, int Key)>();
        while (await reader.ReadAsync()) rows.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2), Convert.ToInt32(reader.GetValue(3))));
        var tables = rows.GroupBy(x => (x.Schema, x.Table)).Select(g => new OriginalTable(g.Key.Schema, g.Key.Table,
            g.Select(x => x.Column).ToArray(), g.Where(x => x.Key > 0).OrderBy(x => x.Key).Select(x => x.Column).ToArray())).ToArray();
        UpgradePolicy.Require(tables.Length > 0 && tables.All(x => x.Keys.Length > 0), "STABLE_PRIMARY_KEYS_REQUIRED_FOR_ALL_ORIGINAL_TABLES");
        return tables;
    }
    internal static async Task<TableDigest[]> Fingerprints(SqlConnection sql, OriginalTable[] definitions)
    {
        var result = new List<TableDigest>();
        foreach (var table in definitions)
        {
            var full = Quote(table.Schema) + "." + Quote(table.Name);
            var columns = string.Join(',', table.Columns.Select(Quote)); var order = string.Join(',', table.Keys.Select(Quote));
            await using var command = sql.CreateCommand(); command.CommandTimeout = 120;
            command.CommandText = $"SELECT COUNT_BIG(*),CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT {columns} FROM {full} ORDER BY {order} FOR JSON PATH,INCLUDE_NULL_VALUES)),2) FROM {full}";
            await using var reader = await command.ExecuteReaderAsync(); await reader.ReadAsync();
            result.Add(new(table.Schema, table.Name, reader.GetInt64(0), reader.IsDBNull(1) ? null : reader.GetString(1)));
        }
        return result.ToArray();
    }
    internal static async Task<int> Integrity(SqlConnection sql)
    {
        await using var command = sql.CreateCommand(); command.CommandTimeout = 180;
        command.CommandText = "DBCC CHECKDB ([NeoSTP_Cloud]) WITH TABLERESULTS, NO_INFOMSGS, ALL_ERRORMSGS";
        await using var reader = await command.ExecuteReaderAsync(); var errors = 0;
        do
        {
            var level = Enumerable.Range(0, reader.FieldCount).FirstOrDefault(i => reader.GetName(i).Equals("Level", StringComparison.OrdinalIgnoreCase), -1);
            while (await reader.ReadAsync()) if (level < 0 || reader.IsDBNull(level) || Convert.ToInt32(reader.GetValue(level)) >= 11) errors++;
        } while (await reader.NextResultAsync());
        return errors;
    }
}
