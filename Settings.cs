namespace SqlInsertScriptGenerator
{
    public class Settings
    {
        public string ConnectionString { get; set; } = string.Empty;
        public Dictionary<string, string> TableNameMap { get; set; } = [];
    }
}