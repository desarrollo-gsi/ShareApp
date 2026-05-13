using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace AvaloniaShareApp.Infrastructure.Ui.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase _currentPage;

    [ObservableProperty]
    private string _currentViewName = "Dashboard";

    // --- NUEVO: Estado de la barra lateral ---
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SidebarWidth))] // Notifica a SidebarWidth cuando esto cambie
    private bool _isSidebarExpanded = true;

    // Ancho calculado: 210px expandido, 64px colapsado
    public double SidebarWidth => IsSidebarExpanded ? 210 : 64;

    private readonly IServiceProvider _serviceProvider;

    public MainWindowViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _currentPage = _serviceProvider.GetRequiredService<DashboardViewModel>();
    }

    public MainWindowViewModel() 
    {
        _serviceProvider = null!;
        _currentPage = null!;
    }

    [RelayCommand]
    private void ToggleSidebar()
    {
        IsSidebarExpanded = !IsSidebarExpanded;
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        if (Avalonia.Application.Current != null)
        {
            var isDark = Avalonia.Application.Current.RequestedThemeVariant == ThemeVariant.Dark;
            Avalonia.Application.Current.RequestedThemeVariant = isDark ? ThemeVariant.Light : ThemeVariant.Dark;
        }
    }

    [RelayCommand]
    private void Navigate(string destination)
    {
        if (CurrentViewName == destination) return;

        CurrentViewName = destination;
        CurrentPage = destination switch
        {
            "Dashboard" => _serviceProvider.GetRequiredService<DashboardViewModel>(),
            "Share" => _serviceProvider.GetRequiredService<ShareViewModel>(),
            "Trash" => _serviceProvider.GetRequiredService<TrashViewModel>(),
            "Word" => _serviceProvider.GetRequiredService<WordViewModel>(),
            "Excel" => _serviceProvider.GetRequiredService<ExcelViewModel>(),
            "Editor" => _serviceProvider.GetRequiredService<DocumentEditorViewModel>(),
            "Settings" => _serviceProvider.GetRequiredService<SettingsViewModel>(),
            _ => _serviceProvider.GetRequiredService<DashboardViewModel>()
        };
    }
}