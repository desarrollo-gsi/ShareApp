using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using AvaloniaShareApp.Infrastructure.Ui.ViewModels;

namespace AvaloniaShareApp.Infrastructure.Ui.Views
{
    public partial class ExcelView : UserControl
    {
        public ExcelView()
        {
            InitializeComponent();
            
            DataContextChanged += (s, e) =>
            {
                if (DataContext is ExcelViewModel vm)
                {
                    var editorCtrl = this.FindControl<Controls.CustomSpreadsheetControl>("SpreadsheetEditor");
                    if (editorCtrl != null)
                    {
                        editorCtrl.CellChanged += (sender, cellRef) =>
                        {
                            var info = editorCtrl.GetCellInfo(cellRef);
                            var formulaBar = this.FindControl<TextBox>("FormulaBar");
                            if (formulaBar != null)
                            {
                                formulaBar.Text = !string.IsNullOrEmpty(info.Formula) ? info.Formula : info.Value;
                            }
                        };
                    }
                }
            };
        }

        private void OnBoldClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var editor = this.FindControl<Controls.CustomSpreadsheetControl>("SpreadsheetEditor");
            editor?.SetBold();
        }

        private void OnItalicClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var editor = this.FindControl<Controls.CustomSpreadsheetControl>("SpreadsheetEditor");
            editor?.SetItalic();
        }

        private void OnAlignLeftClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var editor = this.FindControl<Controls.CustomSpreadsheetControl>("SpreadsheetEditor");
            editor?.SetAlignment("Left");
        }

        private void OnAlignCenterClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var editor = this.FindControl<Controls.CustomSpreadsheetControl>("SpreadsheetEditor");
            editor?.SetAlignment("Center");
        }

        private void OnAlignRightClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var editor = this.FindControl<Controls.CustomSpreadsheetControl>("SpreadsheetEditor");
            editor?.SetAlignment("Right");
        }

        private void OnFillColorClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var editor = this.FindControl<Controls.CustomSpreadsheetControl>("SpreadsheetEditor");
            editor?.SetFillColor("#FFF2CC"); // Default light yellow fill
        }

        private void OnCurrencyClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var editor = this.FindControl<Controls.CustomSpreadsheetControl>("SpreadsheetEditor");
            editor?.SetFormat("Currency");
        }

        private void OnPercentClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var editor = this.FindControl<Controls.CustomSpreadsheetControl>("SpreadsheetEditor");
            editor?.SetFormat("Percent");
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
