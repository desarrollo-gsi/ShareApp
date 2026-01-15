using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;

namespace AvaloniaShareApp.ViewModels
{
    public partial class DocumentEditorViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _documentTitle = "Documento sin título";

        [ObservableProperty]
        private string _documentContent = "";

        // --- Propiedades de Estilo (Booleanos para los botones) ---
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FontWeight))]
        private bool _isBold;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FontStyle))]
        private bool _isItalic;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TextDecorations))]
        private bool _isUnderline;

        // --- Propiedades Calculadas (Para el TextBox) ---
        public FontWeight FontWeight => IsBold ? FontWeight.Bold : FontWeight.Normal;
        public FontStyle FontStyle => IsItalic ? FontStyle.Italic : FontStyle.Normal;
        public TextDecorationCollection? TextDecorations => IsUnderline ? Avalonia.Media.TextDecorations.Underline : null;

        [ObservableProperty]
        private string _selectedFont = "Calibri";

        [ObservableProperty]
        private double _selectedFontSize = 11;

        // CORRECCIÓN 1: Usar nombre completo para evitar conflicto con la propiedad generada
        [ObservableProperty]
        private Avalonia.Media.TextAlignment _textAlignment = Avalonia.Media.TextAlignment.Left;

        public List<string> AvailableFonts { get; } = new() { "Calibri", "Arial", "Times New Roman", "Verdana", "Georgia", "Consolas", "Segoe UI" };
        public List<double> AvailableFontSizes { get; } = new() { 8, 9, 10, 11, 12, 14, 16, 18, 24, 36, 48, 72 };

        // --- Comandos ---
        [RelayCommand]
        private void ToggleBold() => IsBold = !IsBold;

        [RelayCommand]
        private void ToggleItalic() => IsItalic = !IsItalic;

        [RelayCommand]
        private void ToggleUnderline() => IsUnderline = !IsUnderline;

        [RelayCommand]
        private void SetAlignment(string alignment)
        {
            // CORRECCIÓN 2: Parsear explícitamente usando el tipo completo
            if (System.Enum.TryParse<Avalonia.Media.TextAlignment>(alignment, true, out var result))
            {
                TextAlignment = result;
            }
        }

        // CORRECCIÓN 3: Asegurar que el campo sea IBrush explícito
        [ObservableProperty]
        private IBrush _textColor = Brushes.Black;

        [RelayCommand]
        private void SetTextColor(string colorHex)
        {
            try
            {
                TextColor = Brush.Parse(colorHex);
            }
            catch
            {
                TextColor = Brushes.Black;
            }
        }
    }
}