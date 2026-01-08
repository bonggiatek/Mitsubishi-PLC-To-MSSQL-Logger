using Microsoft.Data.SqlClient;
using PLCDataLogger.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PLCDataLogger.Services
{
    public class DatabaseService
    {
        private static readonly Lazy<DatabaseService> _instance = new(() => new DatabaseService());
        public static DatabaseService Instance => _instance.Value;

        private DatabaseService() { }

        /// <summary>
        /// Executes SQL command (INSERT/UPDATE) for a register value.
        /// Supports custom queries or uses default templates based on configuration.
        /// Uses parameters: @FieldName, @RegisterAddress, @Value, @Timestamp, @Description, @Unit
        /// Additionally supports: @Reg_{FieldName} for accessing other configured register values
        /// </summary>
        public async Task<bool> InsertRegisterValueAsync(
            string connectionString,
            string tableName,
            RegisterMapping mapping,
            string value,
            DateTime timestamp,
            Dictionary<string, string> allRegisterValues = null)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                LoggingService.Instance.LogWarning("Connection string is empty, skipping database operation");
                return false;
            }

            try
            {
                string query;

                // Use custom query if specified, otherwise use default INSERT template
                if (mapping.Sql != null && mapping.Sql.UseCustomQuery && !string.IsNullOrWhiteSpace(mapping.Sql.CustomQuery))
                {
                    query = mapping.Sql.CustomQuery;
                }
                else
                {
                    // Default to INSERT template
                    query = Helpers.SqlQueryTemplates.GetInsertTemplate(tableName);
                }

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    using (var command = new SqlCommand(query, connection))
                    {
                        // Add all standard parameters
                        command.Parameters.AddWithValue("@FieldName", mapping.FieldName ?? string.Empty);
                        command.Parameters.AddWithValue("@RegisterAddress", mapping.RegisterAddress ?? string.Empty);
                        command.Parameters.AddWithValue("@Value", value ?? string.Empty);
                        command.Parameters.AddWithValue("@Timestamp", timestamp);
                        command.Parameters.AddWithValue("@Description", mapping.Description ?? string.Empty);
                        command.Parameters.AddWithValue("@Unit", mapping.Unit ?? string.Empty);

                        // Add parameters for all other register values (if provided)
                        // This allows SQL queries to access other configured register values
                        // using @Reg_{FieldName} syntax (e.g., @Reg_Temperature, @Reg_Pressure)
                        if (allRegisterValues != null)
                        {
                            foreach (var kvp in allRegisterValues)
                            {
                                string paramName = $"@Reg_{kvp.Key}";
                                command.Parameters.AddWithValue(paramName, kvp.Value ?? string.Empty);
                            }
                        }

                        // Log SQL query before execution
                        LoggingService.Instance.LogInfo($"Executing SQL: {query}");

                        // Build parameter log string
                        var paramLog = $"Parameters: FieldName='{mapping.FieldName}', RegisterAddress='{mapping.RegisterAddress}', Value='{value}', Timestamp='{timestamp:yyyy-MM-dd HH:mm:ss}', Description='{mapping.Description}', Unit='{mapping.Unit}'";
                        if (allRegisterValues != null && allRegisterValues.Count > 0)
                        {
                            paramLog += $", OtherRegisters={string.Join(", ", allRegisterValues.Select(kvp => $"{kvp.Key}={kvp.Value}"))}";
                        }
                        LoggingService.Instance.LogInfo(paramLog);

                        int rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            LoggingService.Instance.LogInfo($"Logged {mapping.FieldName} = {value} to database ({rowsAffected} rows affected)");
                            return true;
                        }
                        else
                        {
                            LoggingService.Instance.LogWarning($"SQL operation failed for {mapping.FieldName}, no rows affected");
                            return false;
                        }
                    }
                }
            }
            catch (SqlException sqlEx)
            {
                LoggingService.Instance.LogError(sqlEx, $"SQL error for {mapping.FieldName}: {sqlEx.Message}");
                return false;
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError(ex, $"Error executing SQL for {mapping.FieldName}");
                return false;
            }
        }

        /// <summary>
        /// Checks if the table exists in the database
        /// </summary>
        public async Task<bool> EnsureTableExistsAsync(string connectionString, string tableName)
        {
            if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(tableName))
                return false;

            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // Check if table exists using OBJECT_ID
                    string checkTableQuery = "SELECT OBJECT_ID(@TableName, 'U')";

                    using (var checkCommand = new SqlCommand(checkTableQuery, connection))
                    {
                        checkCommand.Parameters.AddWithValue("@TableName", tableName);

                        // Log SQL query before execution
                        LoggingService.Instance.LogInfo($"Executing SQL: {checkTableQuery}");
                        LoggingService.Instance.LogInfo($"Parameters: TableName='{tableName}'");

                        var result = await checkCommand.ExecuteScalarAsync();

                        if (result == null || result == DBNull.Value)
                        {
                            LoggingService.Instance.LogError(null, $"Table '{tableName}' does not exist in database");
                            return false;
                        }
                        else
                        {
                            LoggingService.Instance.LogInfo($"Table '{tableName}' verified in database");
                            return true;
                        }
                    }
                }
            }
            catch (SqlException sqlEx)
            {
                LoggingService.Instance.LogError(sqlEx, $"SQL error checking table existence: {sqlEx.Message}");
                return false;
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError(ex, "Error checking table existence");
                return false;
            }
        }

        /// <summary>
        /// Tests database connectivity
        /// </summary>
        public async Task<bool> TestConnectionAsync(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return false;

            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    LoggingService.Instance.LogInfo("Database connection test successful");
                    return true;
                }
            }
            catch (SqlException sqlEx)
            {
                LoggingService.Instance.LogError(sqlEx, $"SQL connection test failed: {sqlEx.Message}");
                return false;
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError(ex, "Database connection test failed");
                return false;
            }
        }
    }
}
