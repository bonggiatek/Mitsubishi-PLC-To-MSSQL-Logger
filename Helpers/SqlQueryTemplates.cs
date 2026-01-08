using PLCDataLogger.Models;
using System.Collections.Generic;

namespace PLCDataLogger.Helpers
{
    public static class SqlQueryTemplates
    {
        /// <summary>
        /// Gets predefined INSERT query template based on table name
        /// Available placeholders: {FieldName}, {RegisterAddress}, {Value}, {Timestamp}, {Description}, {Unit}
        /// </summary>
        public static string GetInsertTemplate(string tableName)
        {
            return $@"INSERT INTO {tableName}
(FieldName, RegisterAddress, Value, Timestamp, Description, Unit)
VALUES
(@FieldName, @RegisterAddress, @Value, @Timestamp, @Description, @Unit)";
        }

        /// <summary>
        /// Gets predefined UPDATE query template based on table name
        /// Updates the most recent record for the given FieldName
        /// Available placeholders: {FieldName}, {RegisterAddress}, {Value}, {Timestamp}, {Description}, {Unit}
        /// </summary>
        public static string GetUpdateTemplate(string tableName)
        {
            return $@"UPDATE {tableName}
SET
    Value = @Value,
    Timestamp = @Timestamp,
    Description = @Description,
    Unit = @Unit
WHERE Id = (
    SELECT TOP 1 Id
    FROM {tableName}
    WHERE FieldName = @FieldName
    ORDER BY Timestamp DESC
)";
        }

        /// <summary>
        /// Gets predefined UPSERT (INSERT or UPDATE) query template
        /// Inserts if no record exists for FieldName, otherwise updates
        /// </summary>
        public static string GetUpsertTemplate(string tableName)
        {
            return $@"IF EXISTS (SELECT 1 FROM {tableName} WHERE FieldName = @FieldName)
BEGIN
    UPDATE {tableName}
    SET
        Value = @Value,
        Timestamp = @Timestamp,
        RegisterAddress = @RegisterAddress,
        Description = @Description,
        Unit = @Unit
    WHERE FieldName = @FieldName
END
ELSE
BEGIN
    INSERT INTO {tableName}
    (FieldName, RegisterAddress, Value, Timestamp, Description, Unit)
    VALUES
    (@FieldName, @RegisterAddress, @Value, @Timestamp, @Description, @Unit)
END";
        }

        /// <summary>
        /// Gets all available INSERT templates with descriptions
        /// </summary>
        public static Dictionary<string, string> GetInsertTemplates(string tableName)
        {
            return new Dictionary<string, string>
            {
                ["Standard Insert"] = GetInsertTemplate(tableName),
                ["Insert with Current Time"] = $@"INSERT INTO {tableName}
(FieldName, RegisterAddress, Value, Timestamp, Description, Unit)
VALUES
(@FieldName, @RegisterAddress, @Value, GETDATE(), @Description, @Unit)",
                ["Insert with NULL checks"] = $@"INSERT INTO {tableName}
(FieldName, RegisterAddress, Value, Timestamp, Description, Unit)
VALUES
(ISNULL(@FieldName, 'Unknown'), @RegisterAddress, @Value, @Timestamp, @Description, @Unit)",
                ["Insert with Other Register Values"] = $@"INSERT INTO {tableName}
(FieldName, RegisterAddress, Value, Timestamp, Temperature, Pressure, FlowRate)
VALUES
(@FieldName, @RegisterAddress, @Value, @Timestamp, @Reg_Temperature, @Reg_Pressure, @Reg_FlowRate)"
            };
        }

        /// <summary>
        /// Gets all available UPDATE templates with descriptions
        /// </summary>
        public static Dictionary<string, string> GetUpdateTemplates(string tableName)
        {
            return new Dictionary<string, string>
            {
                ["Update Latest Record"] = GetUpdateTemplate(tableName),
                ["Update by FieldName"] = $@"UPDATE {tableName}
SET
    Value = @Value,
    Timestamp = @Timestamp
WHERE FieldName = @FieldName",
                ["Update by RegisterAddress"] = $@"UPDATE {tableName}
SET
    Value = @Value,
    Timestamp = @Timestamp
WHERE RegisterAddress = @RegisterAddress",
                ["Upsert (Insert or Update)"] = GetUpsertTemplate(tableName)
            };
        }

        /// <summary>
        /// Gets all available SQL query templates (INSERT and UPDATE combined)
        /// </summary>
        public static Dictionary<string, string> GetAllTemplates(string tableName)
        {
            var allTemplates = new Dictionary<string, string>();

            // Add INSERT templates with prefix
            foreach (var kvp in GetInsertTemplates(tableName))
            {
                allTemplates[$"[INSERT] {kvp.Key}"] = kvp.Value;
            }

            // Add UPDATE templates with prefix
            foreach (var kvp in GetUpdateTemplates(tableName))
            {
                allTemplates[$"[UPDATE] {kvp.Key}"] = kvp.Value;
            }

            return allTemplates;
        }

        /// <summary>
        /// Gets description of available query placeholders
        /// </summary>
        public static string GetPlaceholderHelp()
        {
            return @"Available Placeholders:

Current Register:
@FieldName - The field name from configuration (e.g., 'T1')
@RegisterAddress - The PLC register address (e.g., 'D3115.1')
@Value - The current value read from PLC
@Timestamp - DateTime when value was read
@Description - Description from configuration
@Unit - Unit of measurement from configuration

Other Configured Registers:
@Reg_{FieldName} - Access any other register's value by its FieldName
Examples: @Reg_Temperature, @Reg_Pressure, @Reg_FlowRate
Note: Replace {FieldName} with the actual field name from your configuration

Example Custom Queries:

1. Simple Insert:
INSERT INTO MyTable (Field, Val, Time)
VALUES (@FieldName, @Value, @Timestamp)

2. Insert with Other Register Values:
INSERT INTO SensorData (Timestamp, Temperature, Pressure, Status)
VALUES (@Timestamp, @Reg_Temperature, @Reg_Pressure, @Value)

3. Conditional Insert (only if temperature is high):
IF CAST(@Reg_Temperature AS FLOAT) > 80
BEGIN
    INSERT INTO Alerts (Timestamp, AlertType, Value, Temperature)
    VALUES (@Timestamp, @FieldName, @Value, @Reg_Temperature)
END";
        }
    }
}
