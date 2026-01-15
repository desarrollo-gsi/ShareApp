using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaShareApp.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase _currentPage;

    // --- NUEVO: Estado de la barra lateral ---
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SidebarWidth))] // Notifica a SidebarWidth cuando esto cambie
    private bool _isSidebarExpanded = true;

    // Ancho calculado: 240px expandido, 70px colapsado
    public double SidebarWidth => IsSidebarExpanded ? 240 : 70;

    public MainWindowViewModel()
    {
        _currentPage = new DashboardViewModel();
    }

    [RelayCommand]
    private void ToggleSidebar()
    {
        IsSidebarExpanded = !IsSidebarExpanded;
    }

    [RelayCommand]
    private void Navigate(string destination)
    {
        CurrentPage = destination switch
        {
            "Dashboard" => new DashboardViewModel(),
            "Share" => new ShareViewModel(),
            "Trash" => new TrashViewModel(),
            "Word" => new WordViewModel(),
            "Excel" => new ExcelViewModel(),
            "Editor" => new DocumentEditorViewModel(),
            _ => new DashboardViewModel()
        };
    }
}