using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using AvaloniaShareApp.Application.UseCases.Documents.Queries.GetDashboardStats;
using AvaloniaShareApp.Application.Ports;
using System.Linq;

namespace AvaloniaShareApp.Infrastructure.Ui.ViewModels
{
    public partial class DashboardViewModel : ViewModelBase
    {
        private readonly IMediator _mediator;

        [ObservableProperty]
        private string _userName = "Usuario";

        [ObservableProperty]
        private string _totalStorage = "0 GB";

        [ObservableProperty]
        private double _storagePercentage = 0;

        public ObservableCollection<RecentFileViewModel> RecentFiles { get; } = new();

        public DashboardViewModel(IMediator mediator)
        {
            _mediator = mediator;
            LoadDashboardDataAsync();
        }

        public DashboardViewModel()
        {
            _mediator = null!;
        }

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        partial void OnSearchQueryChanged(string value)
        {
            LoadDashboardDataCommand.Execute(null);
        }

        [RelayCommand]
        public async Task LoadDashboardData()
        {
            if (_mediator == null) return;

            var stats = await _mediator.Send(new GetDashboardStatsQuery());
            
            UserName = stats.UserName;
            TotalStorage = stats.TotalStorage;
            StoragePercentage = stats.StoragePercentage;

            var docs = await _mediator.Send(new Application.UseCases.Documents.Queries.GetAllDocuments.GetAllDocumentsQuery(SearchQuery));

            RecentFiles.Clear();
            foreach (var doc in docs.OrderByDescending(d => d.UpdatedAt).Take(10))
            {
                RecentFiles.Add(new RecentFileViewModel(doc.Id, doc.Title, "Documento", doc.UpdatedAt.ToString("dd MMM"), "📄", "#3B82F6"));
            }
        }

        private async void LoadDashboardDataAsync()
        {
            await LoadDashboardData();
        }

        [RelayCommand]
        public async Task DeleteFile(string id)
        {
            if (_mediator == null) return;
            await _mediator.Send(new Application.UseCases.Documents.Commands.DeleteDocument.DeleteDocumentCommand(id));
            await LoadDashboardData();
        }

        [RelayCommand]
        public async Task OpenFile(string id)
        {
            var mainWindowVm = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop 
                ? desktop.MainWindow?.DataContext as MainWindowViewModel 
                : null;

            if (mainWindowVm != null)
            {
                mainWindowVm.NavigateCommand.Execute("Editor");
                if (mainWindowVm.CurrentPage is DocumentEditorViewModel editorVm)
                {
                    await editorVm.LoadDocument(id);
                }
            }
        }
    }

    public partial class RecentFileViewModel : ObservableObject
    {
        public string Id { get; }
        public string Name { get; }
        public string Type { get; }
        public string Modified { get; }
        public string IconType { get; }
        public string AccentColor { get; }

        public RecentFileViewModel(string id, string name, string type, string modified, string iconType, string accentColor)
        {
            Id = id;
            Name = name;
            Type = type;
            Modified = modified;
            IconType = iconType;
            AccentColor = accentColor;
        }
    }
}
