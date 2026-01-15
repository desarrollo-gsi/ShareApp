using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaShareApp.ViewModels
{
    public partial class DocumentEditorViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _documentTitle = "Documento sin título";

        [ObservableProperty]
        private string _documentContent = "";

        [ObservableProperty]
        private bool _isBold;

        [ObservableProperty]
        private bool _isItalic;

        [ObservableProperty]
        private bool _isUnderline;

        [ObservableProperty]
        private string _selectedFont = "Calibri";

        [ObservableProperty]
        private int _selectedFontSize = 11;

        [ObservableProperty]
        private string _textAlignment = "Left";

        public string[] AvailableFonts { get; } = new[] { "Calibri", "Arial", "Times New Roman", "Verdana", "Georgia", "Consolas" };
        public int[] AvailableFontSizes { get; } = new[] { 8, 9, 10, 11, 12, 14, 16, 18, 20, 24, 28, 32, 36, 48, 72 };

        [RelayCommand]
        private void ToggleBold() => IsBold = !IsBold;

        [RelayCommand]
        private void ToggleItalic() => IsItalic = !IsItalic;

        [RelayCommand]
        private void ToggleUnderline() => IsUnderline = !IsUnderline;

        [RelayCommand]
        private void SetAlignment(string alignment) => TextAlignment = alignment;

        [ObservableProperty]
        private string _textColor = "#000000";

        public string[] AvailableColors { get; } = new[] { "#000000", "#EA4335", "#FBBC04", "#34A853", "#4285F4", "#9334E6" };

        [RelayCommand]
        private void SetTextColor(string color) => TextColor = color;
    }
}
