using PLCDataLogger.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PLCDataLogger.Services
{
    /// <summary>
    /// Manages logging for individual registers with interval and change-based triggers
    /// </summary>
    public class RegisterLogger
    {
        private readonly RegisterMapping _mapping;
        private readonly DatabaseService _databaseService;
        private readonly Func<Dictionary<string, string>> _getAllRegisterValues;
        private Timer? _intervalTimer;
        private string _lastLoggedValue = string.Empty;
        private DateTime _lastLoggedTime = DateTime.MinValue;
        private bool _isInitialized = false;
        private string _currentValue = string.Empty;

        public RegisterLogger(RegisterMapping mapping, Func<Dictionary<string, string>> getAllRegisterValues = null)
        {
            _mapping = mapping;
            _databaseService = DatabaseService.Instance;
            _getAllRegisterValues = getAllRegisterValues;
        }

        /// <summary>
        /// Initializes the logger and ensures database table exists
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_isInitialized || _mapping.Sql == null)
                return;

            if (_mapping.Sql.LogMode == LogMode.Disabled)
                return;

            // Ensure database table exists
            await _databaseService.EnsureTableExistsAsync(
                _mapping.Sql.ConnectionString,
                _mapping.Sql.TableName);

            // Start interval timer if needed
            if (_mapping.Sql.LogMode == LogMode.Interval || _mapping.Sql.LogMode == LogMode.Both)
            {
                StartIntervalTimer();
            }

            _isInitialized = true;
            LoggingService.Instance.LogInfo($"Initialized logger for {_mapping.FieldName} with mode {_mapping.Sql.LogMode}");
        }

        /// <summary>
        /// Processes a new value read from the PLC
        /// </summary>
        public async Task ProcessValueAsync(string currentValue)
        {
            if (_mapping.Sql == null || _mapping.Sql.LogMode == LogMode.Disabled)
                return;

            bool shouldLog = false;
            string reason = string.Empty;

            // Check if we should log based on mode
            switch (_mapping.Sql.LogMode)
            {
                case LogMode.OnChange:
                    if (HasValueChanged(currentValue))
                    {
                        shouldLog = true;
                        reason = "value changed";
                    }
                    break;

                case LogMode.Interval:
                    // Interval logging is handled by timer, not here
                    break;

                case LogMode.Both:
                    if (HasValueChanged(currentValue))
                    {
                        shouldLog = true;
                        reason = "value changed";
                    }
                    // Interval logging still handled by timer
                    break;
            }

            if (shouldLog)
            {
                await LogValueAsync(currentValue, reason);
            }
        }

        /// <summary>
        /// Logs the current value to database
        /// </summary>
        private async Task LogValueAsync(string value, string reason)
        {
            if (_mapping.Sql == null)
                return;

            DateTime now = DateTime.Now;

            // Get all register values to pass to SQL query (allows referencing other registers)
            Dictionary<string, string> allRegisterValues = null;
            if (_getAllRegisterValues != null)
            {
                try
                {
                    allRegisterValues = _getAllRegisterValues();
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogWarning($"Failed to get all register values for {_mapping.FieldName}: {ex.Message}");
                }
            }

            // Evaluate log condition (if specified) before executing SQL
            if (!string.IsNullOrWhiteSpace(_mapping.Sql.LogCondition))
            {
                bool conditionPassed = ConditionEvaluator.Evaluate(
                    _mapping.Sql.LogCondition,
                    value,
                    allRegisterValues);

                if (!conditionPassed)
                {
                    LoggingService.Instance.LogInfo($"Skipped logging {_mapping.FieldName}={value} - condition not met: '{_mapping.Sql.LogCondition}'");
                    return; // Don't log if condition fails
                }
            }

            bool success = await _databaseService.InsertRegisterValueAsync(
                _mapping.Sql.ConnectionString,
                _mapping.Sql.TableName,
                _mapping,
                value,
                now,
                allRegisterValues);

            if (success)
            {
                _lastLoggedValue = value;
                _lastLoggedTime = now;
                LoggingService.Instance.LogInfo($"Logged {_mapping.FieldName}={value} ({reason})");
            }
        }

        /// <summary>
        /// Checks if the value has changed since last log
        /// </summary>
        private bool HasValueChanged(string currentValue)
        {
            return currentValue != _lastLoggedValue;
        }

        /// <summary>
        /// Starts the interval-based logging timer
        /// </summary>
        private void StartIntervalTimer()
        {
            if (_mapping.Sql == null)
                return;

            int intervalMs = _mapping.Sql.IntervalSeconds * 1000;
            LoggingService.Instance.LogInfo($"Starting interval timer for {_mapping.FieldName} with {_mapping.Sql.IntervalSeconds} second interval");

            _intervalTimer = new Timer((state) =>
            {
                // Use Task.Run to properly handle async work in timer callback
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Use the current value stored in the field
                        if (!string.IsNullOrEmpty(_currentValue))
                        {
                            await LogValueAsync(_currentValue, "interval");
                        }
                        else
                        {
                            LoggingService.Instance.LogWarning($"Interval timer fired for {_mapping.FieldName} but _currentValue is empty");
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggingService.Instance.LogError(ex, $"Error in interval timer for {_mapping.FieldName}");
                    }
                });
            }, null, intervalMs, intervalMs);
        }

        /// <summary>
        /// Updates the current value that will be used by the interval timer
        /// </summary>
        public void UpdateIntervalTimerState(string currentValue)
        {
            if (_mapping.Sql == null)
                return;

            if (_mapping.Sql.LogMode == LogMode.Interval || _mapping.Sql.LogMode == LogMode.Both)
            {
                // Simply update the current value - the timer will use it on next tick
                _currentValue = currentValue;
            }
        }

        /// <summary>
        /// Stops the logger and cleans up resources
        /// </summary>
        public void Stop()
        {
            _intervalTimer?.Dispose();
            _intervalTimer = null;
            _isInitialized = false;
            LoggingService.Instance.LogInfo($"Stopped logger for {_mapping.FieldName}");
        }
    }
}
