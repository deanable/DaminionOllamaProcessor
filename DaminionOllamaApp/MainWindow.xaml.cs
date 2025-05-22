// DaminionOllamaApp/MainWindow.xaml.cs
using DaminionOllamaApp.ViewModels; // For MainViewModel
using System.Windows;

namespace DaminionOllamaApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel(); // Set the DataContext here
        }

        // Ensure the old OpenSettings_Click handler from the test button is removed
    }
}