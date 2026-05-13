using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Media.Imaging;
using Avalonia.Media.TextFormatting;
using System.Reflection;
using Avalonia.Utilities;
using AvaloniaShareApp.Domain.Entities;
namespace AvaloniaShareApp.Infrastructure.Ui.Controls
{
    public class CustomRichTextEditor : UserControl
    {
        private WrapPanel _pagesContainer;
        private bool _isDragging = false;
        private SelectionPoint? _selectionAnchor;

        public struct SelectionPoint
        {
            public int PageIndex;
            public int ParagraphIndex;
            public int CharIndex;

            public SelectionPoint(int page, int para, int ch)
            {
                PageIndex = page;
                ParagraphIndex = para;
                CharIndex = ch;
            }
            
            public bool IsBefore(SelectionPoint other)
            {
                if (PageIndex < other.PageIndex) return true;
                if (PageIndex > other.PageIndex) return false;
                if (ParagraphIndex < other.ParagraphIndex) return true;
                if (ParagraphIndex > other.ParagraphIndex) return false;
                return CharIndex < other.CharIndex;
            }
        }
        private List<PageControl> _pages = new();
        private int _currentPageIndex = 0;
        public int CurrentPageIndex => _currentPageIndex;
        public int TotalPages => _pages.Count;
        private double _currentZoom = 1.0;
        public double Zoom
        {
            get => _currentZoom;
            set
            {
                if (_currentZoom != value)
                {
                    _currentZoom = value;
                    foreach (var page in _pages)
                    {
                        page.Container.RenderTransform = new ScaleTransform(_currentZoom, _currentZoom);
                    }
                }
            }
        }
        private DocumentRuler _ruler;

        // Dimensiones de página (Letter size en píxeles a 96 DPI)
        private const double PageWidth = 816;  // 8.5 inches
        private const double PageHeight = 1056; // 11 inches
        private const double PagePadding = 96;  // 1 inch margins
        private const double ContentHeight = PageHeight - (PagePadding * 2); // ~864px

        // Formato actual
        public FontWeight CurrentFontWeight { get; set; } = FontWeight.Normal;
        public FontStyle CurrentFontStyle { get; set; } = FontStyle.Normal;
        public TextDecorationCollection? CurrentTextDecorations { get; set; }
        public IBrush CurrentForeground { get; set; } = Brushes.Black;
        public IBrush? CurrentBackground { get; set; }
        public FontFamily CurrentFontFamily { get; set; } = new FontFamily("Calibri");
        public double CurrentFontSize { get; set; } = 14;
        public TextAlignment CurrentAlignment { get; set; } = TextAlignment.Left;
        public double CurrentLineHeight { get; set; } = 1.2; // Default Word-like line spacing (multiplier)
        public double CurrentParagraphSpacing { get; set; } = 10; // Spacing after paragraph
        public double CurrentTextIndent { get; set; } = 0; // First line indent (sangría de primera línea)
        public double CurrentLeftIndent { get; set; } = 0; // Left indent (sangría de bloque)
        public double CurrentRightIndent { get; set; } = 0; // Right indent (sangría derecha)
        public IReadOnlyList<PageControl> Pages => _pages;

        public int PageCount => _pages.Count;
        public int CurrentPage => _currentPageIndex + 1;

        // Undo/Redo History
        private List<Document> _undoStack = new();
        private List<Document> _redoStack = new();
        private const int MaxHistory = 50;
        private bool _isApplyingHistory = false;

        // Caret
        private Border _caret;
        private Canvas _caretCanvas;
        private DispatcherTimer _caretTimer;
        private bool _caretVisible = true;
        
        // Image Selection State
        public SelectableImageControl? SelectedImage { get; private set; }
        public event EventHandler<SelectableImageControl?>? ImageSelectionChanged;
        public event EventHandler? SelectionChanged;
        public event EventHandler? DocumentChanged;


        public CustomRichTextEditor()
        {
            Background = Brushes.Transparent; // Ensure hit-testing works everywhere
            _pagesContainer = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            _ruler = new DocumentRuler(this);
            
            _caretCanvas = new Canvas { IsHitTestVisible = false };
            _caret = new Border
            {
                Width = 2,
                Background = Brushes.Black,
                IsVisible = false
            };
            _caretCanvas.Children.Add(_caret);

            // Wrap in Grid for caretaker overlay
            var pagesGrid = new Grid();
            pagesGrid.Children.Add(_pagesContainer);
            pagesGrid.Children.Add(_caretCanvas);

            // Host everything in a container that allows the ruler to stay at the top
            var mainContainer = new DockPanel();
            
            var rulerContainer = new Border
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Child = _ruler
            };
            DockPanel.SetDock(rulerContainer, Dock.Top);
            mainContainer.Children.Add(rulerContainer);

            var scrollViewer = new ScrollViewer
            {
                Background = Brushes.Transparent,
                Padding = new Thickness(40, 40, 40, 100),
                Content = pagesGrid
            };

            scrollViewer.ScrollChanged += (s, e) => _ruler.InvalidateVisual();

            // Guideline Overlay
            var guidelineCanvas = new Canvas { IsHitTestVisible = false };
            var guideline = new Border
            {
                Width = 1,
                Background = new SolidColorBrush(Color.FromArgb(128, 71, 85, 105)),
                IsVisible = false
            };
            guidelineCanvas.Children.Add(guideline);

            var editorStack = new Grid();
            editorStack.Children.Add(scrollViewer);
            editorStack.Children.Add(guidelineCanvas);

            mainContainer.Children.Add(editorStack);

            _ruler.OnGuidelineUpdate += (x, visible) =>
            {
                if (visible)
                {
                    var p = _ruler.TranslatePoint(new Point(x, 0), guidelineCanvas);
                    if (p.HasValue)
                    {
                        Canvas.SetLeft(guideline, p.Value.X);
                        guideline.Height = guidelineCanvas.Bounds.Height;
                        guideline.IsVisible = true;
                    }
                }
                else guideline.IsVisible = false;
            };

            Content = mainContainer;

            // Timer blinking
            _caretTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _caretTimer.Tick += (s, e) =>
            {
                _caretVisible = !_caretVisible;
                _caret.Opacity = _caretVisible ? 1 : 0;
            };
            _caretTimer.Start();

            // Initial Page
            AddNextPage();
        }

        public void SaveUndoState()
        {
            SaveHistory();
        }

        public void RequestSaveState()
        {
            SaveHistory();
        }



        public void UpdateCaretPosition()
        {
            // Reset blink
            _caretVisible = true;
            _caret.Opacity = 1;
            _caretTimer.Stop();
            _caretTimer.Start();

            if (_currentPageIndex < 0 || _currentPageIndex >= _pages.Count) return;
            var page = _pages[_currentPageIndex];

            try
            {
                var point = page.GetCaretPoint();
                if (point.HasValue)
                {
                    // Transform point from Page coordinates to Canvas coordinates
                    // The Page is inside _pagesContainer (StackPanel).
                    // We need the visual transform.

                    // If visual tree not ready, skip
                    if (TopLevel.GetTopLevel(page.Container) == null) return;

                    // The paragraph texts live inside _paragraphContainer which respects the 96px Padding
                    var transform = page.ParagraphContainer.TransformToVisual(_caretCanvas);
                    if (transform.HasValue)
                    {
                        var p = transform.Value.Transform(point.Value);
                        Canvas.SetLeft(_caret, p.X);
                        Canvas.SetTop(_caret, p.Y);
                        _caret.Height = page.GetCurrentLineHeight() * _currentZoom; // Adjust height to font size and zoom
                        _caret.Width = Math.Max(1.5, 2 * _currentZoom); // Scale caret thickness too
                        _caret.IsVisible = true;
                        return;
                    }
                }
            }
            catch { }
            _caret.IsVisible = false;
        }

