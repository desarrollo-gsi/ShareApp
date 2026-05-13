using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaShareApp.Infrastructure.Ui.Controls;

namespace AvaloniaShareApp.Infrastructure.Ui.Views
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
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            if (DataContext is ViewModels.DocumentEditorViewModel vm)
            {
                vm.ExportPdfRequested -= OnExportPdfRequested;
                vm.ExportPdfRequested += OnExportPdfRequested;

                vm.InsertImageRequested -= OnInsertImageRequested;
                vm.InsertImageRequested += OnInsertImageRequested;

                vm.GetDocumentState = () => _editor?.GetDocument() ?? new Domain.Entities.Document();

                vm.DocumentLoaded -= OnDocumentLoaded;
                vm.DocumentLoaded += OnDocumentLoaded;

                // For simple actions, we can just bind commands directly in pure code-behind
                // Re-bind actions that just forward to _editor to avoid complex command parameters
                var btnBlankPage = this.FindControl<Button>("InsertBlankPageButton");
                if (btnBlankPage != null) btnBlankPage.Click += (s, args) => _editor?.InsertBlankPage();

                var btnPageBreak = this.FindControl<Button>("InsertPageBreakButton");
                if (btnPageBreak != null) btnPageBreak.Click += (s, args) => _editor?.InsertPageBreak();

                var btnTable = this.FindControl<Button>("InsertTableButton");
                if (btnTable != null) btnTable.Click += (s, args) => _editor?.InsertTable();
            }
        }
        
        private async void OnInsertImageRequested(object? sender, EventArgs e)
        {
            if (_editor == null) return;
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null || topLevel.StorageProvider == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Seleccionar Imagen",
                AllowMultiple = false,
                FileTypeFilter = new[] 
                { 
                    new Avalonia.Platform.Storage.FilePickerFileType("Images") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.gif", "*.bmp" } } 
                }
            });

            if (files != null && files.Count > 0)
            {
                _editor.InsertImage(files[0].Path.LocalPath);
            }
        }

        private async void OnExportPdfRequested(object? sender, EventArgs e)
        {
            if (_editor == null) return;
            
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null || topLevel.StorageProvider == null) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = "Exportar a PDF",
                DefaultExtension = "pdf",
                SuggestedFileName = "Documento.pdf",
                FileTypeChoices = new[] 
                { 
                    new Avalonia.Platform.Storage.FilePickerFileType("PDF Document") { Patterns = new[] { "*.pdf" } } 
                }
            });

            if (file != null)
            {
                try 
                {
                    await _editor.ExportToPdfAsync(file.Path.LocalPath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error al exportar PDF: {ex.Message}");
                }
            }
        }

        private void OnDocumentLoaded(object? sender, Domain.Entities.Document doc)
        {
            _editor?.LoadDocument(doc);
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            // Create editor and add to pages container
            var editorHost = this.FindControl<Grid>("EditorHost");
            if (editorHost != null)
            {
                _editor = new CustomRichTextEditor();
                editorHost.Children.Add(_editor);

                // --- Micro-interactions: Drag & Drop Support ---
                DragDrop.SetAllowDrop(_editor, true);
                _editor.AddHandler(DragDrop.DragOverEvent, OnDragOver);
                _editor.AddHandler(DragDrop.DropEvent, OnDrop);

                // --- Sync Toolbar with Selection ---
                _editor.SelectionChanged += OnSelectionChanged;
                
                // --- Sync Dirty State for Autosave ---
                _editor.DocumentChanged += (s, e) => 
                {
                    if (DataContext is ViewModels.DocumentEditorViewModel vm) 
                        vm.MarkAsDirty();
                };
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
                    _editor.Focus();
                };
            }

            if (_italicBtn != null)
            {
                _italicBtn.IsCheckedChanged += (s, args) =>
                {
                    _editor.ToggleItalicTo(_italicBtn.IsChecked == true);
                    _editor.Focus();
                };
            }

            if (_underlineBtn != null)
            {
                _underlineBtn.IsCheckedChanged += (s, args) =>
                {
                    _editor.ToggleUnderlineTo(_underlineBtn.IsChecked == true);
                    _editor.Focus();
                };
            }

            var undoBtn = this.FindControl<Button>("UndoButton");
            if (undoBtn != null) undoBtn.Click += (s, args) => _editor?.Undo();

            var redoBtn = this.FindControl<Button>("RedoButton");
            if (redoBtn != null) redoBtn.Click += (s, args) => _editor?.Redo();

            if (_strikethroughBtn != null)
            {
                _strikethroughBtn.IsCheckedChanged += (s, args) =>
                {
                    _editor.ToggleStrikethroughTo(_strikethroughBtn.IsChecked == true);
                    _editor.Focus();
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

            // Line Spacing
            var spacingCombo = this.FindControl<ComboBox>("LineSpacingCombo");
            if (spacingCombo != null)
            {
                spacingCombo.SelectionChanged += (s, args) =>
                {
                    if (spacingCombo.SelectedItem is ComboBoxItem item && item.Tag is string spacingStr)
                    {
                        if (double.TryParse(spacingStr, out double spacing))
                        {
                            _editor?.SetLineHeight(spacing);
                        }
                    }
                };
            }

            // Indentation
            var decreaseIndent = this.FindControl<Button>("DecreaseIndentButton");
            var increaseIndent = this.FindControl<Button>("IncreaseIndentButton");
            
            if (decreaseIndent != null)
            {
                decreaseIndent.Click += (s, args) => _editor?.ChangeIndent(-20);
            }
            
            if (increaseIndent != null)
            {
                increaseIndent.Click += (s, args) => _editor?.ChangeIndent(20);
            }

            // Status bar & Zoom
            _wordCountText = this.FindControl<TextBlock>("WordCountText");
            _zoomSlider = this.FindControl<Slider>("ZoomSlider");
            
            if (_zoomSlider != null)
            {
                _zoomSlider.ValueChanged += (s, args) =>
                {
                    if (_editor != null) _editor.Zoom = args.NewValue / 100.0;
                };
            }

            // Word Count Timer
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _updateTimer.Tick += (s, args) =>
            {
                if (_editor != null && _wordCountText != null)
                {
                    int count = _editor.GetWordCount();
                    _wordCountText.Text = $"{count} palabras";
                    
                    var pageText = this.FindControl<TextBlock>("PageInfoText");
                    if (pageText != null)
                        pageText.Text = $"Página {_editor.CurrentPageIndex + 1} de {_editor.TotalPages}";
                }
            };
            _updateTimer.Start();

            // Tabs switching logic
            var homeTab = this.FindControl<RadioButton>("HomeTab");
            var insertTab = this.FindControl<RadioButton>("InsertTab");
            var formatTab = this.FindControl<RadioButton>("FormatTab");
            var toolsTab = this.FindControl<RadioButton>("ToolsTab");
            var imageFormatTab = this.FindControl<RadioButton>("ImageFormatTab");

            var homeToolbar = this.FindControl<StackPanel>("HomeToolbar");
            var insertToolbar = this.FindControl<StackPanel>("InsertToolbar");
            var imageFormatToolbar = this.FindControl<StackPanel>("ImageFormatToolbar");

            void UpdateToolbars()
            {
                if (homeToolbar != null) homeToolbar.IsVisible = homeTab?.IsChecked == true || formatTab?.IsChecked == true || toolsTab?.IsChecked == true;
                if (insertToolbar != null) insertToolbar.IsVisible = insertTab?.IsChecked == true;
                if (imageFormatToolbar != null) imageFormatToolbar.IsVisible = imageFormatTab?.IsChecked == true;
            }

            if (homeTab != null) homeTab.IsCheckedChanged += (s, e) => UpdateToolbars();
            if (insertTab != null) insertTab.IsCheckedChanged += (s, e) => UpdateToolbars();
            if (formatTab != null) formatTab.IsCheckedChanged += (s, e) => UpdateToolbars();
            if (toolsTab != null) toolsTab.IsCheckedChanged += (s, e) => UpdateToolbars();
            if (imageFormatTab != null) imageFormatTab.IsCheckedChanged += (s, e) => UpdateToolbars();

            // Image Formatting
            _editor.ImageSelectionChanged += (s, selectedImage) =>
            {
                if (imageFormatTab != null)
                {
                    if (selectedImage != null)
                    {
                        imageFormatTab.IsVisible = true;
                        imageFormatTab.IsChecked = true;
                        UpdateToolbars(); // Ensure toolbar updates immediately
                    }
                    else
                    {
                        // Only switch back if we are currently on the Image Format tab
                        bool wasOnImageTab = imageFormatTab.IsChecked == true;
                        
                        imageFormatTab.IsVisible = false;
                        
                        if (wasOnImageTab && homeTab != null)
                        {
                            homeTab.IsChecked = true;
                            UpdateToolbars();
                        }
                    }
                }
            };

            var rotateLeftBtn = this.FindControl<Button>("RotateLeftButton");
            var rotateRightBtn = this.FindControl<Button>("RotateRightButton");
            var flipHBtn = this.FindControl<Button>("FlipHorizontalButton");
            var flipVBtn = this.FindControl<Button>("FlipVerticalButton");
            var bringForwardBtn = this.FindControl<Button>("BringForwardButton");
            var sendBackwardBtn = this.FindControl<Button>("SendBackwardButton");
            var cropBtn = this.FindControl<Button>("CropButton");

            if (rotateLeftBtn != null) rotateLeftBtn.Click += (s, e) => _editor.SelectedImage?.RotateLeft90();
            if (rotateRightBtn != null) rotateRightBtn.Click += (s, e) => _editor.SelectedImage?.RotateRight90();
            if (flipHBtn != null) flipHBtn.Click += (s, e) => _editor.SelectedImage?.FlipHorizontal();
            if (flipVBtn != null) flipVBtn.Click += (s, e) => _editor.SelectedImage?.FlipVertical();
            if (bringForwardBtn != null) bringForwardBtn.Click += (s, e) => { if (_editor.SelectedImage != null) _editor.BringImageToFront(_editor.SelectedImage); };
            if (sendBackwardBtn != null) sendBackwardBtn.Click += (s, e) => { if (_editor.SelectedImage != null) _editor.SendImageToBack(_editor.SelectedImage); };
            if (cropBtn != null) cropBtn.Click += (s, e) => _editor.SelectedImage?.ToggleCropMode();

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
                _editor.SetZoom(scale);
            }
        }

        private void UpdateWordCount()
        {
            if (_editor == null || _wordCountText == null) return;

            int wordCount = _editor.GetTotalWordCount();

            _wordCountText.Text = wordCount == 1 ? "1 palabra" : $"{wordCount} palabras";

            // Actualizar contador de páginas
            var pageInfoText = this.FindControl<TextBlock>("PageInfoText");
            if (pageInfoText != null)
            {
                pageInfoText.Text = $"Página {_editor.CurrentPage} de {_editor.PageCount}";
            }
        }

        private bool _isUpdatingToolbar = false;
        private void OnSelectionChanged(object? sender, EventArgs e)
        {
            if (_editor == null || _isUpdatingToolbar) return;

            _isUpdatingToolbar = true;
            try
            {
                var format = _editor.GetSelectionFormat();

                if (_boldBtn != null) _boldBtn.IsChecked = format.FontWeight == FontWeight.Bold;
                if (_italicBtn != null) _italicBtn.IsChecked = format.FontStyle == FontStyle.Italic;
                if (_underlineBtn != null) _underlineBtn.IsChecked = format.TextDecorations == TextDecorations.Underline;
                if (_strikethroughBtn != null) _strikethroughBtn.IsChecked = format.TextDecorations == TextDecorations.Strikethrough;

                // Alignment
                var alignLeft = this.FindControl<RadioButton>("AlignLeftButton");
                var alignCenter = this.FindControl<RadioButton>("AlignCenterButton");
                var alignRight = this.FindControl<RadioButton>("AlignRightButton");

                if (alignLeft != null) alignLeft.IsChecked = format.Alignment == TextAlignment.Left;
                if (alignCenter != null) alignCenter.IsChecked = format.Alignment == TextAlignment.Center;
                if (alignRight != null) alignRight.IsChecked = format.Alignment == TextAlignment.Right;

                // Font & Size
                var fontCombo = this.FindControl<ComboBox>("FontFamilyCombo");
                if (fontCombo != null && fontCombo.Items != null)
                {
                    foreach (var obj in fontCombo.Items)
                    {
                        if (obj is ComboBoxItem item && item.Tag?.ToString() == format.FontFamily.Name)
                        {
                            fontCombo.SelectedItem = item;
                            break;
                        }
                    }
                }

                var sizeCombo = this.FindControl<ComboBox>("FontSizeCombo");
                if (sizeCombo != null && sizeCombo.Items != null)
                {
                    foreach (var obj in sizeCombo.Items)
                    {
                        if (obj is ComboBoxItem item && item.Tag?.ToString() == format.FontSize.ToString())
                        {
                            sizeCombo.SelectedItem = item;
                            break;
                        }
                    }
                }
            }
            finally
            {
                _isUpdatingToolbar = false;
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
        private void OnDragOver(object? sender, DragEventArgs e)
        {
            if (e.Data.Contains("Files"))
            {
                e.DragEffects = DragDropEffects.Copy;
            }
            else
            {
                e.DragEffects = DragDropEffects.None;
            }
        }

        private void OnDrop(object? sender, DragEventArgs e)
        {
            var files = e.Data.GetFiles();
            if (files != null)
            {
                foreach (var file in files)
                {
                    var localPath = file.Path.LocalPath;
                    if (localPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                        localPath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                        localPath.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                    {
                        _editor?.InsertImage(localPath);
                    }
                }
            }
        }
    }
}
