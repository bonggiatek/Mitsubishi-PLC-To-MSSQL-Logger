using Newtonsoft.Json;
using PLCDataLogger.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PLCDataLogger.Services
{
    public class ConfigurationService
    {
        private static readonly Lazy<ConfigurationService> _instance = new(() => new ConfigurationService());
        public static ConfigurationService Instance => _instance.Value;

        private const string ConfigFileName = "registers.json";
        private const string ConfigFolderName = "Configurations";

        private ConfigurationService() { }

        /// <summary>
        /// Loads register mappings from the registers.json configuration file.
        /// </summary>
        /// <returns>List of RegisterMapping objects, or empty list if loading fails</returns>
        public List<RegisterMapping> LoadRegisterMappings()
        {
            try
            {
                // Get the application's base directory
                string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string configPath = Path.Combine(baseDirectory, ConfigFolderName, ConfigFileName);

                // Check if the configuration file exists
                if (!File.Exists(configPath))
                {
                    LoggingService.Instance.LogWarning($"Configuration file not found at: {configPath}");
                    return new List<RegisterMapping>();
                }

                // Read and deserialize the JSON file
                string jsonContent = File.ReadAllText(configPath);
                var config = JsonConvert.DeserializeObject<RegisterConfiguration>(jsonContent);

                if (config?.Registers == null || !config.Registers.Any())
                {
                    LoggingService.Instance.LogWarning("No registers found in configuration file");
                    return new List<RegisterMapping>();
                }

                LoggingService.Instance.LogInfo($"Loaded {config.Registers.Count} registers from configuration");
                return config.Registers;
            }
            catch (JsonException jsonEx)
            {
                LoggingService.Instance.LogError(jsonEx, "Failed to parse JSON configuration file");
                return new List<RegisterMapping>();
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError(ex, "Failed to load register configuration");
                return new List<RegisterMapping>();
            }
        }

        /// <summary>
        /// Helper class for JSON deserialization
        /// </summary>
        private class RegisterConfiguration
        {
            [JsonProperty("registers")]
            public List<RegisterMapping> Registers { get; set; } = new();
        }
    }
}
