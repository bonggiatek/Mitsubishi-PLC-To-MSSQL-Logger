using CommunityToolkit.Mvvm.ComponentModel;

namespace PLCDataLogger.Models
{
    public partial class PLCRegisterData : ObservableObject
    {
        [ObservableProperty]
        private string registerAddress = string.Empty;

        [ObservableProperty]
        private string description = string.Empty;

        [ObservableProperty]
        private string value = "---";

        [ObservableProperty]
        private string unit = string.Empty;

        [ObservableProperty]
        private string lastUpdate = string.Empty;
    }
}
