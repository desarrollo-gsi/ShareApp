using Avalonia.Controls;

namespace AvaloniaShareApp.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        // Wire up window control buttons
        var minimizeBtn = this.FindControl<Button>("MinimizeButton");
        var maximizeBtn = this.FindControl<Button>("MaximizeButton");
        var closeBtn = this.FindControl<Button>("CloseButton");
        
        if (minimizeBtn != null)
            minimizeBtn.Click += (s, e) => WindowState = WindowState.Minimized;
        
        if (maximizeBtn != null)
            maximizeBtn.Click += (s, e) => 
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        
        if (closeBtn != null)
            closeBtn.Click += (s, e) => Close();
    }
}