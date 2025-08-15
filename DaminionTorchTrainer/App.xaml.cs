using System.Configuration;
using System.Data;
using System.Windows;
using System;
using Serilog;
using System.IO;
using TorchSharp;

namespace DaminionTorchTrainer;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public static string? DaminionUrl { get; private set; }
    public static string? DaminionUsername { get; private set; }
    public static string? DaminionPassword { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            Console.WriteLine("[DEBUG] Application starting up...");
            
            base.OnStartup(e);

            Console.WriteLine("[DEBUG] Initializing logging...");
            // Initialize logging
            InitializeLogging();

            Console.WriteLine("[DEBUG] Initializing TorchSharp...");
            // Initialize TorchSharp (optional - app can run without it)
            try
            {
                InitializeTorchSharp();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] TorchSharp initialization failed, but continuing: {ex.Message}");
                Log.Warning(ex, "TorchSharp initialization failed, but application will continue");
                // Don't show error dialog - just log and continue
            }

            Console.WriteLine("[DEBUG] Parsing command line arguments...");
            // Parse command line arguments
            ParseCommandLineArguments(e.Args);
            
            Console.WriteLine("[DEBUG] Application startup completed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Application startup failed: {ex}");
            MessageBox.Show($"Application startup failed: {ex.Message}", 
                "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void InitializeLogging()
    {
        // Get the application directory (where the executable is located)
        var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        
        // Create a unique log file name for each run with timestamp
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var logFileName = $"daminion-trainer-{timestamp}.log";
        var logPath = Path.Combine(appDirectory, "Logs", logFileName);

        // Ensure log directory exists
        var logDir = Path.GetDirectoryName(logPath);
        if (!string.IsNullOrEmpty(logDir))
        {
            Directory.CreateDirectory(logDir);
        }

        // Clean up old log files (keep only last 10)
        CleanupOldLogFiles(logDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug() // Capture all levels including debug
            .WriteTo.File(logPath, outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.Console(outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}") // Also output to console
            .CreateLogger();

        Log.Information("Daminion TorchSharp Trainer starting up - Log file: {LogFile}", logFileName);
        Console.WriteLine($"[DEBUG] Logging initialized. Log file: {logPath}");
    }

    /// <summary>
    /// Cleans up old log files, keeping only the most recent 10 files
    /// </summary>
    private void CleanupOldLogFiles(string? logDirectory)
    {
        if (string.IsNullOrEmpty(logDirectory) || !Directory.Exists(logDirectory))
            return;

        try
        {
            var logFiles = Directory.GetFiles(logDirectory, "daminion-trainer-*.log")
                                   .OrderByDescending(f => File.GetLastWriteTime(f))
                                   .Skip(10) // Keep only the 10 most recent files
                                   .ToArray();

            foreach (var oldFile in logFiles)
            {
                try
                {
                    File.Delete(oldFile);
                    Console.WriteLine($"[DEBUG] Deleted old log file: {Path.GetFileName(oldFile)}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DEBUG] Failed to delete old log file {Path.GetFileName(oldFile)}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Error during log cleanup: {ex.Message}");
        }
    }

    private void InitializeTorchSharp()
    {
        try
        {
            Console.WriteLine("[DEBUG] Starting TorchSharp initialization...");
            Log.Information("Starting TorchSharp initialization...");
            
            // Check for native dependencies
            Console.WriteLine("[DEBUG] Checking for native dependencies...");
            Log.Information("Checking for native dependencies...");
            
            // Check if native libraries are available
            var nativeLibPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtimes", "win-x64", "native");
            Console.WriteLine($"[DEBUG] Checking for native libraries in: {nativeLibPath}");
            Log.Information("Checking for native libraries in: {Path}", nativeLibPath);
            
            if (Directory.Exists(nativeLibPath))
            {
                var nativeFiles = Directory.GetFiles(nativeLibPath, "*.dll");
                Console.WriteLine($"[DEBUG] Found {nativeFiles.Length} native DLL files:");
                foreach (var file in nativeFiles)
                {
                    Console.WriteLine($"[DEBUG]   {Path.GetFileName(file)}");
                }
            }
            else
            {
                Console.WriteLine("[DEBUG] Native library directory not found");
                Log.Warning("Native library directory not found: {Path}", nativeLibPath);
            }
            
            // Try to initialize TorchSharp with minimal operations
            Console.WriteLine("[DEBUG] Attempting basic TorchSharp operations...");
            Log.Information("Attempting basic TorchSharp operations...");
            
            // Test basic tensor creation with try-catch around each operation
            Console.WriteLine("[DEBUG] Creating test tensor...");
            try
            {
                var testTensor = torch.tensor(new float[] { 1.0f, 2.0f, 3.0f });
                Console.WriteLine("[DEBUG] Basic TorchSharp tensor creation successful");
                Log.Information("Basic TorchSharp tensor creation successful");
                
                // Test basic operations
                Console.WriteLine("[DEBUG] Testing basic operations...");
                var result = testTensor + 1.0f;
                Console.WriteLine("[DEBUG] Basic TorchSharp operations successful");
                Log.Information("Basic TorchSharp operations successful");
                
                // Test device operations
                Console.WriteLine("[DEBUG] Testing device operations...");
                var cpuTensor = testTensor.to(torch.CPU);
                Console.WriteLine("[DEBUG] CPU device operations successful");
                Log.Information("CPU device operations successful");
            }
            catch (Exception tensorEx)
            {
                Console.WriteLine($"[DEBUG] Tensor operations failed: {tensorEx.Message}");
                Console.WriteLine($"[DEBUG] Inner exception: {tensorEx.InnerException?.Message}");
                Console.WriteLine($"[DEBUG] Stack trace: {tensorEx.StackTrace}");
                Log.Error(tensorEx, "Tensor operations failed");
                throw new Exception($"Tensor operations failed: {tensorEx.Message}", tensorEx);
            }
            
            // Now try to check device availability
            try
            {
                Console.WriteLine("[DEBUG] Checking CUDA availability...");
                Log.Information("Checking CUDA availability...");
                if (torch.cuda.is_available())
                {
                    Console.WriteLine("[DEBUG] CUDA is available");
                    Log.Information("CUDA is available");
                    var cudaDeviceCount = torch.cuda.device_count();
                    Console.WriteLine($"[DEBUG] CUDA device count: {cudaDeviceCount}");
                    Log.Information("CUDA device count: {DeviceCount}", cudaDeviceCount);
                    
                    // Test CUDA operations
                    try
                    {
                        var cudaTensor = torch.tensor(new float[] { 1.0f, 2.0f, 3.0f }).to(torch.CUDA);
                        Console.WriteLine("[DEBUG] CUDA tensor operations successful");
                        Log.Information("CUDA tensor operations successful");
                    }
                    catch (Exception cudaEx)
                    {
                        Console.WriteLine($"[DEBUG] CUDA tensor operations failed: {cudaEx.Message}");
                        Log.Warning(cudaEx, "CUDA tensor operations failed, but CUDA is available");
                    }
                }
                else
                {
                    Console.WriteLine("[DEBUG] CUDA not available, using CPU");
                    Log.Information("CUDA not available, using CPU");
                }
                
                // Check MPS (Apple Silicon) - may not be available in all TorchSharp versions
                try
                {
                    // Try to access MPS through reflection to avoid compilation errors
                    var mpsBackend = typeof(torch.backends).GetProperty("mps");
                    if (mpsBackend != null)
                    {
                        var isAvailableMethod = mpsBackend.PropertyType.GetMethod("is_available");
                        if (isAvailableMethod != null)
                        {
                            var isAvailable = (bool)isAvailableMethod.Invoke(null, null);
                            Console.WriteLine($"[DEBUG] MPS (Apple Silicon) is available: {isAvailable}");
                            Log.Information("MPS (Apple Silicon) is available: {Available}", isAvailable);
                        }
                    }
                    else
                    {
                        Console.WriteLine("[DEBUG] MPS backend not available in this TorchSharp version");
                        Log.Information("MPS backend not available in this TorchSharp version");
                    }
                }
                catch (Exception mpsEx)
                {
                    Console.WriteLine($"[DEBUG] MPS check failed: {mpsEx.Message}");
                    Log.Information("MPS check failed: {Error}", mpsEx.Message);
                }
            }
            catch (Exception deviceEx)
            {
                Console.WriteLine($"[DEBUG] Device availability check failed: {deviceEx.Message}");
                Log.Warning(deviceEx, "Could not check device availability, continuing with CPU");
            }

            Console.WriteLine("[DEBUG] TorchSharp initialized successfully");
            Log.Information("TorchSharp initialized successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] TorchSharp initialization failed: {ex}");
            Log.Error(ex, "Failed to initialize TorchSharp");
            
            // Re-throw the exception to be handled by the caller
            throw;
        }
    }

    private void ParseCommandLineArguments(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLower())
            {
                case "--daminion-url":
                    if (i + 1 < args.Length)
                        DaminionUrl = args[++i].Trim('"');
                    break;
                case "--daminion-username":
                    if (i + 1 < args.Length)
                        DaminionUsername = args[++i].Trim('"');
                    break;
                case "--daminion-password":
                    if (i + 1 < args.Length)
                        DaminionPassword = args[++i].Trim('"');
                    break;
            }
        }
    }
}

