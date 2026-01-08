using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using PLCDataLogger.ViewModels;

namespace PLCDataLogger;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void ConfigureButton_Click(object sender, RoutedEventArgs e)
    {
        var configWindow = new Views.ConfigurationEditorWindow();

        // Subscribe to the ConfigurationSaved event
        if (configWindow.DataContext is ConfigurationEditorViewModel viewModel)
        {
            viewModel.ConfigurationSaved += (s, args) =>
            {
                // Reload the main window's configuration
                if (this.DataContext is MainViewModel mainViewModel)
                {
                    mainViewModel.ReloadConfiguration();
                }
            };
        }

        configWindow.ShowDialog();
    }
}