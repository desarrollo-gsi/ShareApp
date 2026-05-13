using Microsoft.Extensions.DependencyInjection;
using AvaloniaShareApp.Application.Ports;
using AvaloniaShareApp.Infrastructure.Persistence;
using AvaloniaShareApp.Infrastructure.Ui.ViewModels;

namespace AvaloniaShareApp.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services)
        {
            services.AddSingleton<IDocumentRepository, FileDocumentRepository>();
            services.AddSingleton<ISpreadsheetRepository, FileSpreadsheetRepository>();
            
            // UI Adapters (ViewModels)
            services.AddSingleton<MainWindowViewModel>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<ShareViewModel>();
            services.AddTransient<TrashViewModel>();
            services.AddTransient<WordViewModel>();
            services.AddTransient<ExcelViewModel>();
            services.AddTransient<DocumentEditorViewModel>();
            services.AddTransient<SettingsViewModel>();

            return services;
        }
    }
}
