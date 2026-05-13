using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Controls;
using System.Linq;

namespace AvaloniaShareApp.Infrastructure.Ui.ViewModels
{
    public partial class SettingsViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _selectedTheme;

        public SettingsViewModel()
        {
            // Detect current theme
            if (Avalonia.Application.Current != null)
            {
                if (Avalonia.Application.Current.RequestedThemeVariant == ThemeVariant.Light)
                    _selectedTheme = "Claro";
                else
                {
                    // Check if it's VS Code or Blue
                    if (Avalonia.Application.Current.Resources.TryGetValue("MainBackground", out var bg) && bg is Avalonia.Media.SolidColorBrush brush)
                    {
                        if (brush.Color.ToString() == "#FF1E1E1E")
                            _selectedTheme = "Oscuro (VS Code)";
                        else
                            _selectedTheme = "Oscuro (Azul)";
                    }
                    else
                    {
                        _selectedTheme = "Oscuro (Azul)";
                    }
                }
            }
            else
            {
                _selectedTheme = "Claro";
            }
        }

        [RelayCommand]
        private void ApplyTheme(string themeName)
        {
            SelectedTheme = themeName;
            if (Avalonia.Application.Current == null) return;

            if (themeName == "Claro")
            {
                Avalonia.Application.Current.RequestedThemeVariant = ThemeVariant.Light;
            }
            else if (themeName == "Oscuro (Azul)")
            {
                Avalonia.Application.Current.RequestedThemeVariant = ThemeVariant.Dark;
                // Ensure Blue Dark colors
                UpdateDarkTheme("#1E293B", "#0F172A", "#334155", "#3B82F6");
            }
            else if (themeName == "Oscuro (VS Code)")
            {
                Avalonia.Application.Current.RequestedThemeVariant = ThemeVariant.Dark;
                // Update Dark colors to VS Code style
                UpdateDarkTheme("#1E1E1E", "#252526", "#2D2D30", "#007ACC");
            }
        }

        private void UpdateDarkTheme(string mainBg, string sidebarBg, string secondaryBg, string accent)
        {
            if (Avalonia.Application.Current == null) return;
            
            if (Avalonia.Application.Current.Resources.ThemeDictionaries.TryGetValue(ThemeVariant.Dark, out var darkDictObj) && darkDictObj is ResourceDictionary darkDict)
            {
                darkDict["MainBackground"] = Avalonia.Media.SolidColorBrush.Parse(mainBg);
                darkDict["SidebarBackground"] = Avalonia.Media.SolidColorBrush.Parse(sidebarBg);
                darkDict["SecondaryBackground"] = Avalonia.Media.SolidColorBrush.Parse(secondaryBg);
                darkDict["NavButtonActive"] = Avalonia.Media.SolidColorBrush.Parse(accent);
                darkDict["NavButtonHover"] = Avalonia.Media.SolidColorBrush.Parse(secondaryBg);
            }
        }
    }
}
