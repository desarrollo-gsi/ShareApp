using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using AvaloniaShareApp.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using System.Linq;

namespace AvaloniaShareApp.Infrastructure.Ui.ViewModels
{
    public partial class ExcelViewModel : ViewModelBase
    {
        private readonly IMediator _mediator;
        
        [ObservableProperty]
        private string _spreadsheetTitle = "Hoja de cálculo sin título";

        [ObservableProperty]
        private Spreadsheet? _currentSpreadsheet;

        [ObservableProperty]
        private ObservableCollection<SpreadsheetItem> _recentSpreadsheets = new();

        public event EventHandler<Spreadsheet>? SpreadsheetLoaded;

        public ExcelViewModel(IMediator mediator)
        {
            _mediator = mediator;
            LoadRecentSpreadsheetsCommand.Execute(null);
        }

        public ExcelViewModel()
        {
            _mediator = null!;
        }

        [ObservableProperty]
        private bool _isEditorVisible = false;

        [ObservableProperty]
        private string _searchQuery = "";

        [RelayCommand]
        public void GoBackToDashboard()
        {
            IsEditorVisible = false;
            LoadRecentSpreadsheetsCommand.Execute(null);
        }

        [RelayCommand]
        public async Task LoadRecentSpreadsheets()
        {
            if (_mediator == null) return;
            var sheets = await _mediator.Send(new Application.UseCases.Spreadsheets.Queries.GetAllSpreadsheets.GetAllSpreadsheetsQuery());
            
            var filtered = sheets;
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                filtered = sheets.Where(s => s.Title.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase));
            }

            RecentSpreadsheets.Clear();
            foreach (var sheet in filtered.OrderByDescending(s => s.UpdatedAt))
            {
                RecentSpreadsheets.Add(new SpreadsheetItem 
                { 
                    Id = sheet.Id, 
                    Title = sheet.Title, 
                    UpdatedAt = sheet.UpdatedAt 
                });
            }
        }

        partial void OnSearchQueryChanged(string value)
        {
            LoadRecentSpreadsheetsCommand.Execute(null);
        }

        [RelayCommand]
        public async Task CreateNewSpreadsheet()
        {
            CurrentSpreadsheet = new Spreadsheet();
            SpreadsheetTitle = CurrentSpreadsheet.Title;
            IsEditorVisible = true;
            await Task.Delay(100); // Give UI time to switch
            SpreadsheetLoaded?.Invoke(this, CurrentSpreadsheet);
        }

        [RelayCommand]
        public async Task SaveSpreadsheet()
        {
            if (CurrentSpreadsheet == null || _mediator == null) return;
            CurrentSpreadsheet.Title = SpreadsheetTitle;
            await _mediator.Send(new Application.UseCases.Spreadsheets.Commands.SaveSpreadsheet.SaveSpreadsheetCommand(CurrentSpreadsheet));
            await LoadRecentSpreadsheets();
        }

        [RelayCommand]
        public async Task LoadSpreadsheet(string id)
        {
            if (_mediator == null) return;
            var sheet = await _mediator.Send(new Application.UseCases.Spreadsheets.Queries.GetSpreadsheetById.GetSpreadsheetByIdQuery(id));
            if (sheet != null)
            {
                CurrentSpreadsheet = sheet;
                SpreadsheetTitle = sheet.Title;
                IsEditorVisible = true;
                await Task.Delay(100); // Give UI time to switch
                SpreadsheetLoaded?.Invoke(this, sheet);
            }
        }
    }

    public class SpreadsheetItem
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public DateTime UpdatedAt { get; set; }
    }
}
