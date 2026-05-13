using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using AvaloniaShareApp.Application.Ports;
using AvaloniaShareApp.Domain.Entities;
using System.Linq;

namespace AvaloniaShareApp.Infrastructure.Ui.ViewModels
{
    public partial class TrashViewModel : ViewModelBase
    {
        private readonly IMediator _mediator;

        [ObservableProperty]
        private ObservableCollection<DeletedItemViewModel> _deletedItems = new();

        [ObservableProperty]
        private bool _isEmpty = true;

        public TrashViewModel(IMediator mediator)
        {
            _mediator = mediator;
            LoadDeletedItemsCommand.Execute(null);
        }
        
        public TrashViewModel() { }

        [RelayCommand]
        public async Task LoadDeletedItems()
        {
            if (_mediator == null) return;
            var items = await _mediator.Send(new Application.UseCases.Trash.Queries.GetDeletedItems.GetDeletedItemsQuery());

            var viewModels = items.Select(i => new DeletedItemViewModel
            {
                Id = i.Id,
                Title = i.Title,
                UpdatedAt = i.UpdatedAt,
                Icon = i.Icon,
                ItemType = i.ItemType
            });

            DeletedItems = new ObservableCollection<DeletedItemViewModel>(viewModels);
            IsEmpty = !DeletedItems.Any();
        }

        [RelayCommand]
        public async Task RestoreItem(DeletedItemViewModel item)
        {
            if (item == null || _mediator == null) return;
            await _mediator.Send(new Application.UseCases.Trash.Commands.RestoreItem.RestoreItemCommand(item.Id, item.ItemType));
            await LoadDeletedItems();
        }

        [RelayCommand]
        public async Task PermanentDelete(DeletedItemViewModel item)
        {
            if (item == null || _mediator == null) return;
            await _mediator.Send(new Application.UseCases.Trash.Commands.PermanentDeleteItem.PermanentDeleteItemCommand(item.Id, item.ItemType));
            await LoadDeletedItems();
        }

        [RelayCommand]
        public async Task EmptyTrash()
        {
            if (_mediator == null) return;
            await _mediator.Send(new Application.UseCases.Trash.Commands.EmptyTrash.EmptyTrashCommand());
            await LoadDeletedItems();
        }
    }

    public class DeletedItemViewModel
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public DateTime UpdatedAt { get; set; }
        public string Icon { get; set; } = "";
        public string ItemType { get; set; } = "";
    }
}
