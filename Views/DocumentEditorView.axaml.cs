using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaShareApp.Controls;

namespace AvaloniaShareApp.Views
{
    public partial class DocumentEditorView : UserControl
    {
        private CustomRichTextEditor? _editor;
        private ToggleButton? _boldBtn;
        private ToggleButton? _italicBtn;
        private ToggleButton? _underlineBtn;
        private ToggleButton? _strikethroughBtn;
        
        // Status bar
        private TextBlock? _wordCountText;
        private TextBlock? _zoomPercentText;
        private Slider? _zoomSlider;
        private DispatcherTimer? _updateTimer;

        public DocumentEditorView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            // Create editor and add to pages container
            var pagesContainer = this.FindControl<StackPanel>("PagesContainer");
            if (pagesContainer != null)
            {
                _editor = new CustomRichTextEditor();
                pagesContainer.Children.Add(_editor);
            }
            
            if (_editor == null) return;

            // ToggleButtons de formato
            _boldBtn = this.FindControl<ToggleButton>("BoldButton");
            _italicBtn = this.FindControl<ToggleButton>("ItalicButton");
            _underlineBtn = this.FindControl<ToggleButton>("UnderlineButton");
            _strikethroughBtn = this.FindControl<ToggleButton>("StrikethroughButton");

            // Eventos de ToggleButtons
            if (_boldBtn != null)
            {
                _boldBtn.IsCheckedChanged += (s, args) =>
                {
                    _editor.ToggleBoldTo(_boldBtn.IsChecked == true);
                };
            }
            
            if (_italicBtn != null)
            {
                _italicBtn.IsCheckedChanged += (s, args) =>
                {
                    _editor.ToggleItalicTo(_italicBtn.IsChecked == true);
                };
            }
            
            if (_underlineBtn != null)
            {
                _underlineBtn.IsCheckedChanged += (s, args) =>
                {
                    _editor.ToggleUnderlineTo(_underlineBtn.IsChecked == true);
                };
            }
            
            if (_strikethroughBtn != null)
            {
                _strikethroughBtn.IsCheckedChanged += (s, args) =>
                {
                    _editor.ToggleStrikethroughTo(_strikethroughBtn.IsChecked == true);
                };
            }
            
            // Conectar paleta de colores de texto
            var textColorPalette = this.FindControl<WrapPanel>("TextColorPalette");
            if (textColorPalette != null)
            {
                foreach (var child in textColorPalette.Children.OfType<Button>())
                {
                    child.Click += OnTextColorClick;
                }
            }
            
            // Conectar paleta de resaltado
            var highlightPalette = this.FindControl<WrapPanel>("HighlightColorPalette");
            if (highlightPalette != null)
            {
                foreach (var child in highlightPalette.Children.OfType<Button>())
                {
                    child.Click += OnHighlightColorClick;
                }
            }
            
            // Fuentes
            var fontCombo = this.FindControl<ComboBox>("FontFamilyCombo");
            if (fontCombo != null)
            {
                fontCombo.SelectionChanged += (s, args) =>
                {
                    if (fontCombo.SelectedItem is ComboBoxItem item && item.Tag is string fontName)
                    {
                        _editor?.SetFontFamily(new FontFamily(fontName));
                    }
                };
            }
            
            var sizeCombo = this.FindControl<ComboBox>("FontSizeCombo");
            if (sizeCombo != null)
            {
                sizeCombo.SelectionChanged += (s, args) =>
                {
                    if (sizeCombo.SelectedItem is ComboBoxItem item && item.Tag is string sizeStr)
                    {
                        if (double.TryParse(sizeStr, out double size))
                        {
                            _editor?.SetFontSize(size);
                        }
                    }
                };
            }
            
            // Alineación
            var alignLeft = this.FindControl<RadioButton>("AlignLeftButton");
            var alignCenter = this.FindControl<RadioButton>("AlignCenterButton");
            var alignRight = this.FindControl<RadioButton>("AlignRightButton");
            