        public void NotifySelectionChanged()
        {
            var format = GetSelectionFormat();
            CurrentFontWeight = format.FontWeight;
            CurrentFontStyle = format.FontStyle;
            CurrentTextDecorations = format.TextDecorations;
            CurrentFontFamily = format.FontFamily;
            CurrentFontSize = format.FontSize;
            CurrentForeground = format.Foreground;
            CurrentBackground = format.Background;
            CurrentAlignment = format.Alignment;

            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public SelectionFormat GetSelectionFormat()
        {
            var format = new SelectionFormat
            {
                FontWeight = CurrentFontWeight,
                FontStyle = CurrentFontStyle,
                TextDecorations = CurrentTextDecorations,
                FontFamily = CurrentFontFamily,
                FontSize = CurrentFontSize,
                Foreground = CurrentForeground,
                Background = CurrentBackground,
                Alignment = CurrentAlignment
            };

            if (_currentPageIndex >= 0 && _currentPageIndex < _pages.Count)
            {
                var page = _pages[_currentPageIndex];
                if (page.CurrentParagraphIndex >= 0 && page.CurrentParagraphIndex < page.Paragraphs.Count)
                {
                    var para = page.Paragraphs[page.CurrentParagraphIndex];
                    var tb = para.TextBlock;
                    
                    format.Alignment = para.Alignment;

                    // If there is selection, try to get format from selected runs
                    int start = Math.Min(tb.SelectionStart, tb.SelectionEnd);
                    if (start < tb.SelectionEnd)
                    {
                        // Get format of first selected character for simplicity
                        int runOffset = 0;
                        foreach (var run in para.Runs)
                        {
                            if (start >= runOffset && start < runOffset + run.Text.Length)
                            {
                                format.FontWeight = run.FontWeight;
                                format.FontStyle = run.FontStyle;
                                format.TextDecorations = run.TextDecorations;
                                format.FontFamily = run.FontFamily;
                                format.FontSize = run.FontSize;
                                format.Foreground = run.Foreground;
                                format.Background = run.Background;
                                break;
                            }
                            runOffset += run.Text.Length;
                        }
                    }
                    else
                    {
                        // Get format at caret position (from current run or editor defaults)
                        int runOffset = 0;
                        foreach (var run in para.Runs)
                        {
                            if (start > runOffset && start <= runOffset + run.Text.Length)
                            {
                                format.FontWeight = run.FontWeight;
                                format.FontStyle = run.FontStyle;
                                format.TextDecorations = run.TextDecorations;
                                format.FontFamily = run.FontFamily;
                                format.FontSize = run.FontSize;
                                format.Foreground = run.Foreground;
                                format.Background = run.Background;
                                break;
                            }
                            runOffset += run.Text.Length;
                        }
                    }
                }
            }

            return format;
        }

        public class SelectionFormat
        {
            public FontWeight FontWeight { get; set; }
            public FontStyle FontStyle { get; set; }
            public TextDecorationCollection? TextDecorations { get; set; }
            public FontFamily FontFamily { get; set; } = null!;
            public double FontSize { get; set; }
            public IBrush Foreground { get; set; } = null!;
            public IBrush? Background { get; set; }
            public TextAlignment Alignment { get; set; }
        }

        public void SetZoom(double scale)
        {
            _currentZoom = scale;
            foreach (var page in _pages)
            {
                var layoutTransform = page.Container as LayoutTransformControl;
                if (layoutTransform != null)
                {
                    layoutTransform.LayoutTransform = new ScaleTransform(scale, scale);
                }
            }
            _ruler.SetZoom(scale);
            UpdateCaretPosition();
        }
        public PageControl AddNextPage()
        {
            var page = new PageControl(this, _pages.Count);
            // Apply current zoom to the new page
            if (page.Container is LayoutTransformControl ltc)
            {
                ltc.LayoutTransform = new ScaleTransform(_currentZoom, _currentZoom);
            }
            _pages.Add(page);
            _pagesContainer.Children.Add(page.Container);
            _currentPageIndex = _pages.Count - 1;
            return page;
        }

        public void SetCurrentPage(int pageIndex)
        {
            if (pageIndex >= 0 && pageIndex < _pages.Count)
            {
                _currentPageIndex = pageIndex;
            }
        }

        public void SetSelectedImage(SelectableImageControl? image)
        {
            if (SelectedImage != null && SelectedImage != image)
            {
                SelectedImage.IsSelected = false;
            }

            SelectedImage = image;
            
            if (SelectedImage != null)
            {
                SelectedImage.IsSelected = true;
            }
            
            ImageSelectionChanged?.Invoke(this, SelectedImage);
        }

        public void BringImageToFront(SelectableImageControl image)
        {
            foreach (var page in _pages)
            {
                if (page.BackgroundCanvas.Children.Contains(image))
                {
                    page.BackgroundCanvas.Children.Remove(image);
                    page.ForegroundCanvas.Children.Add(image);
                    break;
                }
            }
        }

        public void SendImageToBack(SelectableImageControl image)
        {
            foreach (var page in _pages)
            {
                if (page.ForegroundCanvas.Children.Contains(image))
                {
                    page.ForegroundCanvas.Children.Remove(image);
                    page.BackgroundCanvas.Children.Add(image);
                    break;
                }
            }
        }

        public void UpdateCurrentPropertiesFromParagraph(Paragraph para)
        {
            CurrentAlignment = para.Alignment;
            CurrentLineHeight = para.LineSpacing;
            CurrentTextIndent = para.TextIndent;
            CurrentLeftIndent = para.LeftIndent;
            CurrentRightIndent = para.RightIndent;
            _ruler.InvalidateVisual();
        }

        public void CheckPageOverflow(int pageIndex)
        {
            if (pageIndex < 0 || pageIndex >= _pages.Count) return;

            // Usamos Dispatcher con prioridad baja para esperar a que el layout se actualice
            Dispatcher.UIThread.Post(() =>
            {
                if (pageIndex >= _pages.Count) return;

                var page = _pages[pageIndex];

                // Si la altura del contenido es mayor que el espacio disponible
                if (page.GetContentHeight() > ContentHeight)
                {
                    // Si es la última página, crear una nueva
                    PageControl targetPage;
                    if (pageIndex == _pages.Count - 1)
                    {
                        targetPage = AddNextPage();
                    }
                    else
                    {
                        targetPage = _pages[pageIndex + 1];
                    }

                    // Mover el último párrafo a la siguiente página
                    page.MoveLastParagraphTo(targetPage);
                }
            }, DispatcherPriority.Input);
        }

        // --- MÉTODOS DE INSERCIÓN (INSERT TAB) ---
        
        public void InsertBlankPage()
        {
            // Simply append a new empty page at the end of the document
            // Option: Insert it immediately after the current page instead of at the end. Let's do after current.
            var newPage = new PageControl(this, _currentPageIndex + 1);
            if (newPage.Container is LayoutTransformControl ltc)
            {
                ltc.LayoutTransform = new ScaleTransform(_currentZoom, _currentZoom);
            }
            
            _pages.Insert(_currentPageIndex + 1, newPage);
            _pagesContainer.Children.Insert(_currentPageIndex + 1, newPage.Container);
            
            // Re-index subsequent pages
            for (int i = _currentPageIndex + 1; i < _pages.Count; i++)
            {
                _pages[i].PageIndex = i;
            }
            
            // Set focus to the new page
            _currentPageIndex++;
            UpdateCaretPosition();
        }

        public void InsertPageBreak()
        {
            if (_currentPageIndex < 0 || _currentPageIndex >= _pages.Count) return;

            var currentPage = _pages[_currentPageIndex];
            
            // 1. Split current paragraph at caret
            currentPage.SplitAtCaretForPageBreak();

            // 2. Insert new page immediately after current
            InsertBlankPageAt(_currentPageIndex + 1);
            var nextPage = _pages[_currentPageIndex + 1];

            // 3. Move all paragraphs from the split point onwards to the new page
            currentPage.MoveContentFromSplitTo(nextPage);

            // 4. Force layout update and caret position
            UpdateCaretPosition();
        }

        public void InsertBlankPageAt(int index)
        {
            var newPage = new PageControl(this, index);
            if (newPage.Container is LayoutTransformControl ltc)
            {
                ltc.LayoutTransform = new ScaleTransform(_currentZoom, _currentZoom);
            }
            
            _pages.Insert(index, newPage);
            _pagesContainer.Children.Insert(index, newPage.Container);
            
            // Re-index subsequent pages
            for (int i = index + 1; i < _pages.Count; i++)
            {
                _pages[i].PageIndex = i;
            }
            
            _currentPageIndex = index;
        }

        public void InsertTable()
        {
            var page = _pages[_currentPageIndex];
            page.InsertTable();
            CheckPageOverflow(_currentPageIndex);
            UpdateCaretPosition();
        }

        public void InsertImage(string filePath)
        {
            try
            {
                var bitmap = new Avalonia.Media.Imaging.Bitmap(filePath);
                var imageControl = new SelectableImageControl(bitmap);
                imageControl.FilePath = filePath;
                
                imageControl.Selected += (s, e) => 
                {
                    SetSelectedImage(imageControl);
                };

                imageControl.ImageDragged += OnImageDragged;

                if (_currentPageIndex >= 0 && _currentPageIndex < _pages.Count)
                {
                    var page = _pages[_currentPageIndex];
                    
                    Canvas.SetLeft(imageControl, 50);
                    Canvas.SetTop(imageControl, 50);
                    
                    page.ForegroundCanvas.Children.Add(imageControl);
                    SetSelectedImage(imageControl);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error insertando imagen: {ex.Message}");
            }
        }

        private void OnImageDragged(object? sender, ImageDragEventArgs e)
        {
            if (sender is SelectableImageControl imageControl)
            {
                double left = double.IsNaN(Canvas.GetLeft(imageControl)) ? 0 : Canvas.GetLeft(imageControl);
                double top = double.IsNaN(Canvas.GetTop(imageControl)) ? 0 : Canvas.GetTop(imageControl);

                left += e.DeltaX;
                top += e.DeltaY;

                Canvas.SetLeft(imageControl, left);
                Canvas.SetTop(imageControl, top);

                PageControl? sourcePage = null;
                bool isForeground = true;

                foreach (var p in _pages)
                {
                    if (p.ForegroundCanvas.Children.Contains(imageControl))
                    {
                        sourcePage = p;
                        isForeground = true;
                        break;
                    }
                    if (p.BackgroundCanvas.Children.Contains(imageControl))
                    {
                        sourcePage = p;
                        isForeground = false;
                        break;
                    }
                }

                if (sourcePage == null) return;

                int pageIndex = sourcePage.PageIndex;
                double canvasHeight = 1056 - (96 * 2); // 864, the height of the padded content area

                if (top > canvasHeight && pageIndex < _pages.Count - 1)
                {
                    if (isForeground) sourcePage.ForegroundCanvas.Children.Remove(imageControl);
                    else sourcePage.BackgroundCanvas.Children.Remove(imageControl);

                    var nextPage = _pages[pageIndex + 1];
                    Canvas.SetTop(imageControl, top - canvasHeight);
                    
                    if (isForeground) nextPage.ForegroundCanvas.Children.Add(imageControl);
                    else nextPage.BackgroundCanvas.Children.Add(imageControl);
                }
                else if (top < 0 && pageIndex > 0)
                {
                    if (isForeground) sourcePage.ForegroundCanvas.Children.Remove(imageControl);
                    else sourcePage.BackgroundCanvas.Children.Remove(imageControl);

                    var prevPage = _pages[pageIndex - 1];
                    Canvas.SetTop(imageControl, top + canvasHeight);
                    
                    if (isForeground) prevPage.ForegroundCanvas.Children.Add(imageControl);
                    else prevPage.BackgroundCanvas.Children.Add(imageControl);
                }
            }
        }

        // Métodos de formato
        public void ToggleBold() => CurrentPage_Action(p => p.ToggleBold());
        public void ToggleBoldTo(bool isBold)
        {
            SaveUndoState();
            CurrentFontWeight = isBold ? FontWeight.Bold : FontWeight.Normal;
            CurrentPage_Action(p => p.ApplyCurrentFormat());
        }

        public void ToggleItalic() => CurrentPage_Action(p => p.ToggleItalic());
        public void ToggleItalicTo(bool isItalic)
        {
            SaveUndoState();
            CurrentFontStyle = isItalic ? FontStyle.Italic : FontStyle.Normal;
            CurrentPage_Action(p => p.ApplyCurrentFormat());
        }

        public void ToggleUnderline() => CurrentPage_Action(p => p.ToggleUnderline());
        public void ToggleUnderlineTo(bool isUnderline)
        {
            SaveUndoState();
            CurrentTextDecorations = isUnderline ? TextDecorations.Underline : null;
            CurrentPage_Action(p => p.ApplyCurrentFormat());
        }

        public void ToggleStrikethroughTo(bool isStrikethrough)
        {
            CurrentTextDecorations = isStrikethrough ? TextDecorations.Strikethrough : null;
            CurrentPage_Action(p => p.ApplyCurrentFormat());
        }

        public void SetForegroundColor(IBrush color)
        {
            CurrentForeground = color;
            CurrentPage_Action(p => p.ApplyCurrentFormat());
        }

        public void SetBackgroundColor(IBrush? color)
        {
            CurrentBackground = color;
            CurrentPage_Action(p => p.ApplyCurrentFormat());
        }

        public void SetFontFamily(FontFamily font)
        {
            SaveUndoState();
            CurrentFontFamily = font;
            CurrentPage_Action(p => p.ApplyCurrentFormat());
        }

        public void SetFontSize(double size)
        {
            SaveUndoState();
            CurrentFontSize = size;
            CurrentPage_Action(p => p.ApplyCurrentFormat());
        }

        public void SetTextAlignment(TextAlignment alignment)
        {
            CurrentAlignment = alignment;
            CurrentPage_Action(p => p.SetAlignment(alignment));
        }

        public void SetLineHeight(double lineHeight)
        {
            CurrentLineHeight = lineHeight;
            CurrentPage_Action(p => p.SetLineHeight(lineHeight));
        }

        public void SetTextIndent(double indent)
        {
            CurrentTextIndent = indent;
            CurrentPage_Action(p => p.SetTextIndent(indent));
        }

        public void SetLeftIndent(double indent)
        {
            CurrentLeftIndent = indent;
            CurrentPage_Action(p => p.SetLeftIndent(indent));
        }

        public void SetRightIndent(double indent)
        {
            CurrentRightIndent = indent;
            CurrentPage_Action(p => p.SetRightIndent(indent));
        }

        public void ChangeIndent(double delta)
        {
            CurrentLeftIndent = Math.Max(0, CurrentLeftIndent + delta);
            CurrentPage_Action(p => p.SetLeftIndent(CurrentLeftIndent));
        }

        private void CurrentPage_Action(Action<PageControl> action)
        {
            // Apply to all pages to ensure selection on any page is handled
            // If action is specific (e.g. alignment), it sets global state then applies to selection/current cursor.
            // PageControl logic handles "HasSelection" checks.
            foreach (var page in _pages) action(page);
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            if (!_isDragging) return;

            var point = e.GetPosition(_pagesContainer);
            int targetPageIndex = -1;
            double minDistance = double.MaxValue;

            for (int i = 0; i < _pages.Count; i++)
            {
                var p = _pages[i];
                var pageBounds = p.Container.Bounds;
                
                // 1. Direct hit check
                if (point.X >= pageBounds.Left && point.X <= pageBounds.Right &&
                    point.Y >= pageBounds.Top && point.Y <= pageBounds.Bottom)
                {
                    targetPageIndex = i;
                    break;
                }
                
                // 2. Proximity check (fallback)
                double dist = Math.Abs(point.Y - pageBounds.Center.Y);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    targetPageIndex = i;
                }
            }

            if (targetPageIndex != -1)
            {
                var page = _pages[targetPageIndex];
                var pointOnPage = e.GetPosition(page.Container);
                int targetParaIndex = page.GetParagraphIndexAt(pointOnPage.Y);
                
                int charIdx = 0;
                if (targetParaIndex >= page.Paragraphs.Count)
                {
                    targetParaIndex = page.Paragraphs.Count - 1;
                    charIdx = page.Paragraphs[targetParaIndex].TextBlock.Text?.Length ?? 0;
                }
                else if (targetParaIndex < 0)
                {
                    targetParaIndex = 0;
                    charIdx = 0;
                }
                else
                {
                    var para = page.Paragraphs[targetParaIndex];
                    var pointOnPara = e.GetPosition(para.TextBlock);
                    charIdx = para.TextBlock.GetCaretIndexAt(pointOnPara);
                    
                // Update current paragraph for caret
                    page.SetCurrentParagraph(targetParaIndex);
                }

                var target = new SelectionPoint(targetPageIndex, targetParaIndex, charIdx);
                UpdateDragSelection(target);
                
                // Keep caret updated during drag
                UpdateCaretPosition();

                // Word-like Auto-Scroll
                HandleAutoScroll(e.GetPosition(this));
            }
        }

        private void HandleAutoScroll(Point mousePos)
        {
            if (Parent is ScrollViewer sv)
            {
                double threshold = 40;
                double scrollSpeed = 15;
                
                if (mousePos.Y < threshold)
                    sv.Offset = new Vector(sv.Offset.X, Math.Max(0, sv.Offset.Y - scrollSpeed));
                else if (mousePos.Y > Bounds.Height - threshold)
                    sv.Offset = new Vector(sv.Offset.X, Math.Min(sv.Extent.Height - sv.Viewport.Height, sv.Offset.Y + scrollSpeed));
            }
        }

        public void SelectAll()
        {
            foreach (var page in _pages)
            {
                page.SelectAll();
            }
        }

        public void StartDragSelection(int pageIndex, int paragraphIndex, int charIndex)
        {
            _isDragging = true;
            _selectionAnchor = new SelectionPoint(pageIndex, paragraphIndex, charIndex);
        }

        public void UpdateDragSelection(SelectionPoint target)
        {
            if (!_isDragging || _selectionAnchor == null) return;
            
            var anchor = _selectionAnchor.Value;
            
            // Determine range
            SelectionPoint start, end;
            if (anchor.IsBefore(target))
            {
                start = anchor;
                end = target;
            }
            else
            {
                start = target;
                end = anchor;
            }

            // Update all pages
            foreach (var page in _pages)
            {
                page.UpdateSelectionRange(start, end);
            }
            
            NotifySelectionChanged();
        }

        public void EndDragSelection()
        {
            _isDragging = false;
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            if (_isDragging)
            {
                EndDragSelection();
                e.Pointer.Capture(null);
            }
        }

        public void HandlePageSelection(int pageIndex, int paragraphIndex, int charIndex, bool isShiftPressed)
        {
            if (!isShiftPressed || _selectionAnchor == null)
            {
                _selectionAnchor = new SelectionPoint(pageIndex, paragraphIndex, charIndex);
                foreach (var page in _pages)
                {
                    if (page.PageIndex != pageIndex)
                    {
                        page.ClearSelection();
                    }
                    else
                    {
                        page.ClearSelectionExcept(paragraphIndex);
                    }
                }
                return;
            }

            var anchor = _selectionAnchor.Value;
            var startPageIdx = anchor.PageIndex;
            var startParaIdx = anchor.ParagraphIndex;
            var startCharIdx = anchor.CharIndex;
            
            var endPageIdx = pageIndex;
            var endParaIdx = paragraphIndex;
            var endCharIdx = charIndex;

            // Normalize range for UpdateSelectionRange (it already does normalization, but good to have anchor)
            UpdateDragSelection(new SelectionPoint(endPageIdx, endParaIdx, endCharIdx));
        }

        public int GetWordCount()
        {
            int count = 0;
            foreach (var page in _pages)
            {
                foreach (var para in page.Paragraphs)
                {
                    var text = para.TextBlock.Text;
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    
                    var words = text.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    count += words.Length;
                }
            }
            return count;
        }

        public void ScrollToCaret()
        {
            if (_currentPageIndex < 0 || _currentPageIndex >= _pages.Count) return;
            var point = _pages[_currentPageIndex].GetCaretPoint();
            
            if (point != null && Parent is ScrollViewer sv)
            {
                // Translate point to scrollviewer coordinates
                var p = point.Value;
                // Since pages are in a WrapPanel, we need to find the page's position
                var page = _pages[_currentPageIndex];
                var pagePos = page.Container.Bounds.TopLeft;
                
                double globalY = pagePos.Y + p.Y;
                
                double viewportY = sv.Offset.Y;
                double viewportHeight = sv.Viewport.Height;

                if (globalY < viewportY)
                    sv.Offset = new Vector(sv.Offset.X, Math.Max(0, globalY - 50));
                else if (globalY + 20 > viewportY + viewportHeight)
                    sv.Offset = new Vector(sv.Offset.X, globalY - viewportHeight + 70);
            }
        }

        public SelectionPoint GetCurrentCaretSelectionPoint()
        {
            if (_currentPageIndex < 0 || _currentPageIndex >= _pages.Count) return new SelectionPoint(0, 0, 0);
            var page = _pages[_currentPageIndex];
            int paraIdx = page.GetCurrentParagraphIndex();
            int charIdx = page.GetCurrentCharIndex();
            return new SelectionPoint(_currentPageIndex, paraIdx, charIdx);
        }

        public Document GetDocument()
        {
            var doc = new Document();
            foreach (var pageControl in _pages)
            {
                var domainPage = new Domain.Entities.Page();
                
                // 1. Capture Ordered Content (Paragraphs and Tables)
                foreach (var child in pageControl.ParagraphContainer.Children)
                {
                    if (child is SelectableTextBlock tb)
                    {
                        var paraControl = pageControl.Paragraphs.FirstOrDefault(p => p.TextBlock == tb);
                        if (paraControl != null)
                        {
                            var domainPara = new Domain.Entities.Paragraph
                            {
                                Alignment = paraControl.TextBlock.TextAlignment.ToString(),
                                LineSpacing = paraControl.LineSpacing,
                                ParagraphSpacing = paraControl.ParagraphSpacing,
                                LeftIndent = paraControl.LeftIndent,
                                RightIndent = paraControl.RightIndent,
                                TextIndent = paraControl.TextIndent
                            };
                            foreach (var run in paraControl.Runs)
                            {
                                domainPara.Runs.Add(new Domain.Entities.TextRun
                                {
                                    Text = run.Text,
                                    FontFamily = run.FontFamily.Name,
                                    FontSize = run.FontSize,
                                    FontWeight = run.FontWeight.ToString(),
                                    FontStyle = run.FontStyle.ToString(),
                                    ForegroundColor = run.Foreground?.ToString() ?? "#000000",
                                    IsUnderlined = run.TextDecorations != null
                                });
                            }
                            domainPage.Content.Add(new Domain.Entities.ContentItem { Type = "Paragraph", Paragraph = domainPara });
                        }
                    }
                    else if (child is Grid grid)
                    {
                        var table = new Domain.Entities.TableEntity
                        {
                            Columns = grid.ColumnDefinitions.Count,
                            Rows = grid.RowDefinitions.Count,
                            MarginTop = grid.Margin.Top,
                            MarginBottom = grid.Margin.Bottom
                        };
                        foreach (var cellChild in grid.Children)
                        {
                            if (cellChild is Border cellBorder && cellBorder.Child is SelectableTextBlock cellTb)
                            {
                                table.Cells.Add(new Domain.Entities.TableCell
                                {
                                    Row = Grid.GetRow(cellBorder),
                                    Column = Grid.GetColumn(cellBorder),
                                    Text = cellTb.Text ?? "",
                                    BackgroundColor = cellBorder.Background?.ToString() ?? "Transparent"
                                });
                            }
                        }
                        domainPage.Content.Add(new Domain.Entities.ContentItem { Type = "Table", Table = table });
                    }
                }

                // 2. Capture Floating Images
                CaptureImages(pageControl.BackgroundCanvas, domainPage.FloatingImages, true);
                CaptureImages(pageControl.ForegroundCanvas, domainPage.FloatingImages, false);

                doc.Pages.Add(domainPage);
            }
            return doc;
        }

        private void CaptureImages(Canvas canvas, List<Domain.Entities.ImageEntity> list, bool isBackground)
        {
            foreach (var child in canvas.Children)
            {
                if (child is SelectableImageControl img)
                {
                    list.Add(new Domain.Entities.ImageEntity
                    {
                        Path = img.FilePath ?? "",
                        X = Canvas.GetLeft(img),
                        Y = Canvas.GetTop(img),
                        Width = img.Width,
                        Height = img.Height,
                        Rotation = img.Rotation,
                        IsBackground = isBackground
                    });
                }
            }
        }

        public void LoadDocument(Document doc)
        {
            _pages.Clear();
            _pagesContainer.Children.Clear();

            if (doc.Pages.Count == 0)
            {
                InsertBlankPage();
                return;
            }

            int pageIdx = 0;
            foreach (var domainPage in doc.Pages)
            {
                var pageControl = new PageControl(this, pageIdx++);
                
                // Clear default contents
                pageControl.Paragraphs.Clear();
                pageControl.ParagraphContainer.Children.Clear();

                // 1. Restore Ordered Content
                foreach (var item in domainPage.Content)
                {
                    if (item.Type == "Paragraph" && item.Paragraph != null)
                    {
                        var domainPara = item.Paragraph;
                        var para = new Paragraph
                        {
                            TextBlock = pageControl.CreateDefaultTextBlock(),
                            LineSpacing = domainPara.LineSpacing,
                            ParagraphSpacing = domainPara.ParagraphSpacing,
                            LeftIndent = domainPara.LeftIndent,
                            RightIndent = domainPara.RightIndent,
                            TextIndent = domainPara.TextIndent,
                            Alignment = Enum.TryParse<TextAlignment>(domainPara.Alignment, true, out var align) ? align : TextAlignment.Left
                        };
                        
                        para.TextBlock.TextAlignment = para.Alignment;

                        foreach (var domainRun in domainPara.Runs)
                        {
                            var run = new FormattedRun
                            {
                                Text = domainRun.Text,
                                FontSize = domainRun.FontSize,
                                FontFamily = new FontFamily(domainRun.FontFamily),
                                FontWeight = Enum.TryParse<FontWeight>(domainRun.FontWeight, true, out var w) ? w : FontWeight.Normal,
                                FontStyle = Enum.TryParse<FontStyle>(domainRun.FontStyle, true, out var s) ? s : FontStyle.Normal,
                                Foreground = Brush.Parse(domainRun.ForegroundColor)
                            };
                            if (domainRun.IsUnderlined) run.TextDecorations = TextDecorations.Underline;
                            para.Runs.Add(run);
                        }
                        
                        pageControl.Paragraphs.Add(para);
                        pageControl.ParagraphContainer.Children.Add(para.TextBlock);
                        pageControl.RefreshParagraph(pageControl.Paragraphs.Count - 1);
                    }
                    else if (item.Type == "Table" && item.Table != null)
                    {
                        var domainTable = item.Table;
                        var grid = new Grid
                        {
                            Margin = new Thickness(0, domainTable.MarginTop, 0, domainTable.MarginBottom),
                            Background = Brushes.Transparent,
                            ShowGridLines = true
                        };

                        // Reconstruct columns and rows
                        for (int i = 0; i < domainTable.Columns; i++) grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
                        for (int i = 0; i < domainTable.Rows; i++) grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

                        foreach (var domainCell in domainTable.Cells)
                        {
                            var cellBorder = new Border
                            {
                                BorderBrush = Brushes.LightGray,
                                BorderThickness = new Thickness(1),
                                Padding = new Thickness(5),
                                Background = Brush.Parse(domainCell.BackgroundColor),
                                Child = new SelectableTextBlock 
                                { 
                                    Text = domainCell.Text,
                                    TextWrapping = TextWrapping.Wrap,
                                    FontFamily = CurrentFontFamily,
                                    FontSize = CurrentFontSize
                                }
                            };
                            Grid.SetRow(cellBorder, domainCell.Row);
                            Grid.SetColumn(cellBorder, domainCell.Column);
                            grid.Children.Add(cellBorder);
                        }
                        pageControl.ParagraphContainer.Children.Add(grid);
                    }
                }

                // 2. Restore Floating Images
                foreach (var domainImg in domainPage.FloatingImages)
                {
                    try
                    {
                        var bitmap = new Avalonia.Media.Imaging.Bitmap(domainImg.Path);
                        var imageControl = new SelectableImageControl(bitmap)
                        {
                            FilePath = domainImg.Path,
                            Width = domainImg.Width,
                            Height = domainImg.Height,
                            Rotation = domainImg.Rotation
                        };
                        
                        imageControl.Selected += (s, e) => SetSelectedImage(imageControl);
                        imageControl.ImageDragged += OnImageDragged;

                        Canvas.SetLeft(imageControl, domainImg.X);
                        Canvas.SetTop(imageControl, domainImg.Y);

                        if (domainImg.IsBackground) pageControl.BackgroundCanvas.Children.Add(imageControl);
                        else pageControl.ForegroundCanvas.Children.Add(imageControl);
                    }
                    catch { /* Skip missing images */ }
                }

                _pages.Add(pageControl);
                _pagesContainer.Children.Add(pageControl.Container);
                pageControl.Container.RenderTransform = new ScaleTransform(_currentZoom, _currentZoom);
            }

            _currentPageIndex = 0;
            UpdateCaretPosition();
        }

        public void SaveHistory()
        {
            if (_isApplyingHistory) return;
            
            // Capture current state
            var snapshot = GetDocument();
            _undoStack.Add(snapshot);
            
            if (_undoStack.Count > MaxHistory)
                _undoStack.RemoveAt(0);
                
            _redoStack.Clear();

            DocumentChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Undo()
        {
            if (_undoStack.Count == 0) return;

            _isApplyingHistory = true;
            
            // Save current to redo
            _redoStack.Add(GetDocument());
            
            // Pop from undo
            var previous = _undoStack[_undoStack.Count - 1];
            _undoStack.RemoveAt(_undoStack.Count - 1);
            
            LoadDocument(previous);
            
            _isApplyingHistory = false;
        }

        public void Redo()
        {
            if (_redoStack.Count == 0) return;

            _isApplyingHistory = true;
            
            // Save current to undo
            _undoStack.Add(GetDocument());
            
            // Pop from redo
            var next = _redoStack[_redoStack.Count - 1];
            _redoStack.RemoveAt(_redoStack.Count - 1);
            
            LoadDocument(next);
            
            _isApplyingHistory = false;
        }

        public void MoveCaret(SelectionPoint newPoint, bool select)
        {
            if (newPoint.PageIndex < 0 || newPoint.PageIndex >= _pages.Count) return;
            
            _currentPageIndex = newPoint.PageIndex;
            var page = _pages[_currentPageIndex];
            page.SetCurrentParagraph(newPoint.ParagraphIndex);
            page.SetCurrentCharIndex(newPoint.CharIndex);

            if (select)
            {
                UpdateDragSelection(newPoint);
            }
            else
            {
                // Clear selection and set anchor to new position
                _selectionAnchor = newPoint;
                foreach (var p in _pages) p.ClearSelection();
            }

            UpdateCaretPosition();
        }

        public void HandleNavigation(Key key, bool isShiftPressed)
        {
            var current = GetCurrentCaretSelectionPoint();
            var target = current;
            var page = _pages[current.PageIndex];
            var para = page.Paragraphs[current.ParagraphIndex];
            var tb = para.TextBlock;
            var text = tb.Text ?? "";


            switch (key)
            {
                case Key.Left:
                    if (current.CharIndex > 0) target.CharIndex--;
                    else if (current.ParagraphIndex > 0)
                    {
                        target.ParagraphIndex--;
                        target.CharIndex = page.Paragraphs[target.ParagraphIndex].TextBlock.Text?.Length ?? 0;
                    }
                    else if (current.PageIndex > 0)
                    {
                        target.PageIndex--;
                        var prevPage = _pages[target.PageIndex];
                        target.ParagraphIndex = prevPage.Paragraphs.Count - 1;
                        target.CharIndex = prevPage.Paragraphs[target.ParagraphIndex].TextBlock.Text?.Length ?? 0;
                    }
                    break;

                case Key.Right:
                    if (current.CharIndex < text.Length) target.CharIndex++;
                    else if (current.ParagraphIndex < page.Paragraphs.Count - 1)
                    {
                        target.ParagraphIndex++;
                        target.CharIndex = 0;
                    }
                    else if (current.PageIndex < _pages.Count - 1)
                    {
                        target.PageIndex++;
                        target.ParagraphIndex = 0;
                        target.CharIndex = 0;
                    }
                    break;

                case Key.Home:
                    {
                        var layout = page.CreateCaretLayout(tb);
                        int lineStart = 0;
                        foreach (var line in layout.TextLines)
                        {
                            int lineEnd = lineStart + line.Length;
                            if (current.CharIndex >= lineStart && current.CharIndex <= lineEnd)
                            {
                                target.CharIndex = lineStart;
                                break;
                            }
                            lineStart = lineEnd;
                        }
                    }
                    break;

                case Key.End:
                    {
                        var layout = page.CreateCaretLayout(tb);
                        int lineStart = 0;
                        foreach (var line in layout.TextLines)
                        {
                            int lineEnd = lineStart + line.Length;
                            if (current.CharIndex >= lineStart && current.CharIndex <= lineEnd)
                            {
                                target.CharIndex = lineEnd;
                                if (target.CharIndex > text.Length) target.CharIndex = text.Length;
                                break;
                            }
                            lineStart = lineEnd;
                        }
                    }
                    break;

                case Key.Up:
                    {
                        var layout = page.CreateCaretLayout(tb);
                        var caretRect = layout.HitTestTextPosition(current.CharIndex);
                        double targetY = caretRect.Y - 1; 
                        if (targetY >= 0)
                        {
                            var hit = layout.HitTestPoint(new Point(caretRect.X, targetY));
                            target.CharIndex = hit.TextPosition;
                        }
                        else if (current.ParagraphIndex > 0)
                        {
                            target.ParagraphIndex--;
                            var prevPara = page.Paragraphs[target.ParagraphIndex].TextBlock;
                            var prevLayout = page.CreateCaretLayout(prevPara);
                            var prevHit = prevLayout.HitTestPoint(new Point(caretRect.X, prevLayout.Height - 1));
                            target.CharIndex = prevHit.TextPosition;
                        }
                        else if (current.PageIndex > 0)
                        {
                            target.PageIndex--;
                            var prevPage = _pages[target.PageIndex];
                            target.ParagraphIndex = prevPage.Paragraphs.Count - 1;
                            var prevPara = prevPage.Paragraphs[target.ParagraphIndex].TextBlock;
                            var prevLayout = prevPage.CreateCaretLayout(prevPara);
                            var prevHit = prevLayout.HitTestPoint(new Point(caretRect.X, prevLayout.Height - 1));
                            target.CharIndex = prevHit.TextPosition;
                        }
                    }
                    break;

                case Key.Down:
                    {
                        var layout = page.CreateCaretLayout(tb);
                        var caretRect = layout.HitTestTextPosition(current.CharIndex);
                        double targetY = caretRect.Y + caretRect.Height + 1; 
                        if (targetY < layout.Height)
                        {
                            var hit = layout.HitTestPoint(new Point(caretRect.X, targetY));
                            target.CharIndex = hit.TextPosition;
                        }
                        else if (current.ParagraphIndex < page.Paragraphs.Count - 1)
                        {
                            target.ParagraphIndex++;
                            var nextPara = page.Paragraphs[target.ParagraphIndex].TextBlock;
                            var nextLayout = page.CreateCaretLayout(nextPara);
                            var nextHit = nextLayout.HitTestPoint(new Point(caretRect.X, 0));
                            target.CharIndex = nextHit.TextPosition;
                        }
                        else if (current.PageIndex < _pages.Count - 1)
                        {
                            target.PageIndex++;
                            var nextPage = _pages[target.PageIndex];
                            target.ParagraphIndex = 0;
                            var nextPara = nextPage.Paragraphs[target.ParagraphIndex].TextBlock;
                            var nextLayout = nextPage.CreateCaretLayout(nextPara);
                            var nextHit = nextLayout.HitTestPoint(new Point(caretRect.X, 0));
                            target.CharIndex = nextHit.TextPosition;
                        }
                    }
                    break;
            }

            if (target.PageIndex != current.PageIndex || target.ParagraphIndex != current.ParagraphIndex || target.CharIndex != current.CharIndex)
            {
                MoveCaret(target, isShiftPressed);
            }
        }

        public string GetPlainText()
        {
            return string.Join("\n\n", _pages.Select(p => p.GetPlainText()));
        }

        public int GetTotalWordCount()
        {
            int total = 0;
            foreach (var page in _pages)
            {
                string text = page.GetPlainText();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    total += text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
                }
            }
            return total;
        }

        public async void CopySelection()
        {
            var selectedText = new System.Text.StringBuilder();
            foreach (var page in _pages)
            {
                var text = page.GetSelectedText();
                if (!string.IsNullOrEmpty(text))
                {
                    if (selectedText.Length > 0) selectedText.AppendLine();
                    selectedText.Append(text);
                }
            }

            if (selectedText.Length > 0)
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel?.Clipboard != null)
                {
                    await topLevel.Clipboard.SetTextAsync(selectedText.ToString());
                }
            }
        }

        public async void CutSelection()
        {
            CopySelection();
            DeleteSelection();
        }

        public async void Paste()
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.Clipboard == null) return;

            var text = await topLevel.Clipboard.GetTextAsync();
            if (string.IsNullOrEmpty(text)) return;

            if (HasSelection()) DeleteSelection();

            // Insert text at current cursor
            var page = _pages[_currentPageIndex];
            page.InsertText(text);
            CheckPageOverflow(_currentPageIndex);
        }

        public void DeleteSelection()
        {
            // Iterate all pages to delete selected content
            foreach (var page in _pages)
            {
                page.DeleteSelectedText(); // Renamed/Updated local method
            }
            // Logic to merge pages or remove empty pages could go here
            // For now, removing empty paragraphs within pages is handled by DeleteSelectedText
        }

        public bool HasSelection()
        {
            return _pages.Any(p => p.HasSelection);
        }

        public async System.Threading.Tasks.Task ExportToPdfAsync(string filePath)
        {
            // PDF units are in points (1/72 inch). 
            // Our editor is at 96 DPI (default). 
            // Scale factor: 72 / 96 = 0.75
            const float scale = 0.75f;
            const float pdfPageWidth = 612f;
            const float pdfPageHeight = 792f;

            using var stream = System.IO.File.Create(filePath);
            using var document = SkiaSharp.SKDocument.CreatePdf(stream);

            foreach (var page in _pages)
            {
                var canvas = document.BeginPage(pdfPageWidth, pdfPageHeight);
                
                // Draw white background
                using var bgPaint = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColors.White };
                canvas.DrawRect(0, 0, pdfPageWidth, pdfPageHeight, bgPaint);

                // Page padding (96px * 0.75 = 72pt)
                float paddingLeft = (float)page.PageBorder.Padding.Left * scale;
                float paddingTop = (float)page.PageBorder.Padding.Top * scale;
                float contentWidth = (float)(page.PageBorder.Width - page.PageBorder.Padding.Left - page.PageBorder.Padding.Right) * scale;

                // --- 1. Draw Background Images ---
                DrawImages(canvas, page.BackgroundCanvas, scale, paddingLeft, paddingTop);

                // --- 2. Draw Main Content (Paragraphs, Tables) ---
                float currentY = paddingTop;
                foreach (var child in page.ParagraphContainer.Children)
                {
                    if (child is SelectableTextBlock tb)
                    {
                        var paragraph = FindParagraphForTextBlock(page, tb);
                        float height = DrawTextBlock(canvas, tb, paragraph, paddingLeft, currentY, contentWidth, scale);
                        currentY += height;
                    }
                    else if (child is Grid grid) // Table
                    {
                        float height = DrawTable(canvas, grid, paddingLeft, currentY, contentWidth, scale);
                        currentY += height;
                    }
                }

                // --- 3. Draw Foreground Images ---
                DrawImages(canvas, page.ForegroundCanvas, scale, paddingLeft, paddingTop);

                document.EndPage();
            }

            document.Close();
        }

        private Paragraph? FindParagraphForTextBlock(PageControl page, SelectableTextBlock tb)
        {
            // This is a bit inefficient but necessary to get the FormattedRuns correctly
            // Usually PageControl should expose more metadata
            var field = typeof(PageControl).GetField("_paragraphs", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                var list = field.GetValue(page) as List<Paragraph>;
                return list?.FirstOrDefault(p => p.TextBlock == tb);
            }
            return null;
        }

        private void DrawImages(SkiaSharp.SKCanvas canvas, Canvas imageLayer, float scale, float pagePaddingX, float pagePaddingY)
        {
            foreach (var child in imageLayer.Children)
            {
                if (child is SelectableImageControl img)
                {
                    float left = (float)Canvas.GetLeft(img) * scale + pagePaddingX;
                    float top = (float)Canvas.GetTop(img) * scale + pagePaddingY;
                    float width = (float)img.Width * scale;
                    float height = (float)img.Height * scale;

                    using var skBitmap = ScaleAvaloniaBitmapToSkia(img.SourceBitmap);
                    if (skBitmap == null) continue;

                    canvas.Save();
                    
                    // Transformation/Rotation around center
                    canvas.Translate(left + width/2, top + height/2);
                    canvas.RotateDegrees((float)img.Rotation);
                    canvas.Scale((float)img.ScaleX, (float)img.ScaleY);
                    canvas.Translate(-(width/2), -(height/2));

                    // Clip for Crop
                    canvas.ClipRect(SkiaSharp.SKRect.Create(0, 0, width, height));

                    // Draw the bitmap 
                    // Calculate source rect based on crop
                    float origW = (float)img.BaseWidth * scale;
                    float origH = (float)img.BaseHeight * scale;
                    float cL = (float)img.CropLeft * scale;
                    float cT = (float)img.CropTop * scale;

                    canvas.DrawBitmap(skBitmap, SkiaSharp.SKRect.Create(-cL, -cT, origW, origH));
                    
                    canvas.Restore();
                }
            }
        }

        private float DrawTextBlock(SkiaSharp.SKCanvas canvas, SelectableTextBlock tb, Paragraph? para, float x, float y, float width, float scale)
        {
            if (para == null || para.Runs.Count == 0 || string.IsNullOrEmpty(tb.Text)) 
                return (float)tb.MinHeight * scale;

            float currentParaY = y;
            float lineSpacing = (float)(tb.LineHeight > 0 ? tb.LineHeight : tb.FontSize * 1.2) * scale;
            
            // Simple Line Breaking Approximation
            // (Note: To be perfect we'd need a real text shaper, but this makes text selectable)
            string text = tb.Text;
            var runs = para.Runs;
            
            // For selectable text in PDF, we draw string by string
            // We use the first run's font for simplicity in measurement or split runs
            var run = runs[0];
            using var paint = new SkiaSharp.SKPaint
            {
                Typeface = SkiaSharp.SKTypeface.FromFamilyName(run.FontFamily.Name, 
                    run.FontWeight == FontWeight.Bold ? SkiaSharp.SKFontStyleWeight.Bold : SkiaSharp.SKFontStyleWeight.Normal,
                    SkiaSharp.SKFontStyleWidth.Normal,
                    run.FontStyle == FontStyle.Italic ? SkiaSharp.SKFontStyleSlant.Italic : SkiaSharp.SKFontStyleSlant.Upright),
                TextSize = (float)run.FontSize * scale,
                Color = MapAvaloniaColorToSkia(run.Foreground),
                IsAntialias = true
            };

            float indent = (float)tb.TextIndent * scale;
            float leftM = (float)tb.Margin.Left * scale;
            float rightM = (float)tb.Margin.Right * scale;
            float usableWidth = width - leftM - rightM - indent;

            string[] words = text.Split(' ');
            string line = "";
            float lineY = currentParaY + paint.TextSize;

            foreach (var word in words)
            {
                string testLine = string.IsNullOrEmpty(line) ? word : line + " " + word;
                if (paint.MeasureText(testLine) > usableWidth && !string.IsNullOrEmpty(line))
                {
                    DrawLine(canvas, line, x + leftM + indent, lineY, tb.TextAlignment, usableWidth, paint);
                    line = word;
                    lineY += lineSpacing;
                    indent = 0; // only first line has indent
                    usableWidth = width - leftM - rightM;
                }
                else
                {
                    line = testLine;
                }
            }
            if (!string.IsNullOrEmpty(line))
            {
                DrawLine(canvas, line, x + leftM + indent, lineY, tb.TextAlignment, usableWidth, paint);
                lineY += lineSpacing;
            }

            return lineY - currentParaY + (float)tb.Margin.Bottom * scale;
        }

        private void DrawLine(SkiaSharp.SKCanvas canvas, string text, float x, float y, TextAlignment align, float width, SkiaSharp.SKPaint paint)
        {
            float drawX = x;
            if (align == TextAlignment.Center)
            {
                drawX = x + (width - paint.MeasureText(text)) / 2;
            }
            else if (align == TextAlignment.Right)
            {
                drawX = x + width - paint.MeasureText(text);
            }
            canvas.DrawText(text, drawX, y, paint);
        }

        private float DrawTable(SkiaSharp.SKCanvas canvas, Grid grid, float x, float y, float width, float scale)
        {
            // Table drawing logic: iterate children (Borders)
            float tableY = y;
            float maxH = 0;

            foreach (var child in grid.Children)
            {
                if (child is Border b && b.Child is SelectableTextBlock tb)
                {
                    int row = Grid.GetRow(b);
                    int col = Grid.GetColumn(b);
                    
                    float cellWidth = width / 2; // Assuming 2 columns for now based on InsertTable
                    float cellX = x + col * cellWidth;
                    float cellY = tableY + row * 40 * scale; // Approx row height

                    // Draw border
                    using var pen = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColors.LightGray, IsStroke = true, StrokeWidth = 1 };
                    canvas.DrawRect(cellX, cellY, cellWidth, 40 * scale, pen);

                    DrawTextBlock(canvas, tb, null, cellX + 5 * scale, cellY + 5 * scale, cellWidth - 10 * scale, scale);
                    maxH = Math.Max(maxH, (row + 1) * 40 * scale);
                }
            }
            return maxH + 10 * scale;
        }

        private SkiaSharp.SKColor MapAvaloniaColorToSkia(IBrush brush)
        {
            if (brush is ISolidColorBrush s) return new SkiaSharp.SKColor(s.Color.R, s.Color.G, s.Color.B, s.Color.A);
            return SkiaSharp.SKColors.Black;
        }

        private SkiaSharp.SKBitmap? ScaleAvaloniaBitmapToSkia(Bitmap bitmap)
        {
            using var ms = new System.IO.MemoryStream();
            bitmap.Save(ms);
            ms.Position = 0;
            return SkiaSharp.SKBitmap.Decode(ms);
        }
    }

    public class PageControl
    {
        private CustomRichTextEditor _editor;
        public int PageIndex { get; internal set; }
        private StackPanel _paragraphContainer;
        private Canvas _backgroundCanvas;
        private Canvas _foregroundCanvas;
        private List<Paragraph> _paragraphs = new();
        private int _currentParagraphIndex = 0;

        // Caché de selección
        private int _savedSelectionStart = -1;
        private int _savedSelectionEnd = -1;
        
        private Border _pageBorder; // Internal border
        public Border PageBorder => _pageBorder;
        public Control Container { get; private set; } // The LayoutTransformControl wrapper
        public List<Paragraph> Paragraphs => _paragraphs;
        public int CurrentParagraphIndex => _currentParagraphIndex;
        public StackPanel ParagraphContainer => _paragraphContainer;
        public Canvas BackgroundCanvas => _backgroundCanvas;
        public Canvas ForegroundCanvas => _foregroundCanvas;

        public List<ImageState> CaptureImagesState()
        {
            var images = new List<ImageState>();
            CaptureFromCanvas(_backgroundCanvas, images, true);
            CaptureFromCanvas(_foregroundCanvas, images, false);
            return images;
        }

        private void CaptureFromCanvas(Canvas canvas, List<ImageState> list, bool isBackground)
        {
            foreach (var child in canvas.Children)
            {
                if (child is SelectableImageControl img)
                {
                    list.Add(new ImageState
                    {
                        FilePath = img.FilePath,
                        X = Canvas.GetLeft(img),
                        Y = Canvas.GetTop(img),
                        Width = img.Width,
                        Height = img.Height,
                        Rotation = img.Rotation,
                        IsBackground = isBackground
                    });
                }
            }
        }

        public SelectableTextBlock CreateDefaultTextBlock()
        {
            return new SelectableTextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontFamily = _editor.CurrentFontFamily,
                FontSize = _editor.CurrentFontSize,
                Focusable = true,
                Background = Brushes.Transparent,
                SelectionBrush = new SolidColorBrush(Color.FromArgb(100, 0, 120, 215)),
                TextAlignment = _editor.CurrentAlignment,
                LineHeight = _editor.CurrentFontSize * _editor.CurrentLineHeight,
                Margin = new Thickness(_editor.CurrentLeftIndent, 0, _editor.CurrentRightIndent, _editor.CurrentParagraphSpacing),
                TextIndent = _editor.CurrentTextIndent,
                MinHeight = 20
            };
        }

        private SelectableTextBlock? CurrentTextBlock =>
            _currentParagraphIndex >= 0 && _currentParagraphIndex < _paragraphs.Count
                ? _paragraphs[_currentParagraphIndex].TextBlock
                : null;

        public bool HasSelection
        {
            get
            {
                if (_savedSelectionStart >= 0 && _savedSelectionEnd > _savedSelectionStart)
                    return true;
                return _paragraphs.Any(p => p.TextBlock.SelectionStart != p.TextBlock.SelectionEnd);
            }
        }

        private int SelectionStart
        {
            get
            {
                if (_savedSelectionStart >= 0)
                    return Math.Min(_savedSelectionStart, _savedSelectionEnd);
                var tb = CurrentTextBlock;
                return tb != null ? Math.Min(tb.SelectionStart, tb.SelectionEnd) : 0;
            }
        }

        private int SelectionEnd
        {
            get
            {
                if (_savedSelectionEnd >= 0)
                    return Math.Max(_savedSelectionStart, _savedSelectionEnd);
                var tb = CurrentTextBlock;
                return tb != null ? Math.Max(tb.SelectionStart, tb.SelectionEnd) : 0;
            }
        }

        public PageControl(CustomRichTextEditor editor, int pageIndex)
        {
            _editor = editor;
            PageIndex = pageIndex; // Use Property

            _paragraphContainer = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Background = Brushes.Transparent, // Changed from White so it can overlay
                ZIndex = 0
            };

            _backgroundCanvas = new Canvas
            {
                Background = null,
                ZIndex = -1
            };

            _foregroundCanvas = new Canvas
            {
                Background = null, 
                ZIndex = 1
            };

            var pageGrid = new Grid();
            pageGrid.Children.Add(_backgroundCanvas);
            pageGrid.Children.Add(_paragraphContainer);
            pageGrid.Children.Add(_foregroundCanvas);

            _pageBorder = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(4),
                Width = 816,
                Height = 1056,
                Padding = new Thickness(96),
                Margin = new Thickness(15), // Added margin for spacing in WrapPanel grid
                Child = pageGrid,
                BoxShadow = BoxShadows.Parse("0 10 30 0 #2A000000"), // Darkened and sharpened the shadow specifically for better visual distinction
                ClipToBounds = true
            };

            Container = new LayoutTransformControl
            {
                Child = _pageBorder
            };

            _pageBorder.PointerPressed += (s, e) =>
            {
                _editor.SetCurrentPage(PageIndex);
                // Clear any selected image when clicking on the page background
                _editor.SetSelectedImage(null);

                if (_paragraphs.Count > 0)
                {
                    if (_currentParagraphIndex < _paragraphs.Count)
                    {
                        var tb = _paragraphs[_currentParagraphIndex].TextBlock;
                        tb.Focus();
                        // Handle Shift click on container border (edge case, usually clicks TB)
                    }
                }
                else
                {
                    AddNewParagraph();
                }
            };

            // Si es la página 0, añadir un párrafo inicial
            if (pageIndex == 0)
            {
                AddNewParagraph();
            }
        }

        public void SelectAll()
        {
            foreach (var para in _paragraphs)
            {
                para.TextBlock.SelectAll();
            }
        }

        public void ClearContent()
        {
            _paragraphContainer.Children.Clear();
            _paragraphs.Clear();
            _backgroundCanvas.Children.Clear();
            _foregroundCanvas.Children.Clear();
            _currentParagraphIndex = 0;
        }

        public void SetCurrentParagraph(int index)
        {
            if (index >= 0 && index < _paragraphs.Count)
            {
                _currentParagraphIndex = index;
            }
        }

        public int GetCurrentParagraphIndex() => _currentParagraphIndex;

        public int GetCurrentCharIndex()
        {
            var tb = CurrentTextBlock;
            return tb != null ? tb.SelectionEnd : 0;
        }

        public void SetCurrentCharIndex(int index)
        {
            var tb = CurrentTextBlock;
            if (tb != null)
            {
                tb.SelectionStart = index;
                tb.SelectionEnd = index;
                tb.Focus();
            }
        }

        public void ClearSelection()
        {
            ClearSelectionExcept(-1);
        }

        public void UpdateSelectionRange(CustomRichTextEditor.SelectionPoint start, CustomRichTextEditor.SelectionPoint end)
        {
            for (int i = 0; i < _paragraphs.Count; i++)
            {
                var para = _paragraphs[i];
                var tb = para.TextBlock;

                bool isBefore = (PageIndex < start.PageIndex) || (PageIndex == start.PageIndex && i < start.ParagraphIndex);
                bool isAfter = (PageIndex > end.PageIndex) || (PageIndex == end.PageIndex && i > end.ParagraphIndex);

                if (isBefore || isAfter)
                {
                    tb.ClearSelection();
                }
                else if (PageIndex == start.PageIndex && i == start.ParagraphIndex && PageIndex == end.PageIndex && i == end.ParagraphIndex)
                {
                    // Same paragraph
                    tb.SelectionStart = start.CharIndex;
                    tb.SelectionEnd = end.CharIndex;
                }
                else if (PageIndex == start.PageIndex && i == start.ParagraphIndex)
                {
                    // Start paragraph of a range
                    tb.SelectionStart = start.CharIndex;
                    tb.SelectionEnd = tb.Text?.Length ?? 0;
                }
                else if (PageIndex == end.PageIndex && i == end.ParagraphIndex)
                {
                    // End paragraph of a range
                    tb.SelectionStart = 0;
                    tb.SelectionEnd = end.CharIndex;
                }
                else
                {
                    // Inside range
                    tb.SelectAll();
                }
            }
        }

        public void ClearSelectionExcept(int keepParaIndex)
        {
            for (int i = 0; i < _paragraphs.Count; i++)
            {
                if (i != keepParaIndex)
                {
                    _paragraphs[i].TextBlock.SelectionStart = 0;
                    _paragraphs[i].TextBlock.SelectionEnd = 0;
                }
            }
            if (keepParaIndex == -1) ClearSavedSelection();
        }

        public double GetContentHeight()
        {
            // Forzar medida si es necesaria
            _paragraphContainer.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return _paragraphContainer.DesiredSize.Height;
        }

        public void MoveParagraphTo(int index, PageControl targetPage, bool atBeginning = false)
        {
            if (index < 0 || index >= _paragraphs.Count) return;

            var para = _paragraphs[index];
            bool hasFocus = para.TextBlock.IsFocused;

            RemoveParagraphListeners(para.TextBlock);
            _paragraphContainer.Children.RemoveAt(index);
            _paragraphs.RemoveAt(index);
            
            if (_currentParagraphIndex >= _paragraphs.Count) 
                _currentParagraphIndex = Math.Max(0, _paragraphs.Count - 1);

            targetPage.AddExistingParagraph(para, atBeginning);

            if (hasFocus)
            {
                para.TextBlock.Focus();
                _editor.SetCurrentPage(targetPage.PageIndex);
            }
        }

        public void MoveImageTo(SelectableImageControl image, PageControl targetPage)
        {
            // Logic to move image between canvases
            bool isBackground = _backgroundCanvas.Children.Contains(image);
            if (isBackground) _backgroundCanvas.Children.Remove(image);
            else _foregroundCanvas.Children.Remove(image);

            targetPage.AddExistingImage(image, isBackground);
        }

        public void AddExistingImage(SelectableImageControl image, bool toBackground)
        {
            if (toBackground) _backgroundCanvas.Children.Add(image);
            else _foregroundCanvas.Children.Add(image);
            
            image.Selected += (s, args) => {
                _editor.SetSelectedImage(image);
            };
        }

        public void AddExistingImage(string filePath, Point position, double width, double height, double rotation, bool isBackground)
        {
            try
            {
                var bitmap = new Avalonia.Media.Imaging.Bitmap(filePath);
                var image = new SelectableImageControl(bitmap);
                image.FilePath = filePath;
                image.Width = width;
                image.Height = height;
                image.Rotation = rotation;
                Canvas.SetLeft(image, position.X);
                Canvas.SetTop(image, position.Y);

                AddExistingImage(image, isBackground);
            }
            catch { }
        }

        public void SplitAtCaretForPageBreak()
        {
            if (_currentParagraphIndex < 0 || _currentParagraphIndex >= _paragraphs.Count) return;
            
            var tb = _paragraphs[_currentParagraphIndex].TextBlock;
            if (tb.SelectionEnd < (tb.Text?.Length ?? 0))
            {
                SplitParagraphAtCaret();
            }
        }

        public void MoveContentFromSplitTo(PageControl targetPage)
        {
            // Move all paragraphs from _currentParagraphIndex + 1 to the end
            // We do it in reverse to maintain indices or just keep taking the same index
            int startIndex = _currentParagraphIndex + 1;
            while (startIndex < _paragraphs.Count)
            {
                MoveParagraphTo(startIndex, targetPage, atBeginning: false);
            }

            // Move images that are below the cursor? 
            // For now, let's just move everything that follows the paragraphs
            // or maybe just the paragraphs for a clean "text flow" break.
        }

        public void MoveLastParagraphTo(PageControl targetPage)
        {
            MoveParagraphTo(_paragraphs.Count - 1, targetPage, atBeginning: true);
        }

        public void AddExistingParagraph(Paragraph para, bool atBeginning = false)
        {
            AddParagraphToUI(para, atBeginning);
        }

        private void AddParagraphToUI(Paragraph para, bool atBeginning)
        {
            // Use AddHandler with handledEventsToo: true to ensure we catch events even if SelectableTextBlock handles them
            para.TextBlock.AddHandler(InputElement.KeyDownEvent, OnTextBlockKeyDown, RoutingStrategies.Tunnel); // Preview for keys
            para.TextBlock.AddHandler(InputElement.TextInputEvent, OnTextBlockTextInput, RoutingStrategies.Bubble, true);
            para.TextBlock.AddHandler(InputElement.PointerPressedEvent, OnTextBlockPointerPressed, RoutingStrategies.Bubble, true);
            para.TextBlock.AddHandler(InputElement.PointerReleasedEvent, OnTextBlockPointerReleased, RoutingStrategies.Bubble, true);
            para.TextBlock.LostFocus += OnLostFocus; // Standard event
            para.TextBlock.GotFocus += OnTextBlockGotFocus;

            if (atBeginning)
            {
                _paragraphs.Insert(0, para);
                _paragraphContainer.Children.Insert(0, para.TextBlock);
                _currentParagraphIndex = 0;
            }
            else
            {
                _paragraphs.Add(para);
                _paragraphContainer.Children.Add(para.TextBlock);
                _currentParagraphIndex = _paragraphs.Count - 1;
            }
            
            para.TextBlock.SelectionChanged += (s, e) => _editor.NotifySelectionChanged();
        }

        private void RemoveParagraphListeners(SelectableTextBlock tb)
        {
            tb.RemoveHandler(InputElement.KeyDownEvent, OnTextBlockKeyDown);
            tb.RemoveHandler(InputElement.TextInputEvent, OnTextBlockTextInput);
            tb.RemoveHandler(InputElement.PointerPressedEvent, OnTextBlockPointerPressed);
            tb.RemoveHandler(InputElement.PointerReleasedEvent, OnTextBlockPointerReleased);
            tb.LostFocus -= OnLostFocus;
            tb.GotFocus -= OnTextBlockGotFocus;
        }

        private void OnTextBlockKeyDown(object? sender, KeyEventArgs e)
        {
            if (sender is SelectableTextBlock tb)
            {
                var idx = GetParagraphIndex(tb);
                if (idx != -1) OnKeyDown(idx, e);
            }
        }

        private void OnTextBlockTextInput(object? sender, TextInputEventArgs e)
        {
            if (sender is SelectableTextBlock tb)
            {
                var idx = GetParagraphIndex(tb);
                if (idx != -1) OnTextInput(idx, e);
            }
        }


        private void OnTextBlockPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (sender is SelectableTextBlock tb)
            {
                var idx = GetParagraphIndex(tb);
                if (idx != -1) OnPointerReleased(idx, e);
            }
        }

        private void OnTextBlockGotFocus(object? sender, GotFocusEventArgs e)
        {
            if (sender is SelectableTextBlock tb)
            {
                var idx = GetParagraphIndex(tb);
                if (idx != -1)
                {
                    _currentParagraphIndex = idx;
                    _editor.SetCurrentPage(PageIndex);
                    _editor.UpdateCurrentPropertiesFromParagraph(_paragraphs[idx]);
                }
            }
        }

        private int GetParagraphIndex(SelectableTextBlock tb)
        {
            for (int i = 0; i < _paragraphs.Count; i++)
            {
                if (_paragraphs[i].TextBlock == tb) return i;
            }
            return -1;
        }

        private void SplitParagraphAtCaret()
        {
            if (_currentParagraphIndex < 0 || _currentParagraphIndex >= _paragraphs.Count)
            {
                AddNewParagraph(_editor.CurrentAlignment);
                return;
            }

            var para = _paragraphs[_currentParagraphIndex];
            int caretPos = para.TextBlock.SelectionEnd;
            int totalLen = para.Runs.Sum(r => r.Text.Length);
            if (caretPos < 0) caretPos = 0;
            if (caretPos > totalLen) caretPos = totalLen;

            // Split runs at caretPos
            var beforeRuns = new List<FormattedRun>();
            var afterRuns = new List<FormattedRun>();
            int runOffset = 0;

            foreach (var run in para.Runs)
            {
                int runStart = runOffset;
                int runEnd = runOffset + run.Text.Length;

                if (runEnd <= caretPos)
                {
                    // Entire run is before caret
                    beforeRuns.Add(run);
                }
                else if (runStart >= caretPos)
                {
                    // Entire run is after caret
                    afterRuns.Add(run);
                }
                else
                {
                    // Split this run
                    int splitIndex = caretPos - runStart;
                    beforeRuns.Add(run.Clone(run.Text.Substring(0, splitIndex)));
                    afterRuns.Add(run.Clone(run.Text.Substring(splitIndex)));
                }
                runOffset = runEnd;
            }

            // Ensure each side has at least one run
            if (beforeRuns.Count == 0) beforeRuns.Add(new FormattedRun { Text = "" });
            if (afterRuns.Count == 0) afterRuns.Add(new FormattedRun { Text = "" });

            // Update current paragraph with "before" runs
            para.Runs = beforeRuns;
            RefreshParagraph(_currentParagraphIndex);

            // Create new paragraph with "after" runs
            var newTextBlock = new SelectableTextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontFamily = _editor.CurrentFontFamily,
                FontSize = _editor.CurrentFontSize,
                Focusable = true,
                Background = Brushes.Transparent,
                SelectionBrush = new SolidColorBrush(Color.FromArgb(100, 0, 120, 215)),
                TextAlignment = para.Alignment,
                LineHeight = _editor.CurrentFontSize * _editor.CurrentLineHeight,
                Margin = new Thickness(_editor.CurrentLeftIndent, 0, _editor.CurrentRightIndent, _editor.CurrentParagraphSpacing),
                TextIndent = _editor.CurrentTextIndent,
                MinHeight = 20
            };

            var newParagraph = new Paragraph
            {
                TextBlock = newTextBlock,
                Runs = afterRuns,
                Alignment = para.Alignment,
                LineSpacing = para.LineSpacing,
                ParagraphSpacing = para.ParagraphSpacing,
                TextIndent = para.TextIndent,
                LeftIndent = para.LeftIndent,
                RightIndent = para.RightIndent
            };

            int insertIndex = _currentParagraphIndex + 1;
            InsertParagraphAt(newParagraph, insertIndex);
            _currentParagraphIndex = insertIndex;

            RefreshParagraph(insertIndex);

            // Focus and position caret at start of new paragraph
            newTextBlock.SelectionStart = 0;
            newTextBlock.SelectionEnd = 0;
            newTextBlock.Focus();
            _editor.SetCurrentPage(PageIndex);
            _editor.CheckPageOverflow(PageIndex);

            // Use Dispatcher to update caret after layout
            Dispatcher.UIThread.Post(() =>
            {
                _editor.UpdateCaretPosition();
            }, DispatcherPriority.Input);
        }

        private void InsertParagraphAt(Paragraph para, int index)
        {
            para.TextBlock.AddHandler(InputElement.KeyDownEvent, OnTextBlockKeyDown, RoutingStrategies.Tunnel);
            para.TextBlock.AddHandler(InputElement.TextInputEvent, OnTextBlockTextInput, RoutingStrategies.Bubble, true);
            para.TextBlock.AddHandler(InputElement.PointerPressedEvent, OnTextBlockPointerPressed, RoutingStrategies.Bubble, true);
            para.TextBlock.AddHandler(InputElement.PointerReleasedEvent, OnTextBlockPointerReleased, RoutingStrategies.Bubble, true);
            para.TextBlock.LostFocus += OnLostFocus;
            para.TextBlock.GotFocus += OnTextBlockGotFocus;
            para.TextBlock.SelectionChanged += (s, e) => _editor.NotifySelectionChanged();

            if (index >= _paragraphs.Count)
            {
                _paragraphs.Add(para);
                _paragraphContainer.Children.Add(para.TextBlock);
            }
            else
            {
                _paragraphs.Insert(index, para);
                // We need to find the correct visual index in _paragraphContainer.
                // Because we might have block elements (Images, Tables), the index in _paragraphs 
                // doesn't perfectly match the index in _paragraphContainer.Children.
                // To keep it simple, we insert the TextBlock right before the next paragraph's TextBlock
                int visualIndex = _paragraphContainer.Children.Count;
                if (index + 1 < _paragraphs.Count)
                {
                    visualIndex = _paragraphContainer.Children.IndexOf(_paragraphs[index + 1].TextBlock);
                    if (visualIndex < 0) visualIndex = _paragraphContainer.Children.Count;
                }
                
                _paragraphContainer.Children.Insert(visualIndex, para.TextBlock);
            }
        }


        public void InsertTable()
        {
            // Create a basic 2x2 grid table
            var grid = new Grid
            {
                Margin = new Thickness(0, 10, 0, 10),
                ColumnDefinitions = new ColumnDefinitions("*,*"),
                RowDefinitions = new RowDefinitions("Auto,Auto"),
                ShowGridLines = true, // Avalonia's built-in grid lines for debugging/simple presentation
                Background = Brushes.Transparent,
            };

            // Add standard Borders/TextBlocks to make it look like a real table
            for (int r = 0; r < 2; r++)
            {
                for (int c = 0; c < 2; c++)
                {
                    var cellBorder = new Border
                    {
                        BorderBrush = Brushes.LightGray,
                        BorderThickness = new Thickness(1),
                        Padding = new Thickness(5),
                        Child = new SelectableTextBlock 
                        { 
                            Text = $"Celda {r+1},{c+1}",
                            TextWrapping = TextWrapping.Wrap,
                            FontFamily = _editor.CurrentFontFamily,
                            FontSize = _editor.CurrentFontSize
                        }
                    };
                    Grid.SetRow(cellBorder, r);
                    Grid.SetColumn(cellBorder, c);
                    grid.Children.Add(cellBorder);
                }
            }

            int insertPos = _paragraphContainer.Children.Count;
            if (_currentParagraphIndex >= 0 && _currentParagraphIndex < _paragraphs.Count)
            {
                var currentTb = _paragraphs[_currentParagraphIndex].TextBlock;
                int visualIdx = _paragraphContainer.Children.IndexOf(currentTb);
                
                if (currentTb.SelectionEnd < (currentTb.Text?.Length ?? 0))
                {
                    SplitParagraphAtCaret();
                    insertPos = _paragraphContainer.Children.IndexOf(_paragraphs[_currentParagraphIndex].TextBlock);
                }
                else
                {
                    insertPos = visualIdx + 1;
                }
            }

            if (insertPos < 0 || insertPos > _paragraphContainer.Children.Count) 
                insertPos = _paragraphContainer.Children.Count;
                
            _paragraphContainer.Children.Insert(insertPos, grid);
        }

        private void AddNewParagraph(TextAlignment alignment = TextAlignment.Left)
        {
            var textBlock = new SelectableTextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontFamily = new FontFamily("Calibri"),
                FontSize = 14,
                Focusable = true,
                Background = Brushes.Transparent,
                SelectionBrush = new SolidColorBrush(Color.FromArgb(100, 0, 120, 215)),
                TextAlignment = alignment,
                LineHeight = _editor.CurrentFontSize * _editor.CurrentLineHeight,
                Margin = new Thickness(_editor.CurrentLeftIndent, 0, 0, _editor.CurrentParagraphSpacing),
                TextIndent = _editor.CurrentTextIndent,
                MinHeight = 20
            };

            var paragraph = new Paragraph
            {
                TextBlock = textBlock,
                Runs = new List<FormattedRun> { new FormattedRun { Text = "" } },
                Alignment = alignment
            };

            AddParagraphToUI(paragraph, atBeginning: false);
            
            paragraph.TextBlock.SelectionStart = 0;
            paragraph.TextBlock.SelectionEnd = 0;

            RefreshParagraph(_paragraphs.Count - 1);
        }

        private void SaveSelection()
        {
            var tb = CurrentTextBlock;
            if (tb != null && tb.SelectionStart != tb.SelectionEnd)
            {
                _savedSelectionStart = tb.SelectionStart;
                _savedSelectionEnd = tb.SelectionEnd;
            }
        }

        private void ClearSavedSelection()
        {
            _savedSelectionStart = -1;
            _savedSelectionEnd = -1;
        }

        private void OnPointerPressed(int paragraphIndex, PointerPressedEventArgs e)
        {
            _currentParagraphIndex = paragraphIndex;
            _editor.SetCurrentPage(PageIndex);
            
            // Clear any selected image when clicking on text
            _editor.SetSelectedImage(null);

            // Focus is handled by TextBlock pointer events usually, but ensure it here
            _paragraphs[paragraphIndex].TextBlock.Focus();
            _savedSelectionStart = -1;
            _savedSelectionEnd = -1;

            _editor.UpdateCaretPosition(); // UPDATE CARET
        }

        private void OnPointerReleased(int paragraphIndex, PointerReleasedEventArgs e)
        {
            _editor.EndDragSelection();
            SaveSelection();
        }

        private void OnLostFocus(object? sender, RoutedEventArgs e)
        {
            SaveSelection();
        }

        public void SelectRange(int startParaIndex, int endParaIndex)
        {
            for (int i = 0; i < _paragraphs.Count; i++)
            {
                if (i >= startParaIndex && i <= endParaIndex)
                {
                    if (startParaIndex != endParaIndex)
                    {
                        _paragraphs[i].TextBlock.SelectAll();
                    }
                }
                else
                {
                    _paragraphs[i].TextBlock.SelectionStart = 0;
                    _paragraphs[i].TextBlock.SelectionEnd = 0;
                }
            }
            ClearSavedSelection();
        }

        public double GetCurrentLineHeight()
        {
            if (_currentParagraphIndex >= 0 && _currentParagraphIndex < _paragraphs.Count)
            {
                var run = _paragraphs[_currentParagraphIndex].Runs.LastOrDefault();
                return run != null ? run.FontSize * 1.2 : 18; // Approx
            }
            return 18;
        }

        public Point? GetCaretPoint()
        {
            if (_currentParagraphIndex < 0 || _currentParagraphIndex >= _paragraphs.Count) return null;
            var para = _paragraphs[_currentParagraphIndex];
            var tb = para.TextBlock;

            // Ensure we have valid text
            var text = tb.Text ?? "";

            // Construct TextLayout manually
            // We ignore complex styling for caret positioning for now to ensure robustness
            // (Functionality over perfection: caret might be slightly off on bold text but visible)

            var typeface = new Typeface(tb.FontFamily, tb.FontStyle, tb.FontWeight);

            var layout = new TextLayout(
                text,
                typeface,
                tb.FontSize,
                Brushes.Black,
                tb.TextAlignment, // Important: TextAlignment matches the block's alignment, meaning layout.HitTest returns absolute offsets correctly
                tb.TextWrapping,
                maxWidth: tb.Bounds.Width, // Constraint bounds matches the visual layout exactly
                maxHeight: double.PositiveInfinity
            );

            int caretIndex = tb.SelectionEnd;
            if (caretIndex < 0) caretIndex = 0;
            if (caretIndex > text.Length) caretIndex = text.Length;

            // Wait, does it have Position?

            // Standard usage: 
            // var rect = layout.HitTestTextPos(pos). 
            // If it returns a complex object, we might need 'Rect' property from it?

            // Let's TRY assuming it returns TextHitTestResult and check if we can get a Rect.
            // NOTE: If this fails to compile, we might need "Layout.HitTestTextRange(pos, 1).First().Rect"?

            var hit = layout.HitTestTextPosition(caretIndex);

            // Check properties of 'hit' (TextHitTestResult)
            // It usually contains 'Rect' or similar. 
            // If previous error said "Rect does not contain Point", then 'hit' MIGHT BE Rect if I typed it as var?
            // No, the error said "Rect does not contain Point". This implies 'hit' WAS a Rect struct and I tried .Point.
            // So HitTestTextPosition RETURNED A RECT? 
            // If so:
            // return new Point(hit.X, hit.Y + tb.Bounds.Y);

            // BUT: "TextLayout does not contain definition for HitTestTextPosition" (Wait, error said FormattedText didn't)
            // Error in 1497: "TextLayout does not contain definition for XXX"? or "Rect does not contain Point"?
            // Warning CS0618 (Clipboard). 
            // Error 1497 contained: "Rect does not contain Point".
            // This CONFIRMS HitTestTextPosition returns Rect (or struct compatible with Rect).
            // So use hit.Position (TopLeft) or hit.X, hit.Y.

            // HitTestTextPosition returns coordinates bound to the top-left of the TextLayout.
            // Since the TextBlock may be offset within its container, we MUST add tb.Bounds.TopLeft.
            return new Point(hit.X + tb.Bounds.X, hit.Y + tb.Bounds.Y);
        }

        public string GetSelectedText()
        {
            var sb = new System.Text.StringBuilder();
            if (HasSelection)
            {
                // Complex because selection might span multiple paragraphs conceptually if mapped to paragraphs,
                // but SelectableTextBlock selection is per TB.
                // We iterate paragraphs checking for selection.

                foreach (var para in _paragraphs)
                {
                    var tb = para.TextBlock;
                    if (tb.SelectionStart != tb.SelectionEnd)
                    {
                        int start = Math.Min(tb.SelectionStart, tb.SelectionEnd);
                        int end = Math.Max(tb.SelectionStart, tb.SelectionEnd);
                        var text = tb.Text; // Assuming Text property has current text. Run extraction?
                                            // SelectableTextBlock.Text property usually reflects Inlines text.
                                            // Standard TextBlock Text property is updated if Inlines change? 
                                            // Yes, usually.

                        if (!string.IsNullOrEmpty(text) && start < text.Length)
                        {
                            int len = Math.Min(end, text.Length) - start;
                            if (len > 0)
                            {
                                if (sb.Length > 0) sb.AppendLine();
                                sb.Append(text.Substring(start, len));
                            }
                        }
                    }
                }
            }
            return sb.ToString();
        }

        public int GetParagraphIndexAt(double y)
        {
            double contentY = y - 96; 
            if (contentY < 0) return 0;

            // Iterate paragraphs
            // Bounds of TextBlocks in StackPanel
            // Note: Bounds are relative to StackPanel (_paragraphContainer)
            // We can't access _paragraphContainer.Children easily? It's private.
            // But we have _paragraphs list.
            // We can iterate _paragraphContainer.Children via property? No.
            // But we can iterate _paragraphs and check their TextBlock.Bounds with parent logic?

            for (int i = 0; i < _paragraphs.Count; i++)
            {
                var tb = _paragraphs[i].TextBlock;
                var bounds = tb.Bounds;
                if (contentY <= bounds.Bottom + 5)
                {
                    return i;
                }
            }

            return _paragraphs.Count - 1; // Bottom margin -> Last paragraph
        }

        private void OnTextBlockPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is SelectableTextBlock tb)
            {
                bool isShift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
                var idx = GetParagraphIndex(tb);
                if (idx != -1)
                {
                    var pos = e.GetPosition(tb);
                    var charIdx = tb.GetCaretIndexAt(pos);

                    _editor.StartDragSelection(PageIndex, idx, charIdx);
                    _editor.HandlePageSelection(PageIndex, idx, charIdx, isShift);

                    // Capture pointer on the EDITOR to handle cross-paragraph dragging
                    e.Pointer.Capture(_editor);
                    
                    OnPointerPressed(idx, e);
                }
            }
        }

        // ... (Helper properties truncated)

        private void OnKeyDown(int paragraphIndex, KeyEventArgs e)
        {
            _currentParagraphIndex = paragraphIndex;

            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                switch (e.Key)
                {
                    case Key.B: ToggleBold(); e.Handled = true; return;
                    case Key.I: ToggleItalic(); e.Handled = true; return;
                    case Key.U: ToggleUnderline(); e.Handled = true; return;
                    case Key.A:
                        _editor.SelectAll();
                        e.Handled = true;
                        return;
                    case Key.X:
                        _editor.CutSelection();
                        e.Handled = true;
                        return;
                    case Key.C:
                        _editor.CopySelection();
                        e.Handled = true;
                        return;
                    case Key.V:
                        _editor.Paste();
                        e.Handled = true;
                        return;
                    case Key.Z:
                        _editor.Undo();
                        e.Handled = true;
                        return;
                    case Key.Y:
                        _editor.Redo();
                        e.Handled = true;
                        return;
                }
            }

            switch (e.Key)
            {
                case Key.Back:
                    if (_editor.HasSelection()) _editor.DeleteSelection();
                    else DeleteCharacterAtCaret();
                    _editor.UpdateCaretPosition();
                    e.Handled = true;
                    break;
                case Key.Delete:
                    if (_editor.HasSelection()) _editor.DeleteSelection();
                    else DeleteForwardAtCaret();
                    _editor.UpdateCaretPosition();
                    e.Handled = true;
                    break;
                case Key.Enter:
                    if (_editor.HasSelection()) _editor.DeleteSelection();
                    SplitParagraphAtCaret();
                    e.Handled = true;
                    break;
                case Key.Left:
                    _editor.HandleNavigation(Key.Left, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
                    e.Handled = true;
                    break;
                case Key.Right:
                    _editor.HandleNavigation(Key.Right, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
                    e.Handled = true;
                    break;
                case Key.Home:
                    _editor.HandleNavigation(Key.Home, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
                    e.Handled = true;
                    break;
                case Key.End:
                    _editor.HandleNavigation(Key.End, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
                    e.Handled = true;
                    break;
                case Key.Up:
                    _editor.HandleNavigation(Key.Up, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
                    e.Handled = true;
                    break;
                case Key.Down:
                    _editor.HandleNavigation(Key.Down, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
                    e.Handled = true;
                    break;
            }
        }

        private void OnTextInput(int paragraphIndex, TextInputEventArgs e)
        {
            _currentParagraphIndex = paragraphIndex;

            if (!string.IsNullOrEmpty(e.Text))
            {
                _editor.RequestSaveState();
                
                if (_editor.HasSelection()) _editor.DeleteSelection();
                InsertTextAtCaret(e.Text);
                _editor.UpdateCaretPosition();
                _editor.ScrollToCaret();
                // Verificar overflow
                _editor.CheckPageOverflow(PageIndex);
                e.Handled = true;

                _editor.UpdateCaretPosition(); // UPDATE CARET
            }
        }

        private void InsertTextAtCaret(string text)
        {
            if (_currentParagraphIndex < 0 || _currentParagraphIndex >= _paragraphs.Count) return;

            var para = _paragraphs[_currentParagraphIndex];
            int caretPos = para.TextBlock.SelectionEnd;
            int totalLen = para.Runs.Sum(r => r.Text.Length);

            // Clamp caret position
            if (caretPos < 0) caretPos = 0;
            if (caretPos > totalLen) caretPos = totalLen;

            var newRun = new FormattedRun
            {
                Text = text,
                FontWeight = _editor.CurrentFontWeight,
                FontStyle = _editor.CurrentFontStyle,
                TextDecorations = _editor.CurrentTextDecorations,
                Foreground = _editor.CurrentForeground,
                Background = _editor.CurrentBackground,
                FontFamily = _editor.CurrentFontFamily,
                FontSize = _editor.CurrentFontSize
            };

            // Find which run the caret falls into
            int runOffset = 0;
            bool inserted = false;
            for (int i = 0; i < para.Runs.Count; i++)
            {
                var run = para.Runs[i];
                int runStart = runOffset;
                int runEnd = runOffset + run.Text.Length;

                if (caretPos >= runStart && caretPos <= runEnd)
                {
                    int indexInRun = caretPos - runStart;

                    if (run.HasSameFormat(newRun))
                    {
                        // Same format: just insert text into this run
                        run.Text = run.Text.Insert(indexInRun, text);
                    }
                    else if (indexInRun == 0 && i > 0 && para.Runs[i - 1].HasSameFormat(newRun))
                    {
                        // At the start of this run, previous run has same format
                        para.Runs[i - 1].Text += text;
                    }
                    else if (indexInRun == run.Text.Length && i < para.Runs.Count - 1 && para.Runs[i + 1].HasSameFormat(newRun))
                    {
                        // At the end of this run, next run has same format
                        para.Runs[i + 1].Text = text + para.Runs[i + 1].Text;
                    }
                    else if (indexInRun == 0)
                    {
                        para.Runs.Insert(i, newRun);
                    }
                    else if (indexInRun == run.Text.Length)
                    {
                        para.Runs.Insert(i + 1, newRun);
                    }
                    else
                    {
                        // Split the run at the caret position
                        var beforeText = run.Text.Substring(0, indexInRun);
                        var afterText = run.Text.Substring(indexInRun);
                        run.Text = beforeText;
                        para.Runs.Insert(i + 1, newRun);
                        para.Runs.Insert(i + 2, run.Clone(afterText));
                    }
                    inserted = true;
                    break;
                }
                runOffset = runEnd;
            }

            // Fallback: if paragraph is empty or caret is at the very end
            if (!inserted)
            {
                if (para.Runs.Count == 1 && string.IsNullOrEmpty(para.Runs[0].Text))
                    para.Runs[0] = newRun;
                else if (para.Runs.Count > 0 && para.Runs[^1].HasSameFormat(newRun))
                    para.Runs[^1].Text += text;
                else
                    para.Runs.Add(newRun);
            }

            ClearSavedSelection();
            RefreshParagraph(_currentParagraphIndex);

            // Update caret to after inserted text
            int newCaretPos = caretPos + text.Length;
            para.TextBlock.SelectionStart = newCaretPos;
            para.TextBlock.SelectionEnd = newCaretPos;
        }

        private void DeleteCharacterAtCaret()
        {
            if (_currentParagraphIndex < 0 || _currentParagraphIndex >= _paragraphs.Count) return;

            var para = _paragraphs[_currentParagraphIndex];
            if (para.Runs.Count == 0) return;

            int caretPos = para.TextBlock.SelectionEnd;
            int totalLen = para.Runs.Sum(r => r.Text.Length);

            if (caretPos <= 0)
            {
                // Caret is at the beginning: merge with previous paragraph or do nothing
                if (_paragraphs.Count > 1 && _currentParagraphIndex > 0)
                {
                    RemoveParagraphListeners(para.TextBlock);
                    // Merge current paragraph's runs into previous paragraph
                    var prevPara = _paragraphs[_currentParagraphIndex - 1];
                    int prevLen = prevPara.Runs.Sum(r => r.Text.Length);
                    foreach (var run in para.Runs)
                    {
                        if (!string.IsNullOrEmpty(run.Text))
                            prevPara.Runs.Add(run);
                    }
                    _paragraphContainer.Children.RemoveAt(_currentParagraphIndex);
                    _paragraphs.RemoveAt(_currentParagraphIndex);
                    _currentParagraphIndex--;
                    _paragraphs[_currentParagraphIndex].TextBlock.Focus();

                    ClearSavedSelection();
                    RefreshParagraph(_currentParagraphIndex);

                    prevPara.TextBlock.SelectionStart = prevLen;
                    prevPara.TextBlock.SelectionEnd = prevLen;
                }
                return;
            }

            // Find the character at caretPos - 1 and remove it
            int deletePos = caretPos - 1;
            int runOffset = 0;
            for (int i = 0; i < para.Runs.Count; i++)
            {
                var run = para.Runs[i];
                int runStart = runOffset;
                int runEnd = runOffset + run.Text.Length;

                if (deletePos >= runStart && deletePos < runEnd)
                {
                    int indexInRun = deletePos - runStart;
                    run.Text = run.Text.Remove(indexInRun, 1);

                    // Remove empty runs (but keep at least one)
                    if (string.IsNullOrEmpty(run.Text))
                    {
                        if (para.Runs.Count > 1)
                            para.Runs.RemoveAt(i);
                        // else leave the empty run
                    }
                    break;
                }
                runOffset = runEnd;
            }

            ClearSavedSelection();
            RefreshParagraph(_currentParagraphIndex);

            // Move caret back by 1
            int newCaretPos = Math.Max(0, caretPos - 1);
            para.TextBlock.SelectionStart = newCaretPos;
            para.TextBlock.SelectionEnd = newCaretPos;
        }

        private void DeleteForwardAtCaret()
        {
            if (_currentParagraphIndex < 0 || _currentParagraphIndex >= _paragraphs.Count) return;

            var para = _paragraphs[_currentParagraphIndex];
            if (para.Runs.Count == 0) return;

            int caretPos = para.TextBlock.SelectionEnd;
            int totalLen = para.Runs.Sum(r => r.Text.Length);

            if (caretPos >= totalLen)
            {
                // Caret is at the end: merge with next paragraph
                if (_currentParagraphIndex < _paragraphs.Count - 1)
                {
                    var nextPara = _paragraphs[_currentParagraphIndex + 1];
                    RemoveParagraphListeners(nextPara.TextBlock);
                    foreach (var run in nextPara.Runs)
                    {
                        if (!string.IsNullOrEmpty(run.Text))
                            para.Runs.Add(run);
                    }
                    _paragraphContainer.Children.RemoveAt(_currentParagraphIndex + 1);
                    _paragraphs.RemoveAt(_currentParagraphIndex + 1);

                    ClearSavedSelection();
                    RefreshParagraph(_currentParagraphIndex);

                    para.TextBlock.SelectionStart = caretPos;
                    para.TextBlock.SelectionEnd = caretPos;
                }
                return;
            }

            // Find the character at caretPos and remove it
            int runOffset = 0;
            for (int i = 0; i < para.Runs.Count; i++)
            {
                var run = para.Runs[i];
                int runStart = runOffset;
                int runEnd = runOffset + run.Text.Length;

                if (caretPos >= runStart && caretPos < runEnd)
                {
                    int indexInRun = caretPos - runStart;
                    run.Text = run.Text.Remove(indexInRun, 1);

                    if (string.IsNullOrEmpty(run.Text))
                    {
                        if (para.Runs.Count > 1)
                            para.Runs.RemoveAt(i);
                    }
                    break;
                }
                runOffset = runEnd;
            }

            ClearSavedSelection();
            RefreshParagraph(_currentParagraphIndex);

            // Caret stays at the same position
            para.TextBlock.SelectionStart = caretPos;
            para.TextBlock.SelectionEnd = caretPos;
        }

        public TextLayout CreateCaretLayout(SelectableTextBlock tb)
        {
            var text = tb.Text ?? "";
            var typeface = new Typeface(tb.FontFamily, tb.FontStyle, tb.FontWeight);
            return new TextLayout(
                text,
                typeface,
                tb.FontSize,
                Brushes.Black,
                tb.TextAlignment,
                tb.TextWrapping,
                maxWidth: tb.Bounds.Width,
                maxHeight: double.PositiveInfinity
            );
        }

        public void DeleteSelectedText()
        {
            if (!HasSelection) return;

            // Iterate ALL paragraphs to delete selection
            // We iterate backwards to avoid index issues when removing
            for (int i = _paragraphs.Count - 1; i >= 0; i--)
            {
                var para = _paragraphs[i];
                var tb = para.TextBlock;

                int start = 0;
                int end = 0;

                if (i == _currentParagraphIndex && _savedSelectionStart >= 0)
                {
                    start = Math.Min(_savedSelectionStart, _savedSelectionEnd);
                    end = Math.Max(_savedSelectionStart, _savedSelectionEnd);
                }
                else
                {
                    if (tb.SelectionStart == tb.SelectionEnd) continue;
                    start = Math.Min(tb.SelectionStart, tb.SelectionEnd);
                    end = Math.Max(tb.SelectionStart, tb.SelectionEnd);
                }

                if (start >= end) continue;

                var newRuns = new List<FormattedRun>();
                int currentPos = 0;

                foreach (var run in para.Runs)
                {
                    int runStart = currentPos;
                    int runEnd = currentPos + run.Text.Length;

                    if (runEnd <= start || runStart >= end)
                    {
                        // Keep run
                        newRuns.Add(run);
                    }
                    else if (runStart >= start && runEnd <= end)
                    {
                        // Remove run (skip)
                    }
                    else
                    {
                        // Partial
                        string newText = "";
                        if (runStart < start) newText += run.Text.Substring(0, start - runStart);
                        if (runEnd > end) newText += run.Text.Substring(end - runStart);
                        if (!string.IsNullOrEmpty(newText)) newRuns.Add(run.Clone(newText));
                    }
                    currentPos = runEnd;
                }

                para.Runs = newRuns.Count > 0 ? newRuns : new List<FormattedRun> { new FormattedRun { Text = "" } };

                // If paragraph is empty and we selected the whole thing, remove it?
                // Logic: If I selected the whole line, I expect it to disappear.
                // But we must keep at least one paragraph? 
                // Or leave empty paragraph?
                // Standard behavior: text is deleted. Paragraph remains if it wasn't merged. A full line selection and delete often leaves an empty line or pulls lines up.
                // For simplicity: Clear text.
                // If it was already empty?

                RefreshParagraph(i);

                // Reset selection
                tb.SelectionStart = 0;
                tb.SelectionEnd = 0;
            }

            ClearSavedSelection();
            // If current paragraph index was affected, ensure it's valid
            if (_currentParagraphIndex >= _paragraphs.Count) _currentParagraphIndex = _paragraphs.Count - 1;
        }

        public void InsertText(string text)
        {
            // Handle newlines by splitting text
            string[] lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            for (int k = 0; k < lines.Length; k++)
            {
                if (k > 0)
                {
                    // Add new paragraph
                    AddNewParagraph(_editor.CurrentAlignment);
                    _currentParagraphIndex = _paragraphs.Count - 1;
                    _paragraphs[_currentParagraphIndex].TextBlock.Focus();
                }

                InsertTextAtCaret(lines[k]);
            }
        }

        public void ToggleBold()
        {
            _editor.CurrentFontWeight = _editor.CurrentFontWeight == FontWeight.Bold ? FontWeight.Normal : FontWeight.Bold;
            if (HasSelection) ApplyFormatToSelection(r => r.FontWeight = _editor.CurrentFontWeight);
        }

        public void ToggleItalic()
        {
            _editor.CurrentFontStyle = _editor.CurrentFontStyle == FontStyle.Italic ? FontStyle.Normal : FontStyle.Italic;
            if (HasSelection) ApplyFormatToSelection(r => r.FontStyle = _editor.CurrentFontStyle);
        }

        public void ToggleUnderline()
        {
            _editor.CurrentTextDecorations = _editor.CurrentTextDecorations == null ? TextDecorations.Underline : null;
            if (HasSelection) ApplyFormatToSelection(r => r.TextDecorations = _editor.CurrentTextDecorations);
        }

        public void ApplyCurrentFormat()
        {
            if (HasSelection)
            {
                ApplyFormatToSelection(r =>
                {
                    r.FontWeight = _editor.CurrentFontWeight;
                    r.FontStyle = _editor.CurrentFontStyle;
                    r.TextDecorations = _editor.CurrentTextDecorations;
                    r.Foreground = _editor.CurrentForeground;
                    r.Background = _editor.CurrentBackground;
                    r.FontFamily = _editor.CurrentFontFamily;
                    r.FontSize = _editor.CurrentFontSize;
                });
            }
        }

        public void SetAlignment(TextAlignment alignment)
        {
            if (_currentParagraphIndex >= 0 && _currentParagraphIndex < _paragraphs.Count)
            {
                _paragraphs[_currentParagraphIndex].Alignment = alignment;
                _paragraphs[_currentParagraphIndex].TextBlock.TextAlignment = alignment;
            }
        }

        public void SetLineHeight(double lineHeight)
        {
            if (_currentParagraphIndex >= 0 && _currentParagraphIndex < _paragraphs.Count)
            {
                var para = _paragraphs[_currentParagraphIndex];
                para.LineSpacing = lineHeight;
                para.TextBlock.LineHeight = para.TextBlock.FontSize * lineHeight;
            }
        }

        public void SetTextIndent(double indent)
        {
            if (_currentParagraphIndex >= 0 && _currentParagraphIndex < _paragraphs.Count)
            {
                var para = _paragraphs[_currentParagraphIndex];
                para.TextIndent = indent;
                para.TextBlock.TextIndent = indent;
            }
        }

        public void SetLeftIndent(double indent)
        {
            if (_currentParagraphIndex >= 0 && _currentParagraphIndex < _paragraphs.Count)
            {
                var para = _paragraphs[_currentParagraphIndex];
                para.LeftIndent = indent;
                para.TextBlock.Margin = new Thickness(indent, para.TextBlock.Margin.Top, para.RightIndent, para.ParagraphSpacing);
            }
        }

        public void SetRightIndent(double indent)
        {
            if (_currentParagraphIndex >= 0 && _currentParagraphIndex < _paragraphs.Count)
            {
                var para = _paragraphs[_currentParagraphIndex];
                para.RightIndent = indent;
                para.TextBlock.Margin = new Thickness(para.LeftIndent, para.TextBlock.Margin.Top, indent, para.ParagraphSpacing);
            }
        }

        private void ApplyFormatToSelection(Action<FormattedRun> formatAction)
        {
            if (!HasSelection) return;

            // Iterate ALL paragraphs to apply format to selection
            for (int i = 0; i < _paragraphs.Count; i++)
            {
                var para = _paragraphs[i];
                var tb = para.TextBlock;

                int start = 0;
                int end = 0;

                // Determine effective selection for this paragraph
                if (i == _currentParagraphIndex && _savedSelectionStart >= 0)
                {
                    // Use saved selection if this is the active paragraph (likely lost focus)
                    start = Math.Min(_savedSelectionStart, _savedSelectionEnd);
                    end = Math.Max(_savedSelectionStart, _savedSelectionEnd);
                }
                else
                {
                    // Use actual control selection
                    if (tb.SelectionStart == tb.SelectionEnd) continue;
                    start = Math.Min(tb.SelectionStart, tb.SelectionEnd);
                    end = Math.Max(tb.SelectionStart, tb.SelectionEnd);
                }

                if (start >= end) continue;

                var newRuns = new List<FormattedRun>();
                int currentPos = 0;

                foreach (var run in para.Runs)
                {
                    int runStart = currentPos;
                    int runEnd = currentPos + run.Text.Length;

                    if (runEnd <= start || runStart >= end) newRuns.Add(run);
                    else if (runStart >= start && runEnd <= end)
                    {
                        var formattedRun = run.Clone();
                        formatAction(formattedRun);
                        newRuns.Add(formattedRun);
                    }
                    else
                    {
                        int selStartInRun = Math.Max(0, start - runStart);
                        int selEndInRun = Math.Min(run.Text.Length, end - runStart);
                        if (selStartInRun > 0) newRuns.Add(run.Clone(run.Text.Substring(0, selStartInRun)));
                        if (selEndInRun > selStartInRun)
                        {
                            var selectedPart = run.Clone(run.Text.Substring(selStartInRun, selEndInRun - selStartInRun));
                            formatAction(selectedPart);
                            newRuns.Add(selectedPart);
                        }
                        if (selEndInRun < run.Text.Length) newRuns.Add(run.Clone(run.Text.Substring(selEndInRun)));
                    }
                    currentPos = runEnd;
                }

                para.Runs = newRuns.Where(r => !string.IsNullOrEmpty(r.Text)).ToList();
                if (para.Runs.Count == 0) para.Runs.Add(new FormattedRun { Text = "" });

                RefreshParagraph(i);

                // RESTORE SELECTION
                try
                {
                    // Ensure indices are valid (text length might have changed practically, though strictly formatting shouldn't change length)
                    // But defensive code is good.
                    // Note: We need to set SelectionStart/End on TextBlock.
                    // Accessing Text length from TextBlock might be slow or depend on layout update?
                    // We can sum runs length.
                    int totalLen = para.Runs.Sum(r => r.Text.Length);
                    int clampedStart = Math.Min(start, totalLen);
                    int clampedEnd = Math.Min(end, totalLen);

                    tb.SelectionStart = clampedStart;
                    tb.SelectionEnd = clampedEnd;
                }
                catch { }
            }

            // Do NOT ClearSavedSelection to allow consecutive formatting operations
            // ClearSavedSelection(); 
        }

        public void RefreshParagraph(int paragraphIndex)
        {
            if (paragraphIndex < 0 || paragraphIndex >= _paragraphs.Count) return;

            var para = _paragraphs[paragraphIndex];
            var textBlock = para.TextBlock;

            textBlock.Inlines?.Clear();
            if (textBlock.Inlines == null) return;

            foreach (var run in para.Runs)
            {
                if (string.IsNullOrEmpty(run.Text)) continue;

                var inline = new Run(run.Text)
                {
                    FontWeight = run.FontWeight,
                    FontStyle = run.FontStyle,
                    TextDecorations = run.TextDecorations,
                    Foreground = run.Foreground,
                    Background = run.Background,
                    FontFamily = run.FontFamily,
                    FontSize = run.FontSize
                };
                textBlock.Inlines.Add(inline);
            }
            
            textBlock.LineHeight = textBlock.FontSize * para.LineSpacing;
            // Margin is always LeftIndent. The FirstLineIndent is handled by the Render logic.
            textBlock.Margin = new Thickness(para.LeftIndent, 0, para.RightIndent, para.ParagraphSpacing);
            textBlock.TextIndent = para.TextIndent;

            if (!textBlock.Inlines.Any()) textBlock.Inlines.Add(new Run(" ") { Foreground = Brushes.Transparent });
        }

        public string GetPlainText() => string.Join("\n", _paragraphs.Select(p => string.Join("", p.Runs.Select(r => r.Text))));
    }

    public class Paragraph
    {
        public SelectableTextBlock TextBlock { get; set; } = null!;
        public List<FormattedRun> Runs { get; set; } = new();
        public TextAlignment Alignment { get; set; } = TextAlignment.Left;
        public double LineSpacing { get; set; } = 1.2;
        public double ParagraphSpacing { get; set; } = 10;
        public double TextIndent { get; set; } = 0;
        public double LeftIndent { get; set; } = 0;
        public double RightIndent { get; set; } = 0;
    }

    public class FormattedRun
    {
        public string Text { get; set; } = "";
        public FontWeight FontWeight { get; set; } = FontWeight.Normal;
        public FontStyle FontStyle { get; set; } = FontStyle.Normal;
        public TextDecorationCollection? TextDecorations { get; set; }
        public IBrush Foreground { get; set; } = Brushes.Black;
        public IBrush? Background { get; set; }
        public FontFamily FontFamily { get; set; } = new FontFamily("Calibri");
        public double FontSize { get; set; } = 14;

        public FormattedRun Clone(string? newText = null)
        {
            return new FormattedRun
            {
                Text = newText ?? Text,
                FontWeight = FontWeight,
                FontStyle = FontStyle,
                TextDecorations = TextDecorations,
                Foreground = Foreground,
                Background = Background,
                FontFamily = FontFamily,
                FontSize = FontSize
            };
        }

        public bool HasSameFormat(FormattedRun other)
        {
            return FontWeight == other.FontWeight &&
                   FontStyle == other.FontStyle &&
                   TextDecorations == other.TextDecorations &&
                   Foreground == other.Foreground &&
                   Background == other.Background &&
                   FontFamily.Name == other.FontFamily.Name &&
                   Math.Abs(FontSize - other.FontSize) < 0.01;
        }
    }

    public class DocumentRuler : Control
    {
        public event Action<double, bool>? OnGuidelineUpdate;
        private enum MarkerType { None, FirstLine, Hanging, Left, Right }
        private readonly CustomRichTextEditor _editor;
        private double _zoom = 1.0;
        private MarkerType _draggingMarker = MarkerType.None;
        private double _dragStartX;
        private double _dragStartLeft;
        private double _dragStartIndent;
        private double _dragStartRight;
        private Point _guidelinePoint;

        public DocumentRuler(CustomRichTextEditor editor)
        {
            _editor = editor;
            Height = 28; // Senior Height (Compact)
            Cursor = new Cursor(StandardCursorType.Ibeam);
            ClipToBounds = true;
        }

        public void SetZoom(double zoom)
        {
            _zoom = zoom;
            InvalidateVisual();
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            var pos = e.GetPosition(this);
            var hit = HitTestMarker(pos.X);
            if (hit != MarkerType.None)
            {
                // Ensure editor properties are synced before dragging starts
                // (Para evitar saltos si el editor tenía valores viejos)
                _draggingMarker = hit;
                _dragStartX = pos.X;
                _dragStartLeft = _editor.CurrentLeftIndent;
                _dragStartIndent = _editor.CurrentTextIndent;
                _dragStartRight = _editor.CurrentRightIndent;
                _guidelinePoint = pos;
                OnGuidelineUpdate?.Invoke(pos.X, true);
                e.Pointer.Capture(this);
                e.Handled = true;
            }
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            var pos = e.GetPosition(this);

            if (_draggingMarker != MarkerType.None)
            {
                double delta = (pos.X - _dragStartX) / _zoom;
                _guidelinePoint = pos;
                OnGuidelineUpdate?.Invoke(pos.X, true);

                switch (_draggingMarker)
                {
                    case MarkerType.FirstLine:
                        _editor.SetTextIndent(_dragStartIndent + delta);
                        break;
                    case MarkerType.Left:
                        _editor.SetLeftIndent(Math.Max(0, _dragStartLeft + delta));
                        break;
                    case MarkerType.Hanging:
                        double newLeft = Math.Max(0, _dragStartLeft + delta);
                        double actualDelta = newLeft - _dragStartLeft;
                        _editor.SetLeftIndent(newLeft);
                        _editor.SetTextIndent(_dragStartIndent - actualDelta);
                        break;
                    case MarkerType.Right:
                        _editor.SetRightIndent(Math.Max(0, _dragStartRight - delta));
                        break;
                }
                InvalidateVisual();
                e.Handled = true;
            }
            else
            {
                _guidelinePoint = pos;
                var hit = HitTestMarker(pos.X);
                Cursor = (hit != MarkerType.None) ? new Cursor(StandardCursorType.SizeWestEast) : new Cursor(StandardCursorType.Arrow);
            }
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            _draggingMarker = MarkerType.None;
            OnGuidelineUpdate?.Invoke(0, false);
            e.Pointer.Capture(null);
            InvalidateVisual();
        }

        private MarkerType HitTestMarker(double x)
        {
            var pages = _editor.Pages;
            if (pages.Count == 0 || _editor.CurrentPage - 1 >= pages.Count) return MarkerType.None;

            var page = pages[_editor.CurrentPage - 1];
            var transform = page.PageBorder.TransformToVisual(this);
            if (!transform.HasValue) return MarkerType.None;

            var origin = transform.Value.Transform(new Point(0, 0));
            double startX = origin.X + (96 * _zoom); // Content start (0 point)
            double pWidth = 624 * _zoom;

            double leftX = startX + (_editor.CurrentLeftIndent * _zoom);
            double firstX = leftX + (_editor.CurrentTextIndent * _zoom); // RELATIVE TO LEFT
            double rightX = startX + pWidth - (_editor.CurrentRightIndent * _zoom);

            if (Math.Abs(x - firstX) < 12) return MarkerType.FirstLine;
            if (Math.Abs(x - leftX) < 12)
            {
                var mousePos = _guidelinePoint; 
                // Track is Y=[12, 24]. Center is 18.
                return mousePos.Y < 18 ? MarkerType.Hanging : MarkerType.Left;
            }
            if (Math.Abs(x - rightX) < 12) return MarkerType.Right;

            return MarkerType.None;
        }

        private double GetValueForMarker(MarkerType type)
        {
            return type switch
            {
                MarkerType.FirstLine => _editor.CurrentTextIndent,
                MarkerType.Left => _editor.CurrentLeftIndent,
                MarkerType.Right => _editor.CurrentRightIndent,
                _ => 0
            };
        }

        public override void Render(DrawingContext context)
        {
            using (context.PushClip(new Rect(Bounds.Size)))
            {
                base.Render(context);

                // Background (Elite UI Slate)
                IBrush? bg = null;
                if (_editor.TryGetResource("SecondaryBackground", out var bgRes) && bgRes is IBrush bgBrush) bg = bgBrush;
                bg ??= new SolidColorBrush(Color.Parse("#F8FAFC"));
                context.DrawRectangle(bg, null, new Rect(0, 0, Bounds.Width, Bounds.Height));

                var pages = _editor.Pages;
                if (pages.Count == 0) return;

                IBrush? tickBrush = null;
                if (_editor.TryGetResource("BorderColor", out var tickRes) && tickRes is IBrush tBrush) tickBrush = tBrush;
                var tickPen = new Pen(tickBrush ?? new SolidColorBrush(Color.Parse("#CBD5E1")), 1);

                IBrush? textBrush = null;
                if (_editor.TryGetResource("SecondaryText", out var textRes) && textRes is IBrush txBrush) textBrush = txBrush;
                textBrush ??= new SolidColorBrush(Color.Parse("#64748B"));

                var activeBrush = Brushes.White; // Paper surface always white as requested

                IBrush? marginBrush = null;
                if (_editor.TryGetResource("SecondaryBackground", out var marginRes) && marginRes is IBrush mBrush) marginBrush = mBrush;
                marginBrush ??= new SolidColorBrush(Color.Parse("#F1F5F9"));

                IBrush? markerBrush = null;
                if (_editor.TryGetResource("PrimaryText", out var markerRes) && markerRes is IBrush mkBrush) markerBrush = mkBrush;
                markerBrush ??= new SolidColorBrush(Color.Parse("#475569"));

                int activeIdx = _editor.CurrentPage - 1;
                if (activeIdx >= 0 && activeIdx < pages.Count)
                {
                    var page = pages[activeIdx];
                    var transform = page.PageBorder.TransformToVisual(this);
                    if (transform.HasValue)
                    {
                        var pageOrigin = transform.Value.Transform(new Point(0, 0));
                        double pageWidth = 816 * _zoom;
                        double marginWidth = 96 * _zoom;
                        double contentStart = pageOrigin.X + marginWidth;
                        double surfaceY = 12;
                        double surfaceH = 12;

                        // Page Surface
                        var pageRect = new Rect(pageOrigin.X, surfaceY, pageWidth, surfaceH);
                        context.DrawRectangle(activeBrush, null, pageRect);
                        
                        // Margins (Gray)
                        context.DrawRectangle(marginBrush, null, new Rect(pageOrigin.X, surfaceY, marginWidth, surfaceH));
                        context.DrawRectangle(marginBrush, null, new Rect(pageOrigin.X + pageWidth - marginWidth, surfaceY, marginWidth, surfaceH));

                        // Ticks & Numbers (Microsoft Word Logic: 0 is at contentStart)
                        double tickStep = (96.0 / 8.0) * _zoom; // 1/8 inch
                        
                        // Left Margin Ticks (Negative)
                        for (int i = 1; i <= 8; i++)
                        {
                            double x = contentStart - (i * tickStep);
                            if (x < pageOrigin.X) break;
                            double tickH = (i % 8 == 0) ? 8 : (i % 4 == 0 ? 5 : 3);
                            context.DrawLine(tickPen, new Point(x, surfaceY + surfaceH - tickH), new Point(x, surfaceY + surfaceH));
                            
                            if (i % 8 == 0) // Number 1 (negative)
                            {
                                var ft = new FormattedText("1", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Inter,Arial"), 9, textBrush);
                                context.DrawText(ft, new Point(x - (ft.Width/2), surfaceY - 10));
                            }
                        }

                        // Content & Right Margin Ticks (Positive)
                        for (int i = 0; i <= (pageWidth - marginWidth) / tickStep; i++)
                        {
                            double x = contentStart + (i * tickStep);
                            if (x > pageOrigin.X + pageWidth) break;
                            
                            double tickH = (i % 8 == 0) ? 8 : (i % 4 == 0 ? 5 : 3);
                            context.DrawLine(tickPen, new Point(x, surfaceY + surfaceH - tickH), new Point(x, surfaceY + surfaceH));

                            if (i % 8 == 0)
                            {
                                var ft = new FormattedText((i / 8).ToString(), System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Inter,Arial"), 9, textBrush);
                                context.DrawText(ft, new Point(x - (ft.Width/2), surfaceY - 10));
                            }
                        }

                        // Markers
                        double leftX = contentStart + (_editor.CurrentLeftIndent * _zoom);
                        double firstX = leftX + (_editor.CurrentTextIndent * _zoom); // RELATIVE TO LEFT
                        double rightX = contentStart + (624 * _zoom) - (_editor.CurrentRightIndent * _zoom);

                        DrawIndentMarker(context, firstX, surfaceY, true, false, markerBrush);
                        DrawIndentMarker(context, leftX, surfaceY + surfaceH, false, true, markerBrush);
                        DrawIndentMarker(context, rightX, surfaceY + surfaceH, false, false, markerBrush);
                    }
                }
            }
        }

        private void DrawIndentMarker(DrawingContext context, double x, double y, bool top, bool hasSquare, IBrush brush)
        {
            if (x < 0 || x > Bounds.Width) return;
            
            double size = 5 * _zoom;
            var path = new StreamGeometry();
            using (var ctx = path.Open())
            {
                if (top)
                {
                    // Downward Triangle (First Line)
                    ctx.BeginFigure(new Point(x - size, y), true);
                    ctx.LineTo(new Point(x + size, y));
                    ctx.LineTo(new Point(x, y + size));
                }
                else
                {
                    // Upward Triangle (Hanging)
                    // Sit on the bottom of the track (y is floor)
                    double bottomY = y;
                    ctx.BeginFigure(new Point(x, bottomY - size - (hasSquare ? 7 : 0)), true);
                    ctx.LineTo(new Point(x - size, bottomY - (hasSquare ? 7 : 0)));
                    ctx.LineTo(new Point(x + size, bottomY - (hasSquare ? 7 : 0)));
                    
                    if (hasSquare)
                    {
                        // Rectangle below
                        double rectH = 7 * _zoom;
                        ctx.BeginFigure(new Point(x - size, bottomY - rectH), true);
                        ctx.LineTo(new Point(x + size, bottomY - rectH));
                        ctx.LineTo(new Point(x + size, bottomY));
                        ctx.LineTo(new Point(x - size, bottomY));
                    }
                }
            }
            context.DrawGeometry(brush, null, path);
        }
    }

    public class SelectableTextBlock : Control
    {
        public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<SelectableTextBlock, string?>(nameof(Text));
        public static readonly StyledProperty<TextWrapping> TextWrappingProperty = AvaloniaProperty.Register<SelectableTextBlock, TextWrapping>(nameof(TextWrapping));
        public static readonly StyledProperty<TextAlignment> TextAlignmentProperty = AvaloniaProperty.Register<SelectableTextBlock, TextAlignment>(nameof(TextAlignment));
        public static readonly StyledProperty<double> LineHeightProperty = AvaloniaProperty.Register<SelectableTextBlock, double>(nameof(LineHeight));
        public static readonly StyledProperty<double> TextIndentProperty = AvaloniaProperty.Register<SelectableTextBlock, double>(nameof(TextIndent));
        public static readonly StyledProperty<IBrush?> SelectionBrushProperty = AvaloniaProperty.Register<SelectableTextBlock, IBrush?>(nameof(SelectionBrush));
        public static readonly StyledProperty<FontFamily> FontFamilyProperty = AvaloniaProperty.Register<SelectableTextBlock, FontFamily>(nameof(FontFamily), new FontFamily("Calibri"));
        public static readonly StyledProperty<double> FontSizeProperty = AvaloniaProperty.Register<SelectableTextBlock, double>(nameof(FontSize), 14);
        public static readonly StyledProperty<FontWeight> FontWeightProperty = AvaloniaProperty.Register<SelectableTextBlock, FontWeight>(nameof(FontWeight), FontWeight.Normal);
        public static readonly StyledProperty<FontStyle> FontStyleProperty = AvaloniaProperty.Register<SelectableTextBlock, FontStyle>(nameof(FontStyle), FontStyle.Normal);
        public static readonly StyledProperty<IBrush> ForegroundProperty = AvaloniaProperty.Register<SelectableTextBlock, IBrush>(nameof(Foreground), Brushes.Black);
        public static readonly StyledProperty<IBrush?> BackgroundProperty = AvaloniaProperty.Register<SelectableTextBlock, IBrush?>(nameof(Background));

        public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
        public TextWrapping TextWrapping { get => GetValue(TextWrappingProperty); set => SetValue(TextWrappingProperty, value); }
        public TextAlignment TextAlignment { get => GetValue(TextAlignmentProperty); set => SetValue(TextAlignmentProperty, value); }
        public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }
        public double TextIndent { get => GetValue(TextIndentProperty); set => SetValue(TextIndentProperty, value); }
        public IBrush? SelectionBrush { get => GetValue(SelectionBrushProperty); set => SetValue(SelectionBrushProperty, value); }
        public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
        public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
        public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
        public FontStyle FontStyle { get => GetValue(FontStyleProperty); set => SetValue(FontStyleProperty, value); }
        public IBrush Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
        public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

        private int _selectionStart;
        private int _selectionEnd;

        public int SelectionStart 
        { 
            get => _selectionStart; 
            set { if (_selectionStart != value) { _selectionStart = value; SelectionChanged?.Invoke(this, EventArgs.Empty); InvalidateVisual(); } } 
        }
        
        public int SelectionEnd 
        { 
            get => _selectionEnd; 
            set { if (_selectionEnd != value) { _selectionEnd = value; SelectionChanged?.Invoke(this, EventArgs.Empty); InvalidateVisual(); } } 
        }

        public event EventHandler? SelectionChanged;

        public InlineCollection Inlines { get; } = new InlineCollection();

        static SelectableTextBlock()
        {
            FocusableProperty.OverrideDefaultValue<SelectableTextBlock>(true);
            AffectsRender<SelectableTextBlock>(TextProperty, TextIndentProperty, TextAlignmentProperty, TextWrappingProperty);
        }

        public SelectableTextBlock()
        {
            Inlines.CollectionChanged += (s, e) => { InvalidateVisual(); UpdateText(); };
        }

        private void UpdateText()
        {
            Text = string.Join("", Inlines.OfType<Run>().Select(r => r.Text));
        }

        public void SelectAll() { _selectionStart = 0; _selectionEnd = Text?.Length ?? 0; InvalidateVisual(); }

        public void ClearSelection() { _selectionStart = 0; _selectionEnd = 0; InvalidateVisual(); }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            Focus();
            var pos = e.GetPosition(this);
            int idx = GetCaretIndexAt(pos);

            if (e.ClickCount == 2)
            {
                // Double Click -> Select Word
                SelectWordAt(idx);
            }
            else if (e.ClickCount >= 3)
            {
                // Triple Click -> Select Paragraph
                SelectAll();
            }
            else
            {
                SelectionStart = SelectionEnd = idx;
            }
            InvalidateVisual();
        }

        private void SelectWordAt(int index)
        {
            if (string.IsNullOrEmpty(Text)) return;
            int start = index;
            int end = index;

            while (start > 0 && !char.IsWhiteSpace(Text[start - 1])) start--;
            while (end < Text.Length && !char.IsWhiteSpace(Text[end])) end++;

            SelectionStart = start;
            SelectionEnd = end;
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            // PointerMoved is now handled globally by CustomRichTextEditor
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            if (e.Pointer.Captured == this)
            {
                e.Pointer.Capture(null);
            }
        }


        public override void Render(DrawingContext context)
        {
            var layout = CreateTextLayout(Bounds.Width);
            var lines = layout.TextLines;
            
            // Draw Selection
            if (SelectionStart != SelectionEnd && SelectionBrush != null)
            {
                int start = Math.Min(SelectionStart, SelectionEnd);
                int end = Math.Max(SelectionStart, SelectionEnd);
                var rects = layout.HitTestTextRange(start, end - start);
                
                double lineY = 0;
                for (int i = 0; i < lines.Count; i++)
                {
                    var line = lines[i];
                    double xOffset = (i == 0) ? TextIndent : 0;
                    double lineBottom = lineY + line.Height;
                    
                    foreach (var r in rects)
                    {
                        // Check if this rectangle belongs to this line (with a small epsilon)
                        if (r.Y >= lineY - 0.1 && r.Y < lineBottom - 0.1)
                        {
                            // Adjust rectangle for Word-like indentation
                            context.DrawRectangle(SelectionBrush, null, new Rect(r.X + xOffset, r.Y, r.Width, r.Height));
                        }
                    }
                    lineY = lineBottom;
                }
            }

            // Word-like Indentation Rendering:
            double y = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                double xOffset = (i == 0) ? TextIndent : 0;
                lines[i].Draw(context, new Point(xOffset, y));
                y += lines[i].Height;
            }
        }

        private TextLayout CreateTextLayout(double maxWidth)
        {
            var typeface = new Typeface(FontFamily, FontStyle, FontWeight);
            var spans = new List<ValueSpan<TextRunProperties>>();
            int currentOffset = 0;

            foreach (var run in Inlines.OfType<Run>())
            {
                if (string.IsNullOrEmpty(run.Text)) continue;

                var runTypeface = new Typeface(run.FontFamily, run.FontStyle, run.FontWeight);
                var props = new GenericTextRunProperties(
                    runTypeface,
                    run.FontSize,
                    foregroundBrush: run.Foreground,
                    backgroundBrush: run.Background,
                    textDecorations: run.TextDecorations
                );

                spans.Add(new ValueSpan<TextRunProperties>(currentOffset, run.Text.Length, props));
                currentOffset += run.Text.Length;
            }

            return new TextLayout(
                Text ?? "",
                typeface,
                FontSize,
                Foreground,
                TextAlignment,
                TextWrapping,
                maxWidth: Math.Max(1, maxWidth - Math.Abs(TextIndent)),
                maxHeight: double.PositiveInfinity,
                textStyleOverrides: spans
            );
        }

        public int GetCaretIndexAt(Point pos)
        {
            if (string.IsNullOrEmpty(Text)) return 0;
            var layout = CreateTextLayout(Bounds.Width);
            
            double currentY = 0;
            for (int i = 0; i < layout.TextLines.Count; i++)
            {
                var line = layout.TextLines[i];
                if (pos.Y < currentY + line.Height || i == layout.TextLines.Count - 1)
                {
                    double xOffset = (i == 0) ? TextIndent : 0;
                    var hit = layout.HitTestPoint(new Point(pos.X - xOffset, pos.Y));
                    return hit.TextPosition;
                }
                currentY += line.Height;
            }
            return Text.Length;
        }
    }
}
