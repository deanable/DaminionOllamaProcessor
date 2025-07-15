// DaminionOllamaApp/Views/SettingsWindow.xaml.cs
using DaminionOllamaApp.ViewModels;
using System.Windows;
using System.Windows.Controls; // Required for PasswordBox
using Microsoft.Win32; // For OpenFileDialog

namespace DaminionOllamaApp.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
        }

        // This event handler updates the ViewModel's password property when the PasswordBox changes.
        private void DaminionPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel viewModel && sender is PasswordBox passwordBox)
            {
                viewModel.SetDaminionPassword(passwordBox.Password);
            }
        }

        // This method can be called by the ViewModel (or the code that shows the window)
        // to set the initial value of the PasswordBox when settings are loaded.
        public void SetPasswordBox(string password)
        {
            DaminionPasswordBox.Password = password;
        }

        // Event handler for the Gemma Service Account JSON file picker
        private void GemmaServiceAccountBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                Title = "Select Google Service Account JSON File"
            };
            if (dlg.ShowDialog() == true)
            {
                if (DataContext is SettingsViewModel viewModel)
                {
                    viewModel.Settings.GemmaServiceAccountJsonPath = dlg.FileName;
                }
            }
        }
    }
}