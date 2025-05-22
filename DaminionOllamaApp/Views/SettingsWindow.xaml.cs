// DaminionOllamaApp/Views/SettingsWindow.xaml.cs
using DaminionOllamaApp.ViewModels;
using System.Windows;
using System.Windows.Controls; // Required for PasswordBox

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
    }
}