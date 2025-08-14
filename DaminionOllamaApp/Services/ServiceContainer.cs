using Microsoft.Extensions.DependencyInjection;
using DaminionOllamaApp.ViewModels;
using DaminionOllamaApp.Services;
using DaminionOllamaInteractionLib;
using DaminionOllamaInteractionLib.Services;

namespace DaminionOllamaApp.Services
{
    /// <summary>
    /// Configures dependency injection for the application.
    /// </summary>
    public static class ServiceContainer
    {
        /// <summary>
        /// Configures services for dependency injection.
        /// </summary>
        /// <param name="services">The service collection to configure.</param>
        public static void ConfigureServices(IServiceCollection services)
        {
            // Core Services
            services.AddSingleton<SettingsService>();
            services.AddSingleton<LogService>();
            services.AddTransient<ProcessingService>();
            
            // API Clients
            services.AddTransient<DaminionApiClient>();
            services.AddTransient<BigQueryBillingClient>();
            services.AddTransient<GemmaApiClient>();
            
            // ViewModels
            services.AddTransient<MainViewModel>();
            services.AddTransient<LocalFileTaggerViewModel>();
            services.AddTransient<DaminionCollectionTaggerViewModel>();
            services.AddTransient<MetadataTidyUpViewModel>();
            services.AddTransient<SettingsViewModel>();
            
            // Settings
            services.AddSingleton(provider => 
            {
                var settingsService = provider.GetRequiredService<SettingsService>();
                return settingsService.LoadSettings();
            });
        }
    }
}