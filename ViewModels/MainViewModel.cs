using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PLCDataLogger.Models;
using PLCDataLogger.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace PLCDataLogger.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly PLCService _plcService;
        private readonly DispatcherTimer _readTimer;
        private readonly Dictionary<PLCRegisterData, RegisterMapping> _registerMappings = new();
        private readonly Dictionary<PLCRegisterData, RegisterLogger> _registerLoggers = new();

        [ObservableProperty]
        private string plcIpAddress = "192.168.3.39";

        [ObservableProperty]
        private int plcPort = 9005;

        [ObservableProperty]
        private string connectionStatus = "Disconnected";

        [ObservableProperty]
        private bool isConnected = false;

        [ObservableProperty]
        private string statusMessage = "Ready";

        [ObservableProperty]
        private ObservableCollection<PLCRegisterData> registers = new();

        public MainViewModel()
        {
            _plcService = PLCService.Instance;

            // Initialize with some sample registers to read
            InitializeSampleRegisters();

            // Setup timer for periodic reading (every 1 second)
            _readTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _readTimer.Tick += async (s, e) => await ReadPLCDataAsync();
        }

        private void InitializeSampleRegisters()
        {
            LoadRegistersFromConfiguration();
        }

        /// <summary>
        /// Reloads registers from configuration file. Can be called to refresh after configuration changes.
        /// </summary>
        public void ReloadConfiguration()
        {
            // Clear existing registers
            Registers.Clear();
            _registerMappings.Clear();

            // Stop any existing loggers
            foreach (var logger in _registerLoggers.Values)
            {
                logger.Stop();
            }
            _registerLoggers.Clear();

            // Reload from configuration
            LoadRegistersFromConfiguration();

            // If connected, reinitialize loggers
            if (IsConnected)
            {
                Task.Run(async () => await InitializeLoggersAsync());
            }

            StatusMessage = "Configuration reloaded";
            LoggingService.Instance.LogInfo("Configuration reloaded from file");
        }

        private void LoadRegistersFromConfiguration()
        {
            // Load registers from configuration file
            var registerMappings = ConfigurationService.Instance.LoadRegisterMappings();

            if (registerMappings.Any())
            {
                // Convert RegisterMapping objects to PLCRegisterData for display
                foreach (var mapping in registerMappings)
                {
                    var registerData = new PLCRegisterData
                    {
                        RegisterAddress = mapping.RegisterAddress,
                        Description = string.IsNullOrEmpty(mapping.Description)
                            ? mapping.FieldName
                            : mapping.Description,
                        Unit = mapping.Unit
                    };

                    Registers.Add(registerData);

                    // Store mapping for SQL logging
                    _registerMappings[registerData] = mapping;
                }

                LoggingService.Instance.LogInfo($"Initialized {Registers.Count} registers from configuration file");
            }
            else
            {
                // Fallback to sample registers if configuration loading fails
                LoggingService.Instance.LogWarning("Using fallback sample registers");

                Registers.Add(new PLCRegisterData
                {
                    RegisterAddress = "D100",
                    Description = "Temperature",
                    Unit = "°C"
                });

                Registers.Add(new PLCRegisterData
                {
                    RegisterAddress = "D101",
                    Description = "Pressure",
                    Unit = "bar"
                });

                Registers.Add(new PLCRegisterData
                {
                    RegisterAddress = "D102",
                    Description = "Flow Rate",
                    Unit = "L/min"
                });

                Registers.Add(new PLCRegisterData
                {
                    RegisterAddress = "D1000",
                    Description = "PC Status",
                    Unit = ""
                });
            }
        }

        [RelayCommand]
        private async Task ConnectAsync()
        {
            try
            {
                StatusMessage = $"Connecting to PLC at {PlcIpAddress}:{PlcPort}...";

                // Set PLC connection parameters
                _plcService.SetPLCConnection(PlcIpAddress, PlcPort);

                // Test connection by reading a register
                var testResult = await _plcService.ReadRegisterAsync(100);

                if (testResult.HasValue)
                {
                    ConnectionStatus = "Connected";
                    IsConnected = true;
                    StatusMessage = "Connected successfully";

                    // Initialize database loggers for registers with SQL configuration
                    await InitializeLoggersAsync();

                    _readTimer.Start();
                    LoggingService.Instance.LogInfo($"Connected to PLC at {PlcIpAddress}:{PlcPort}");
                }
                else
                {
                    ConnectionStatus = "Connection Failed";
                    IsConnected = false;
                    StatusMessage = "Failed to read from PLC";
                }
            }
            catch (Exception ex)
            {
                ConnectionStatus = "Error";
                IsConnected = false;
                StatusMessage = $"Error: {ex.Message}";
                LoggingService.Instance.LogError(ex, "Failed to connect to PLC");
            }
        }

        /// <summary>
        /// Initializes database loggers for registers that have SQL configuration
        /// </summary>
        private async Task InitializeLoggersAsync()
        {
            foreach (var kvp in _registerMappings)
            {
                var registerData = kvp.Key;
                var mapping = kvp.Value;

                if (mapping.Sql != null && mapping.Sql.LogMode != LogMode.Disabled)
                {
                    // Pass callback to RegisterLogger so it can access all register values in SQL queries
                    var logger = new RegisterLogger(mapping, GetAllRegisterValues);
                    await logger.InitializeAsync();
                    _registerLoggers[registerData] = logger;
                }
            }

            LoggingService.Instance.LogInfo($"Initialized {_registerLoggers.Count} database loggers");
        }

        [RelayCommand]
        private void Disconnect()
        {
            _readTimer.Stop();

            // Stop all database loggers
            foreach (var logger in _registerLoggers.Values)
            {
                logger.Stop();
            }
            _registerLoggers.Clear();

            IsConnected = false;
            ConnectionStatus = "Disconnected";
            StatusMessage = "Disconnected from PLC";

            // Clear values
            foreach (var register in Registers)
            {
                register.Value = "---";
                register.LastUpdate = "";
            }

            LoggingService.Instance.LogInfo("Disconnected from PLC");
        }

        private async Task ReadPLCDataAsync()
        {
            if (!IsConnected) return;

            try
            {
                foreach (var register in Registers)
                {
                    if (TryParseRegisterAddress(register.RegisterAddress, out int address, out int? bitIndex))
                    {
                        var value = await _plcService.ReadRegisterAsync(address);

                        if (value.HasValue)
                        {
                            string stringValue;

                            // If a bit index was specified (e.g., D3115.1), extract that specific bit
                            if (bitIndex.HasValue)
                            {
                                int bitValue = (value.Value >> bitIndex.Value) & 1;
                                stringValue = bitValue.ToString();
                            }
                            else
                            {
                                // Read the full register word
                                stringValue = value.Value.ToString();
                            }

                            register.Value = stringValue;
                            register.LastUpdate = DateTime.Now.ToString("HH:mm:ss");

                            // Process value through database logger if configured
                            if (_registerLoggers.TryGetValue(register, out var logger))
                            {
                                await logger.ProcessValueAsync(stringValue);
                                logger.UpdateIntervalTimerState(stringValue);
                            }
                        }
                        else
                        {
                            register.Value = "Error";
                        }
                    }
                    else
                    {
                        register.Value = "Invalid Address";
                        LoggingService.Instance.LogWarning($"Invalid register address format: {register.RegisterAddress}");
                    }
                }

                StatusMessage = $"Last read: {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Read error: {ex.Message}";
                LoggingService.Instance.LogError(ex, "Error reading PLC data");
            }
        }

        /// <summary>
        /// Parses a register address string (e.g., "D100", "D3115.1") into base address and optional bit index.
        /// </summary>
        /// <param name="registerAddress">The register address string (e.g., "D100", "D3115.1")</param>
        /// <param name="address">The base register address (e.g., 100, 3115)</param>
        /// <param name="bitIndex">The bit index if specified (0-15), or null if reading the full word</param>
        /// <returns>True if parsing succeeded, false otherwise</returns>
        private bool TryParseRegisterAddress(string registerAddress, out int address, out int? bitIndex)
        {
            address = 0;
            bitIndex = null;

            if (string.IsNullOrWhiteSpace(registerAddress))
                return false;

            // Remove the 'D' prefix (case-insensitive)
            string addressStr = registerAddress.Trim().ToUpper().Replace("D", "");

            // Check if there's a bit index (decimal notation like "3115.1")
            string[] parts = addressStr.Split('.');

            if (parts.Length == 1)
            {
                // Simple address like "D100" - read full word
                return int.TryParse(parts[0], out address);
            }
            else if (parts.Length == 2)
            {
                // Bit-level address like "D3115.1" - read specific bit
                if (int.TryParse(parts[0], out address) && int.TryParse(parts[1], out int bit))
                {
                    // Validate bit index is in valid range (0-15 for a 16-bit word)
                    if (bit >= 0 && bit <= 15)
                    {
                        bitIndex = bit;
                        return true;
                    }
                    else
                    {
                        LoggingService.Instance.LogWarning($"Bit index {bit} is out of range (0-15) for address {registerAddress}");
                        return false;
                    }
                }
            }

            return false;
        }

        [RelayCommand]
        private async Task WriteTestValueAsync()
        {
            if (!IsConnected)
            {
                StatusMessage = "Not connected to PLC";
                return;
            }

            try
            {
                // Write a test value to D1000
                await _plcService.WriteSingleWord(1000, 123);
                StatusMessage = "Test value written to D1000";
                LoggingService.Instance.LogInfo("Written test value 123 to D1000");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Write error: {ex.Message}";
                LoggingService.Instance.LogError(ex, "Error writing test value");
            }
        }

        /// <summary>
        /// Gets all current register values as a dictionary keyed by FieldName
        /// Used by RegisterLogger to pass all register values to SQL queries
        /// </summary>
        public Dictionary<string, string> GetAllRegisterValues()
        {
            var result = new Dictionary<string, string>();

            foreach (var kvp in _registerMappings)
            {
                var registerData = kvp.Key;
                var mapping = kvp.Value;

                // Use FieldName as the key
                if (!string.IsNullOrEmpty(mapping.FieldName))
                {
                    // Use current value from registerData, or empty string if not available
                    string currentValue = registerData.Value ?? string.Empty;
                    result[mapping.FieldName] = currentValue;
                }
            }

            return result;
        }
    }
}
