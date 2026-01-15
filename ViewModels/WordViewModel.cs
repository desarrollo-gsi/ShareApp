using System;
using System.Collections.ObjectModel;
using AvaloniaShareApp.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaloniaShareApp.ViewModels
{
    public partial class WordViewModel : ViewModelBase
    {
        public string Title => "Word Section";

        [ObservableProperty]
        private ObservableCollection<DocumentItem> _recentDocuments;

        public WordViewModel()
        {
            RecentDocuments = new ObservableCollection<DocumentItem>
            {
                new DocumentItem { Title = "Enlaces_Iconos", LastOpened = DateTime.Now.AddDays(-1), Owner = "Yo" },
                new DocumentItem { Title = "Presupuesto 2026", LastOpened = DateTime.Now.AddDays(-5), Icon = "📊" },
                new DocumentItem { Title = "Carta de Renuncia", LastOpened = DateTime.Now.AddDays(-20) },
                new DocumentItem { Title = "Proyecto Avalonia", LastOpened = DateTime.Now.AddHours(-2) },
                new DocumentItem { Title = "Notas de reunión", LastOpened = DateTime.Now.AddDays(-3) }
            };
        }
    }
}