            if (alignLeft != null)
            {
                alignLeft.IsCheckedChanged += (s, args) =>
                {
                    if (alignLeft.IsChecked == true)
                        _editor?.SetTextAlignment(Avalonia.Media.TextAlignment.Left);
                };
            }
            
            if (alignCenter != null)
            {
                alignCenter.IsCheckedChanged += (s, args) =>
                {
                    if (alignCenter.IsChecked == true)
                        _editor?.SetTextAlignment(Avalonia.Media.TextAlignment.Center);
                };
            }
            
            if (alignRight != null)
            {
                alignRight.IsCheckedChanged += (s, args) =>
                {
                    if (alignRight.IsChecked == true)
                        _editor?.SetTextAlignment(Avalonia.Media.TextAlignment.Right);
                };
            }
            
            // Status bar - Word count
            _wordCountText = this.FindControl<TextBlock>("WordCountText");
            
            // Zoom controls
            _zoomPercentText = this.FindControl<TextBlock>("ZoomPercentText");
            _zoomSlider = this.FindControl<Slider>("ZoomSlider");
            var zoomInBtn = this.FindControl<Button>("ZoomInButton");
            var zoomOutBtn = this.FindControl<Button>("ZoomOutButton");
            
            if (_zoomSlider != null)
            {
                _zoomSlider.PropertyChanged += (s, args) =>
                {
                    if (args.Property.Name == "Value")
                    {
                        UpdateZoom(_zoomSlider.Value);
                    }
                };
            }
            
            if (zoomInBtn != null)
            {
                zoomInBtn.Click += (s, args) =>
                {
                    if (_zoomSlider != null)
                    {
                        _zoomSlider.Value = Math.Min(200, _zoomSlider.Value + 10);
                    }
                };
            }
            
            if (zoomOutBtn != null)
            {
                zoomOutBtn.Click += (s, args) =>
                {
                    if (_zoomSlider != null)
                    {
                        _zoomSlider.Value = Math.Max(50, _zoomSlider.Value - 10);
                    }
                };
            }
            
            // Timer para actualizar conteo de palabras
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _updateTimer.Tick += (s, args) => UpdateWordCount();
            _updateTimer.Start();
        }

        private void UpdateZoom(double zoomPercent)
        {
            if (_zoomPercentText != null)
            {
                _zoomPercentText.Text = $"{(int)zoomPercent}%";
            }
            
            if (_editor != null)
            {
                double scale = zoomPercent / 100.0;
                _editor.RenderTransform = new ScaleTransform(scale, scale);
                _editor.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative);
            }
        }

        private void UpdateWordCount()
        {
            if (_editor == null || _wordCountText == null) return;
            
            string text = _editor.GetPlainText();
            int wordCount = 0;
            
            if (!string.IsNullOrWhiteSpace(text))
            {
                // Contar palabras separadas por espacios, tabs, newlines
                wordCount = text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
            }
            
            _wordCountText.Text = wordCount == 1 ? "1 palabra" : $"{wordCount} palabras";
            
            // Actualizar contador de páginas
            var pageInfoText = this.FindControl<TextBlock>("PageInfoText");
            if (pageInfoText != null)
            {
                pageInfoText.Text = $"Página {_editor.CurrentPage} de {_editor.PageCount}";
            }
        }

        private void OnTextColorClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string colorHex && _editor != null)
            {
                try
                {
                    _editor.SetForegroundColor(Brush.Parse(colorHex));
                    
                    var indicator = this.FindControl<Border>("TextColorIndicator");
                    if (indicator != null)
                    {
                        indicator.Background = Brush.Parse(colorHex);
                    }
                }
                catch { }
            }
        }

        private void OnHighlightColorClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string colorHex && _editor != null)
            {
                try
                {
                    IBrush? brush = colorHex == "Transparent" ? null : Brush.Parse(colorHex);
                    _editor.SetBackgroundColor(brush);
                    
                    var indicator = this.FindControl<Border>("HighlightColorIndicator");
                    if (indicator != null)
                    {
                        indicator.Background = colorHex == "Transparent" ? Brushes.Transparent : Brush.Parse(colorHex);
                    }
                }
                catch { }
            }
        }
    }
}
