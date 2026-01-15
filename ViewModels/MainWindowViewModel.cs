using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaShareApp.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase _currentPage;

    public MainWindowViewModel()
    {
        _currentPage = new DashboardViewModel();
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
