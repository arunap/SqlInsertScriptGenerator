using System.Data;
using System.Globalization;
using System.Text;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using SqlInsertScriptGenerator;

internal class Program
{
    private static readonly string _settingFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
    private static readonly string _scriptFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DataScripts");
    private static bool _truncateBeforeInsert;

    private static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");

        // Load settings from JSON file
        if (!File.Exists(_settingFilePath))
        {
            Console.WriteLine("Settings file not found. Please create a settings.json file.");
            return;
        }

        // Deserialize settings from JSON file
        var settingsJson = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(_settingFilePath));
        ArgumentNullException.ThrowIfNull(settingsJson, nameof(settingsJson));

        _truncateBeforeInsert = settingsJson.TruncateBeforeInsert;

        if (Directory.Exists(_scriptFolderPath))
            Directory.Delete(_scriptFolderPath, true);

        Directory.CreateDirectory(_scriptFolderPath);

        int scriptIndex = 1;
        foreach (var tableName in settingsJson.TableNameMap.Keys)
        {
            Console.WriteLine($"Loading table: {tableName}");
            var dataTable = LoadTable(settingsJson.ConnectionString, tableName);
            Console.WriteLine($"Loaded {dataTable.Rows.Count} rows from {tableName}.");

            string scriptString = GenerateInsertScript(dataTable, tableName);

            Console.WriteLine("Generating insert scripts...");

            string formattedScriptName = $"{scriptIndex.ToString("D2")}_{tableName}_INSERT.sql";
            File.WriteAllText(Path.Combine(_scriptFolderPath, formattedScriptName), scriptString);

            Console.WriteLine($"{formattedScriptName} - scripts generated successfully.");
            scriptIndex++;
        }

        Console.ReadLine();
    }

    public static DataTable LoadTable(string connectionString, string tableName)
    {
        using var connection = new SqlConnection(connectionString);
        var query = $"SELECT * FROM {tableName}";

        using var adapter = new SqlDataAdapter(query, connection);
        var dataTable = new DataTable { TableName = tableName };

        adapter.Fill(dataTable);

        return dataTable;
    }

    public static string GenerateInsertScript(DataTable table, string tableName)
    {
        if (table == null || table.Rows.Count == 0) return string.Empty;

        var sb = new StringBuilder();

        if (_truncateBeforeInsert)
        {
            sb.AppendLine("");
            sb.AppendLine($"TRUNCATE TABLE {tableName};");
            sb.AppendLine("GO");
            sb.AppendLine("");
        }

        foreach (DataRow row in table.Rows)
        {
            var columnNames = new List<string>();
            var values = new List<string>();

            foreach (DataColumn column in table.Columns)
            {
                columnNames.Add($"[{column.ColumnName}]");

                var value = row[column];
                values.Add(FormatSqlValue(value, column.DataType));
            }

            sb.AppendLine($"INSERT INTO {tableName} ({string.Join(", ", columnNames)}) VALUES ({string.Join(", ", values)});");
            sb.AppendLine("GO");
        }

        return sb.ToString();
    }

    private static string FormatSqlValue(object value, Type type)
    {
        if (value == DBNull.Value)
            return "NULL";

        if (type == typeof(string) || type == typeof(char))
            return $"'{value.ToString()?.Replace("'", "''")}'";

        if (type == typeof(DateTime))
            return $"'{((DateTime)value).ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)}'";

        if (type == typeof(bool))
            return ((bool)value) ? "1" : "0";

        if (type == typeof(decimal) || type == typeof(float) || type == typeof(double))
            return Convert.ToDouble(value).ToString(CultureInfo.InvariantCulture);

        if (type == typeof(Guid) || type == typeof(Guid?))
            return $"'{value}'";

        if (type == typeof(byte[]))
        {
            var bytes = (byte[])value;
            if (bytes.Length == 0)
                return "0x"; // empty binary
            return "0x" + BitConverter.ToString(bytes).Replace("-", "");
        }

        return value.ToString() ?? string.Empty;
    }
}