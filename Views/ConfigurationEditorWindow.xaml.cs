using System.Windows;

namespace PLCDataLogger.Views
{
    public partial class ConfigurationEditorWindow : Window
    {
        public ConfigurationEditorWindow()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
