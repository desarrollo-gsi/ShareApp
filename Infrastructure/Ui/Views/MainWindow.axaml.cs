using Avalonia.Controls;

namespace AvaloniaShareApp.Infrastructure.Ui.Views;

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

        this.PropertyChanged += OnWindowPropertyChanged;
    }

    private void OnWindowPropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Window.WindowStateProperty)
        {
            var rootBorder = this.FindControl<Border>("RootBorder");
            if (rootBorder != null)
            {
                if (WindowState == WindowState.Maximized)
                {
                    rootBorder.CornerRadius = new Avalonia.CornerRadius(0);
                    rootBorder.Margin = new Avalonia.Thickness(0);
                }
                else
                {
                    rootBorder.CornerRadius = new Avalonia.CornerRadius(20);
                    rootBorder.Margin = new Avalonia.Thickness(10);
                }
            }
        }
    }

    private void OnTitleBarPointerPressed(object sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }
}