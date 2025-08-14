// DaminionOllamaApp/ViewModels/MainViewModel.cs
using DaminionOllamaApp.Models;
using DaminionOllamaApp.Services;
using DaminionOllamaApp.Utils;
using DaminionOllamaApp.Views;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace DaminionOllamaApp.ViewModels
{
    /// <summary>
    /// The main view model that serves as the central hub for the application.
    /// This class coordinates between different tagging modules and manages the global application state.
    /// It implements the MVVM pattern and provides access to:
    /// - Local file tagging functionality
    /// - Daminion collection tagging functionality  
    /// - Metadata tidy-up operations
    /// - Application settings management
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged
    {
        #region Commands
        /// <summary>
        /// Command to open the settings window for configuring application preferences.
        /// </summary>
        public ICommand OpenSettingsCommand { get; }
        
        /// <summary>
        /// Command to open the training form for ONNX model training.
        /// </summary>
        public ICommand OpenTrainingFormCommand { get; }
        
        /// <summary>
        /// Command to gracefully exit the application.
        /// </summary>
        public ICommand ExitCommand { get; }
        #endregion

        #region ViewModels
        /// <summary>
        /// View model for handling local file tagging operations.
        /// Allows users to process images from local file system with AI-generated metadata.
        /// </summary>
        public LocalFileTaggerViewModel LocalFileTaggerVM { get; }
        
        /// <summary>
        /// View model for handling Daminion collection tagging operations.
        /// Interfaces with Daminion DAM system to process cataloged images.
        /// </summary>
        public DaminionCollectionTaggerViewModel DaminionCollectionTaggerVM { get; }
        
        /// <summary>
        /// View model for metadata cleanup and organization operations.
        /// Provides tools to standardize and clean up existing metadata.
        /// </summary>
        public MetadataTidyUpViewModel MetadataTidyUpVM { get; }
        #endregion

        #region Properties
        /// <summary>
        /// The single source of truth for application settings.
        /// Contains configuration for Daminion, Ollama, OpenRouter, and other application preferences.
        /// This instance is shared across all ViewModels to ensure consistency.
        /// </summary>
        public AppSettings AppSettings { get; }

        private int _activeTabIndex;
        /// <summary>
        /// The index of the currently selected tab in the main window.
        /// 0 = Local File Tagger, 1 = Daminion Query Tagger, 2 = Metadata Tidy-up
        /// </summary>
        public int ActiveTabIndex
        {
            get => _activeTabIndex;
            set
            {
                if (_activeTabIndex != value)
                {
                    _activeTabIndex = value;
                    OnPropertyChanged(nameof(ActiveTabIndex));
                    // Also update billing info properties
                    OnPropertyChanged(nameof(BillingProvider));
                    OnPropertyChanged(nameof(ActualSpendUSD));
                    OnPropertyChanged(nameof(FreeTierLimit));
                    OnPropertyChanged(nameof(FreeTierExceeded));
                    OnPropertyChanged(nameof(RefreshBillingCommand));
                }
            }
        }

        private LocalFileTaggerViewModel GetActiveLocalFileTaggerVM() => LocalFileTaggerVM;
        private DaminionCollectionTaggerViewModel GetActiveDaminionCollectionTaggerVM() => DaminionCollectionTaggerVM;
        private MetadataTidyUpViewModel GetActiveMetadataTidyUpVM() => MetadataTidyUpVM;

        /// <summary>
        /// The provider currently used for billing (Gemma/Google, OpenRouter, Ollama).
        /// </summary>
        public string BillingProvider
        {
            get
            {
                switch (ActiveTabIndex)
                {
                    case 0: // Local File Tagger
                        return LocalFileTaggerVM?.Settings?.SelectedAiProvider switch
                        {
                            Models.AiProvider.Gemma => "Google (Gemma/BigQuery)",
                            Models.AiProvider.OpenRouter => "OpenRouter",
                            Models.AiProvider.Ollama => "Ollama",
                            _ => "Unknown"
                        };
                    case 1: // Daminion Query Tagger
                        return DaminionCollectionTaggerVM?.Settings?.SelectedAiProvider switch
                        {
                            Models.AiProvider.Gemma => "Google (Gemma/BigQuery)",
                            Models.AiProvider.OpenRouter => "OpenRouter",
                            Models.AiProvider.Ollama => "Ollama",
                            _ => "Unknown"
                        };
                    case 2: // Metadata Tidy-up
                        return MetadataTidyUpVM?.Settings?.SelectedAiProvider switch
                        {
                            Models.AiProvider.Gemma => "Google (Gemma/BigQuery)",
                            Models.AiProvider.OpenRouter => "OpenRouter",
                            Models.AiProvider.Ollama => "Ollama",
                            _ => "Unknown"
                        };
                    default:
                        return "Unknown";
                }
            }
        }

        public double ActualSpendUSD
        {
            get
            {
                switch (ActiveTabIndex)
                {
                    case 0: return LocalFileTaggerVM?.ActualSpendUSD ?? -1;
                    case 1: return DaminionCollectionTaggerVM?.ActualSpendUSD ?? -1;
                    case 2: return MetadataTidyUpVM?.ActualSpendUSD ?? -1;
                    default: return -1;
                }
            }
        }

        public double FreeTierLimit
        {
            get
            {
                switch (ActiveTabIndex)
                {
                    case 0: return LocalFileTaggerVM?.GetFreeTierForSelectedModel() ?? 0;
                    case 1: return DaminionCollectionTaggerVM?.GetFreeTierForSelectedModel() ?? 0;
                    case 2: return MetadataTidyUpVM?.GetFreeTierForSelectedModel() ?? 0;
                    default: return 0;
                }
            }
        }

        public bool FreeTierExceeded
        {
            get
            {
                switch (ActiveTabIndex)
                {
                    case 0: return LocalFileTaggerVM?.ShowActualSpendAlert ?? false;
                    case 1: return DaminionCollectionTaggerVM?.ShowActualSpendAlert ?? false;
                    case 2: return MetadataTidyUpVM?.ShowActualSpendAlert ?? false;
                    default: return false;
                }
            }
        }

        public System.Windows.Input.ICommand RefreshBillingCommand
        {
            get
            {
                switch (ActiveTabIndex)
                {
                    case 0: return LocalFileTaggerVM?.RefreshActualSpendCommand;
                    case 1: return DaminionCollectionTaggerVM?.RefreshActualSpendCommand;
                    case 2: return MetadataTidyUpVM?.RefreshActualSpendCommand;
                    default: return null;
                }
            }
        }
        #endregion

        #region Constructor
        /// <summary>
        /// Initializes a new instance of the MainViewModel.
        /// Sets up the application's core components and loads configuration.
        /// </summary>
        public MainViewModel()
        {
            // Load application settings from storage (typically JSON file)
            var settingsService = new SettingsService();
            AppSettings = settingsService.LoadSettings();

            // Initialize commands with their respective handlers
            OpenSettingsCommand = new RelayCommand(param => OpenSettingsWindow());
            OpenTrainingFormCommand = new RelayCommand(param => OpenTrainingForm());
            ExitCommand = new RelayCommand(param => ExitApplication());

            // Initialize child ViewModels with shared settings and services
            // Each VM gets the same AppSettings instance to ensure consistency
            LocalFileTaggerVM = new LocalFileTaggerViewModel(AppSettings, settingsService);
            DaminionCollectionTaggerVM = new DaminionCollectionTaggerViewModel(AppSettings, settingsService);
            MetadataTidyUpVM = new MetadataTidyUpViewModel(AppSettings, settingsService);
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Opens the settings window as a modal dialog.
        /// Handles the configuration of application settings including:
        /// - Daminion server connection details
        /// - Ollama/OpenRouter AI service configuration
        /// - Default prompts and processing options
        /// </summary>
        private void OpenSettingsWindow()
        {
            // Create settings view model with current settings
            var settingsViewModel = new SettingsViewModel(this.AppSettings);
            
            // Create and configure the settings window
            var settingsWindow = new SettingsWindow
            {
                Owner = Application.Current.MainWindow, // Set parent for proper modal behavior
                DataContext = settingsViewModel
            };

            // Define the save/close action for the settings window
            settingsViewModel.CloseAction = () =>
            {
                // Persist the modified settings to storage
                var settingsService = new SettingsService();
                settingsService.SaveSettings(this.AppSettings);
                settingsWindow.Close();
            };

            // Configure password box handling (WPF PasswordBox requires special handling)
            settingsViewModel.UpdatePasswordBoxAction = (pwd) => settingsWindow.SetPasswordBox(pwd);
            settingsViewModel.UpdatePasswordBoxAction?.Invoke(settingsViewModel.Settings.DaminionPassword);

            // Show the settings window as a modal dialog
            settingsWindow.ShowDialog();
        }

        /// <summary>
        /// Opens the DaminionTorchTrainer application for ONNX model training.
        /// Launches the training form as a separate process.
        /// </summary>
        private void OpenTrainingForm()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[DEBUG] OpenTrainingForm method called");
                
                // Get the path to the DaminionTorchTrainer executable
                // Use a simple relative path approach
                var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var assemblyDirectory = System.IO.Path.GetDirectoryName(assemblyLocation);
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Assembly directory: {assemblyDirectory}");
                
                // Simple approach: go up 4 levels from the current assembly location to reach solution root
                var solutionRoot = assemblyDirectory;
                for (int i = 0; i < 4; i++)
                {
                    solutionRoot = System.IO.Directory.GetParent(solutionRoot)?.FullName;
                    if (string.IsNullOrEmpty(solutionRoot))
                    {
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] Could not go up {i + 1} levels from assembly directory");
                        break;
                    }
                }
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Solution root: {solutionRoot}");
                
                if (string.IsNullOrEmpty(solutionRoot))
                {
                    System.Diagnostics.Debug.WriteLine("[DEBUG] Could not determine solution root directory");
                    MessageBox.Show("Could not determine solution root directory.", 
                                  "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                var trainerPath = System.IO.Path.Combine(solutionRoot, "DaminionTorchTrainer", "bin", "Debug", "net8.0-windows10.0.26100.0", "DaminionTorchTrainer.exe");
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Trainer path: {trainerPath}");
                
                // Check if the executable exists
                var fileExists = System.IO.File.Exists(trainerPath);
                System.Diagnostics.Debug.WriteLine($"[DEBUG] File exists: {fileExists}");
                
                if (!fileExists)
                {
                    System.Diagnostics.Debug.WriteLine("[DEBUG] Training form executable not found at calculated path, trying to find it...");
                    
                    // Try to find the executable by searching common locations
                    var possiblePaths = new List<string>();
                    
                    // Try relative to current assembly - go up 4 levels to solution root, then to DaminionTorchTrainer
                    if (!string.IsNullOrEmpty(assemblyDirectory))
                    {
                        var currentDir = assemblyDirectory;
                        for (int i = 0; i < 4; i++)
                        {
                            currentDir = System.IO.Directory.GetParent(currentDir)?.FullName;
                            if (string.IsNullOrEmpty(currentDir)) break;
                        }
                        if (!string.IsNullOrEmpty(currentDir))
                        {
                            possiblePaths.Add(System.IO.Path.Combine(currentDir, "DaminionTorchTrainer", "bin", "Debug", "net8.0-windows10.0.26100.0", "DaminionTorchTrainer.exe"));
                        }
                    }
                    
                    // Try relative to solution root if we found it
                    if (!string.IsNullOrEmpty(solutionRoot))
                    {
                        possiblePaths.Add(System.IO.Path.Combine(solutionRoot, "DaminionTorchTrainer", "bin", "Debug", "net8.0-windows10.0.26100.0", "DaminionTorchTrainer.exe"));
                        possiblePaths.Add(System.IO.Path.Combine(solutionRoot, "DaminionTorchTrainer", "bin", "Release", "net8.0-windows10.0.26100.0", "DaminionTorchTrainer.exe"));
                    }
                    
                    // Try to find any DaminionTorchTrainer.exe in the solution directory
                    if (!string.IsNullOrEmpty(solutionRoot))
                    {
                        try
                        {
                            var allExes = System.IO.Directory.GetFiles(solutionRoot, "DaminionTorchTrainer.exe", System.IO.SearchOption.AllDirectories);
                            possiblePaths.AddRange(allExes);
                        }
                        catch (Exception searchEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"[DEBUG] Error searching for executable: {searchEx.Message}");
                        }
                    }
                    
                    // Try each possible path
                    foreach (var path in possiblePaths)
                    {
                        var normalizedPath = System.IO.Path.GetFullPath(path);
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] Trying path: {normalizedPath}");
                        
                        if (System.IO.File.Exists(normalizedPath))
                        {
                            trainerPath = normalizedPath;
                            fileExists = true;
                            System.Diagnostics.Debug.WriteLine($"[DEBUG] Found executable at: {trainerPath}");
                            break;
                        }
                    }
                    
                    if (!fileExists)
                    {
                        System.Diagnostics.Debug.WriteLine("[DEBUG] Training form executable not found in any location");
                        MessageBox.Show($"Training form executable not found at: {trainerPath}\nPlease ensure the DaminionTorchTrainer project has been built.", 
                                      "Training Form Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                System.Diagnostics.Debug.WriteLine("[DEBUG] Launching training form...");
                
                // Try multiple approaches to launch the process
                Process process = null;
                
                // Approach 1: Use Process.Start with ProcessStartInfo
                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = trainerPath,
                        UseShellExecute = true,
                        WorkingDirectory = System.IO.Path.GetDirectoryName(trainerPath)
                    };

                    // Pass authentication credentials as command line arguments
                    if (!string.IsNullOrEmpty(AppSettings.DaminionServerUrl) &&
                        !string.IsNullOrEmpty(AppSettings.DaminionUsername) &&
                        !string.IsNullOrEmpty(AppSettings.DaminionPassword))
                    {
                        startInfo.Arguments = $"--daminion-url \"{AppSettings.DaminionServerUrl}\" " +
                                             $"--daminion-username \"{AppSettings.DaminionUsername}\" " +
                                             $"--daminion-password \"{AppSettings.DaminionPassword}\"";
                        System.Diagnostics.Debug.WriteLine("[DEBUG] Passing authentication credentials to training form");
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Using ProcessStartInfo with WorkingDirectory: {startInfo.WorkingDirectory}");
                    process = Process.Start(startInfo);
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Process started successfully with ID: {process?.Id}");
                    
                    // Try to bring the window to the foreground
                    if (process != null)
                    {
                        try
                        {
                            // Wait a moment for the window to appear
                            System.Threading.Thread.Sleep(500);
                            process.WaitForInputIdle(3000); // Wait for the process to be ready for input
                            
                            // Try to bring the window to the foreground
                            if (!process.HasExited)
                            {
                                System.Diagnostics.Debug.WriteLine("[DEBUG] Attempting to bring window to foreground");
                                // Note: SetForegroundWindow requires the process to be in the foreground
                                // This is a limitation of Windows security
                            }
                        }
                        catch (Exception foregroundEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"[DEBUG] Could not bring window to foreground: {foregroundEx.Message}");
                        }
                    }
                }
                catch (Exception ex1)
                {
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Approach 1 failed: {ex1.Message}");
                    
                    // Approach 2: Try without UseShellExecute
                    try
                    {
                        var startInfo2 = new ProcessStartInfo
                        {
                            FileName = trainerPath,
                            UseShellExecute = false,
                            WorkingDirectory = System.IO.Path.GetDirectoryName(trainerPath)
                        };
                        
                        System.Diagnostics.Debug.WriteLine("[DEBUG] Trying Approach 2 without UseShellExecute");
                        process = Process.Start(startInfo2);
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] Process started successfully with ID: {process?.Id}");
                    }
                    catch (Exception ex2)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] Approach 2 failed: {ex2.Message}");
                        
                        // Approach 3: Try with just the filename
                        try
                        {
                            System.Diagnostics.Debug.WriteLine("[DEBUG] Trying Approach 3 with just filename");
                            process = Process.Start(trainerPath);
                            System.Diagnostics.Debug.WriteLine($"[DEBUG] Process started successfully with ID: {process?.Id}");
                        }
                        catch (Exception ex3)
                        {
                            System.Diagnostics.Debug.WriteLine($"[DEBUG] Approach 3 failed: {ex3.Message}");
                            throw new Exception($"All launch approaches failed. Last error: {ex3.Message}");
                        }
                    }
                }
                
                if (process != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Process launched successfully. ID: {process.Id}, HasExited: {process.HasExited}");
                    
                    // Wait a moment to see if it exits immediately
                    if (!process.WaitForExit(2000)) // Wait 2 seconds
                    {
                        System.Diagnostics.Debug.WriteLine("[DEBUG] Process is still running after 2 seconds");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] Process exited with code: {process.ExitCode}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[DEBUG] Process.Start returned null");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Exception occurred: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Stack trace: {ex.StackTrace}");
                MessageBox.Show($"Error launching training form: {ex.Message}", 
                              "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Gracefully shuts down the application.
        /// Ensures all resources are properly cleaned up before exit.
        /// </summary>
        private void ExitApplication()
        {
            Application.Current.Shutdown();
        }
        #endregion

        #region INotifyPropertyChanged Implementation
        /// <summary>
        /// Event raised when a property value changes.
        /// Required for WPF data binding to work properly.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;
        
        /// <summary>
        /// Raises the PropertyChanged event for the specified property.
        /// </summary>
        /// <param name="propertyName">The name of the property that changed.</param>
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}