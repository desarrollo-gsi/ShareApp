using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using AvaloniaShareApp.Application.Ports;
using AvaloniaShareApp.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Linq;
using MediatR;

namespace AvaloniaShareApp.Infrastructure.Ui.ViewModels
{
    public partial class WordViewModel : ViewModelBase
    {
        private readonly IMediator _mediator;
        public string Title => "Documentos";

        [ObservableProperty]
        private ObservableCollection<DocumentItem> _recentDocuments = new();

        public WordViewModel(IMediator mediator)
        {
            _mediator = mediator;
            LoadDocumentsCommand.Execute(null);
        }

        public WordViewModel()
        {
            _mediator = null!;
        }

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        partial void OnSearchQueryChanged(string value)
        {
            LoadDocumentsCommand.Execute(null);
        }

        [RelayCommand]
        public async Task LoadDocuments()
        {
            if (_mediator == null) return;
            var docs = await _mediator.Send(new Application.UseCases.Documents.Queries.GetAllDocuments.GetAllDocumentsQuery(SearchQuery));
            
            RecentDocuments = new ObservableCollection<DocumentItem>(
                docs.Select(d => new DocumentItem 
                { 
                    Id = d.Id, 
                    Title = d.Title, 
                    LastOpened = d.UpdatedAt 
                }).OrderByDescending(d => d.LastOpened)
            );
        }

        [RelayCommand]
        public async Task DeleteDocument(string id)
        {
            if (_mediator == null) return;
            await _mediator.Send(new Application.UseCases.Documents.Commands.DeleteDocument.DeleteDocumentCommand(id));
            await LoadDocuments();
        }

        [RelayCommand]
        public async Task OpenDocument(string id)
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
}
