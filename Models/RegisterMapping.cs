namespace PLCDataLogger.Models
{
    public class RegisterMapping
    {
        public string FieldName { get; set; } = string.Empty;
        public string RegisterAddress { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DataType DataType { get; set; }
        public int Length { get; set; } = 1;
        public string Unit { get; set; } = string.Empty;

        // SQL Configuration
        public SqlConfig? Sql { get; set; }
    }

    public class SqlConfig
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
        public LogMode LogMode { get; set; } = LogMode.Disabled;
        public int IntervalSeconds { get; set; } = 5;
        public bool UseCustomQuery { get; set; } = false;
        public string CustomQuery { get; set; } = string.Empty;
        public string LogCondition { get; set; } = string.Empty;
    }

    public enum LogMode
    {
        Disabled,
        Interval,
        OnChange,
        Both
    }

    public enum DataType
    {
        INT,
        UINT,
        FLOAT,
        BOOL,
        STRING
    }
}
