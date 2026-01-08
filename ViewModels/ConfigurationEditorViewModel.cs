using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using PLCDataLogger.Helpers;
using PLCDataLogger.Models;
using PLCDataLogger.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PLCDataLogger.ViewModels
{
    public partial class ConfigurationEditorViewModel : ObservableObject
    {
        // Event to notify when configuration is saved
        public event EventHandler? ConfigurationSaved;
        [ObservableProperty]
        private ObservableCollection<RegisterMapping> registers = new();

        [ObservableProperty]
        private RegisterMapping? selectedRegister;

        [ObservableProperty]
        private string statusMessage = "Ready";

        [ObservableProperty]
        private bool isDirty = false;

        // SQL Query Templates
        [ObservableProperty]
        private ObservableCollection<string> templates = new();

        [ObservableProperty]
        private string? selectedTemplate;

        [ObservableProperty]
        private string placeholderHelp = SqlQueryTemplates.GetPlaceholderHelp();

        private const string ConfigFileName = "registers.json";
        private const string ConfigFolderName = "Configurations";

        public ConfigurationEditorViewModel()
        {
            LoadConfiguration();
            LoadTemplates();
        }

        private void LoadTemplates()
        {
            Templates.Clear();

            var allTemplates = SqlQueryTemplates.GetAllTemplates("TableName");

            foreach (var key in allTemplates.Keys)
            {
                Templates.Add(key);
            }
        }

        [RelayCommand]
        private void LoadConfiguration()
        {
            try
            {
                var registerMappings = ConfigurationService.Instance.LoadRegisterMappings();

                Registers.Clear();
                foreach (var mapping in registerMappings)
                {
                    Registers.Add(mapping);
                }

                StatusMessage = $"Loaded {Registers.Count} registers from configuration";
                IsDirty = false;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading configuration: {ex.Message}";
                LoggingService.Instance.LogError(ex, "Failed to load configuration");
            }
        }

        [RelayCommand]
        private async Task SaveConfigurationAsync()
        {
            try
            {
                string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string configPath = Path.Combine(baseDirectory, ConfigFolderName, ConfigFileName);

                var config = new { registers = Registers.ToList() };
                string json = JsonConvert.SerializeObject(config, Formatting.Indented);

                await File.WriteAllTextAsync(configPath, json);

                StatusMessage = "Configuration saved successfully";
                IsDirty = false;
                LoggingService.Instance.LogInfo("Configuration saved to file");

                // Reload configuration to refresh the UI
                LoadConfiguration();

                // Notify subscribers that configuration was saved
                ConfigurationSaved?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error saving configuration: {ex.Message}";
                LoggingService.Instance.LogError(ex, "Failed to save configuration");
            }
        }

        [RelayCommand]
        private void AddRegister()
        {
            var newRegister = new RegisterMapping
            {
                FieldName = "NewRegister",
                RegisterAddress = "D0",
                Description = "New Register",
                DataType = DataType.INT,
                Length = 1,
                Unit = "",
                Sql = new SqlConfig
                {
                    ConnectionString = "Server=localhost;Database=PLCData;Integrated Security=true;TrustServerCertificate=true;",
                    TableName = "RegisterLogs",
                    LogMode = LogMode.Disabled,
                    IntervalSeconds = 5,
                    UseCustomQuery = false,
                    CustomQuery = ""
                }
            };

            Registers.Add(newRegister);
            SelectedRegister = newRegister;
            IsDirty = true;
            StatusMessage = "New register added";
        }

        [RelayCommand]
        private void DeleteRegister()
        {
            if (SelectedRegister == null)
            {
                StatusMessage = "No register selected";
                return;
            }

            Registers.Remove(SelectedRegister);
            SelectedRegister = null;
            IsDirty = true;
            StatusMessage = "Register deleted";
        }

        [RelayCommand]
        private void DuplicateRegister()
        {
            if (SelectedRegister == null)
            {
                StatusMessage = "No register selected";
                return;
            }

            var json = JsonConvert.SerializeObject(SelectedRegister);
            var duplicate = JsonConvert.DeserializeObject<RegisterMapping>(json);

            if (duplicate != null)
            {
                duplicate.FieldName = $"{duplicate.FieldName}_Copy";
                Registers.Add(duplicate);
                SelectedRegister = duplicate;
                IsDirty = true;
                StatusMessage = "Register duplicated";
            }
        }

        [RelayCommand]
        private void ApplyTemplate()
        {
            if (SelectedRegister?.Sql == null || string.IsNullOrEmpty(SelectedTemplate))
                return;

            var templates = SqlQueryTemplates.GetAllTemplates(SelectedRegister.Sql.TableName);
            if (templates.TryGetValue(SelectedTemplate, out string? query))
            {
                SelectedRegister.Sql.CustomQuery = query;
                SelectedRegister.Sql.UseCustomQuery = true;
                StatusMessage = $"Applied template: {SelectedTemplate}";
                IsDirty = true;

                // Force UI refresh by re-triggering property changed
                var temp = SelectedRegister;
                SelectedRegister = null;
                SelectedRegister = temp;
            }
        }

        [RelayCommand]
        private async Task TestDatabaseConnectionAsync()
        {
            if (SelectedRegister?.Sql == null)
            {
                StatusMessage = "No register selected or SQL config missing";
                return;
            }

            StatusMessage = "Testing database connection...";

            bool success = await DatabaseService.Instance.TestConnectionAsync(SelectedRegister.Sql.ConnectionString);

            if (success)
            {
                StatusMessage = "Database connection successful!";
            }
            else
            {
                StatusMessage = "Database connection failed. Check connection string.";
            }
        }

        partial void OnSelectedRegisterChanged(RegisterMapping? value)
        {
            // When register selection changes, update template selection if using custom query
            if (value?.Sql != null && value.Sql.UseCustomQuery)
            {
                // Try to match the custom query to a known template
                var allTemplates = SqlQueryTemplates.GetAllTemplates(value.Sql.TableName);

                foreach (var template in allTemplates)
                {
                    if (template.Value.Trim() == value.Sql.CustomQuery.Trim())
                    {
                        SelectedTemplate = template.Key;
                        break;
                    }
                }
            }
        }
    }
}
