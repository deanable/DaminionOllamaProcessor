using System.Windows;
using System.Windows.Controls;
using DaminionTorchTrainer.ViewModels;
using System;

namespace DaminionTorchTrainer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            try
            {
                Console.WriteLine("[DEBUG] MainWindow constructor starting...");
                InitializeComponent();
                Console.WriteLine("[DEBUG] InitializeComponent completed");
                
                // Create and set the ViewModel as DataContext
                var viewModel = new MainViewModel();
                DataContext = viewModel;
                
                Console.WriteLine("[DEBUG] MainWindow constructor completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] MainWindow constructor failed: {ex}");
                MessageBox.Show($"MainWindow constructor failed: {ex.Message}", 
                    "MainWindow Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DaminionPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel && sender is PasswordBox passwordBox)
            {
                viewModel.DaminionPassword = passwordBox.Password;
            }
        }
    }
}