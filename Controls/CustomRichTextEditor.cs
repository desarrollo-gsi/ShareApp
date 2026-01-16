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
using Avalonia.Media.TextFormatting;
using System.Reflection;

namespace AvaloniaShareApp.Controls
{
    public class CustomRichTextEditor : UserControl
    {
        private StackPanel _pagesContainer;
        private List<PageControl> _pages = new();
        private (int PageIndex, int ParagraphIndex) _selectionAnchor;
        private bool _isDragging = false;
        private int _currentPageIndex = 0;
        
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

        public int PageCount => _pages.Count;
        public int CurrentPage => _currentPageIndex + 1;

        // Caret
        private Border _caret;
        private Canvas _caretCanvas;
        private DispatcherTimer _caretTimer;
        private bool _caretVisible = true;

        public CustomRichTextEditor()
        {
            _pagesContainer = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 30,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            _caret = new Border
            {
                Background = Brushes.Red, // DEBUG COLOR
                Width = 2,
                Height = 18,
                IsVisible = false,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };

            _caretCanvas = new Canvas
            {
                IsHitTestVisible = false,
                ZIndex = 9999 // Ensure On Top
            };
            _caretCanvas.Children.Add(_caret);

            // Wrap in Grid
            var grid = new Grid();
            grid.Children.Add(_pagesContainer);
            grid.Children.Add(_caretCanvas);

            Content = new ScrollViewer
            {
                Background = Brushes.LightGray,
                Padding = new Thickness(20),
                Content = grid
            };

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
            
            // Listen to global pointer to hide/show caret? 
            // Better triggered by PageControl events
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
                     
                     var transform = page.Container.TransformToVisual(_caretCanvas);
                     if (transform.HasValue)
                     {
                         var p = transform.Value.Transform(point.Value);
                         Canvas.SetLeft(_caret, p.X);
                         Canvas.SetTop(_caret, p.Y);
                         _caret.Height = page.GetCurrentLineHeight(); // Adjust height to font size
                         _caret.IsVisible = true;
                         return;
                     }
                 }
             }
             catch { }
             _caret.IsVisible = false;
        }
        public PageControl AddNextPage()
        {
            var page = new PageControl(this, _pages.Count);
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

        // Métodos de formato
        public void ToggleBold() => CurrentPage_Action(p => p.ToggleBold());
        public void ToggleBoldTo(bool isBold)
        {
            CurrentFontWeight = isBold ? FontWeight.Bold : FontWeight.Normal;
            CurrentPage_Action(p => p.ApplyCurrentFormat());
        }

        public void ToggleItalic() => CurrentPage_Action(p => p.ToggleItalic());
        public void ToggleItalicTo(bool isItalic)
        {
            CurrentFontStyle = isItalic ? FontStyle.Italic : FontStyle.Normal;
            CurrentPage_Action(p => p.ApplyCurrentFormat());
        }

        public void ToggleUnderline() => CurrentPage_Action(p => p.ToggleUnderline());
        public void ToggleUnderlineTo(bool isUnderline)
        {
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
            CurrentFontFamily = font;
            CurrentPage_Action(p => p.ApplyCurrentFormat());
        }

        public void SetFontSize(double size)
        {
            CurrentFontSize = size;
            CurrentPage_Action(p => p.ApplyCurrentFormat());
        }

        public void SetTextAlignment(TextAlignment alignment)
        {
            CurrentAlignment = alignment;
            CurrentPage_Action(p => p.SetAlignment(alignment));
        }

        private void CurrentPage_Action(Action<PageControl> action)
        {
            // Apply to all pages to ensure selection on any page is handled
            // If action is specific (e.g. alignment), it sets global state then applies to selection/current cursor.
            // PageControl logic handles "HasSelection" checks.
            foreach(var page in _pages) action(page);
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            if (!_isDragging) return;

            var point = e.GetPosition(_pagesContainer);
            int targetPageIndex = -1;
            int targetParaIndex = -1;

            for (int i = 0; i < _pages.Count; i++)
            {
                var pageControl = _pages[i];
                var pageBounds = pageControl.Container.Bounds;
                
                if (point.Y <= pageBounds.Bottom + 15)
                {
                    targetPageIndex = i;
                    var pointOnPage = e.GetPosition(pageControl.Container);
                    targetParaIndex = pageControl.GetParagraphIndexAt(pointOnPage.Y);
                    break;
                }
            }
            
            if (targetPageIndex == -1 && _pages.Count > 0)
            {
                targetPageIndex = _pages.Count - 1;
                targetParaIndex = int.MaxValue;
            }

            if (targetPageIndex != -1)
            {
                UpdateDragSelection(targetPageIndex, targetParaIndex);
            }
        }

        public void SelectAll()
        {
            foreach (var page in _pages)
            {
                page.SelectAll();
            }
        }

        public void StartDragSelection(int pageIndex, int paragraphIndex)
        {
            _isDragging = true;
            _selectionAnchor = (pageIndex, paragraphIndex);
            
            // Clear other pages initially?
            // HandlePageSelection will handle logic, but we might want to start fresh or keep Shift?
            // For pure drag, assume new selection unless Shift is held? 
            // PointerPressed in PageControl calls HandlePageSelection first, so that handles Shift check.
            // We just note that drag started.
        }

        public void UpdateDragSelection(int pageIndex, int paragraphIndex)
        {
            if (!_isDragging) return;
            // Treat as "Shift+Click" to current position from Anchor
            HandlePageSelection(pageIndex, paragraphIndex, isShiftPressed: true);
        }

        public void EndDragSelection()
        {
            _isDragging = false;
        }

        public void HandlePageSelection(int pageIndex, int paragraphIndex, bool isShiftPressed)
        {
            if (!isShiftPressed)
            {
                _selectionAnchor = (pageIndex, paragraphIndex);
                foreach (var page in _pages)
                {
                    if (page.PageIndex != pageIndex)
                    {
                        page.ClearSelection();
                    }
                }
                return;
            }

            var startPageIdx = _selectionAnchor.PageIndex;
            var startParaIdx = _selectionAnchor.ParagraphIndex;
            var endPageIdx = pageIndex;
            var endParaIdx = paragraphIndex;

            if (startPageIdx > endPageIdx || (startPageIdx == endPageIdx && startParaIdx > endParaIdx))
            {
                (startPageIdx, endPageIdx) = (endPageIdx, startPageIdx);
                (startParaIdx, endParaIdx) = (endParaIdx, startParaIdx);
            }

            foreach (var page in _pages)
            {
                if (page.PageIndex < startPageIdx || page.PageIndex > endPageIdx)
                {
                    page.ClearSelection();
                }
                else
                {
                    if (page.PageIndex > startPageIdx && page.PageIndex < endPageIdx)
                    {
                        page.SelectAll();
                    }
                    else
                    {
                        int pStart = (page.PageIndex == startPageIdx) ? startParaIdx : 0;
                        int pEnd = (page.PageIndex == endPageIdx) ? endParaIdx : int.MaxValue;
                        page.SelectRange(pStart, pEnd);
                    }
                }
            }
        }

        public string GetPlainText()
        {
            return string.Join("\n\n--- Página ---\n\n", 
                _pages.Select(p => p.GetPlainText()));
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
    }

    public class PageControl
    {
        private CustomRichTextEditor _editor;
        public int PageIndex { get; private set; }
        private StackPanel _paragraphContainer;
        private List<Paragraph> _paragraphs = new();
        private int _currentParagraphIndex = 0;
        
        // Caché de selección
        private int _savedSelectionStart = -1;
        private int _savedSelectionEnd = -1;

        public Border Container { get; private set; }

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
                Background = Brushes.White
            };

            Container = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(2),
                Width = 816,
                Height = 1056,
                Padding = new Thickness(96),
                Child = _paragraphContainer,
                Effect = new DropShadowEffect
                {
                    BlurRadius = 10,
                    Color = Color.FromArgb(153, 0, 0, 0),
                    Opacity = 0.15,
                    OffsetX = 2,
                    OffsetY = 2
                },
                ClipToBounds = true
            };

            Container.PointerPressed += (s, e) =>
            {
                _editor.SetCurrentPage(PageIndex);
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

        public void ClearSelection()
        {
            foreach (var para in _paragraphs)
            {
                para.TextBlock.SelectionStart = 0;
                para.TextBlock.SelectionEnd = 0;
            }
            ClearSavedSelection();
        }

        public double GetContentHeight()
        {
            // Forzar medida si es necesaria
            _paragraphContainer.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return _paragraphContainer.DesiredSize.Height;
        }

        public void MoveLastParagraphTo(PageControl targetPage)
        {
            if (_paragraphs.Count == 0) return;

            // Obtener el último párrafo
            var lastParaIndex = _paragraphs.Count - 1;
            var para = _paragraphs[lastParaIndex];
            
            // Verificar si tiene foco
            bool hasFocus = para.TextBlock.IsFocused;

            // Eliminar de esta página
            RemoveParagraphListeners(para.TextBlock);
            _paragraphContainer.Children.RemoveAt(lastParaIndex);
            _paragraphs.RemoveAt(lastParaIndex);
            if (_currentParagraphIndex >= _paragraphs.Count) _currentParagraphIndex = Math.Max(0, _paragraphs.Count - 1);

            // Añadir a la página destino (al principio o final? Normalmente final si estamos escribiendo)
            targetPage.AddExistingParagraph(para, atBeginning: true);

            if (hasFocus)
            {
                para.TextBlock.Focus();
                _editor.SetCurrentPage(targetPage.PageIndex);
            }
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
                MinHeight = 20
            };

            var paragraph = new Paragraph
            {
                TextBlock = textBlock,
                Runs = new List<FormattedRun> { new FormattedRun { Text = "" } },
                Alignment = alignment
            };
            
            AddParagraphToUI(paragraph, atBeginning: false);
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
                    _paragraphs[i].TextBlock.SelectAll();
                }
                else
                {
                    // Only clear if not part of anchor/current interaction? 
                    // No, HandlePageSelection clears range outside start/end pages.
                    // But inside start/end pages, we select range.
                    // This method SelectRange assumes it is "selecting purely this range".
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
                tb.TextAlignment,
                tb.TextWrapping,
                maxWidth: tb.Bounds.Width > 0 ? tb.Bounds.Width : 9999
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
            
            return new Point(hit.X, hit.Y + tb.Bounds.Y);
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
             // Adjust for Padding (96)
             double contentY = y - 96;
             if (contentY < 0) return 0; // Top margin -> First paragraph
             
             // Iterate paragraphs
             // Bounds of TextBlocks in StackPanel
             // Note: Bounds are relative to StackPanel (_paragraphContainer)
             // We can't access _paragraphContainer.Children easily? It's private.
             // But we have _paragraphs list.
             // We can iterate _paragraphContainer.Children via property? No.
             // But we can iterate _paragraphs and check their TextBlock.Bounds with parent logic?
             
             for(int i=0; i<_paragraphs.Count; i++)
             {
                 var tb = _paragraphs[i].TextBlock;
                 var bounds = tb.Bounds;
                 // StackPanel children bounds should be correct relative to StackPanel
                 if (contentY <= bounds.Bottom)
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
                    _editor.HandlePageSelection(PageIndex, idx, isShift);
                    // Start dragging logic logic
                    _editor.StartDragSelection(PageIndex, idx);
                    
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
                }
            }

            switch (e.Key)
            {
                case Key.Back:
                    if (_editor.HasSelection()) _editor.DeleteSelection();
                    else DeleteCharacterFromEnd();
                    e.Handled = true;
                    break;
                case Key.Delete:
                    if (_editor.HasSelection()) _editor.DeleteSelection();
                    e.Handled = true;
                    break;
                case Key.Enter:
                    if (_editor.HasSelection()) _editor.DeleteSelection();
                    AddNewParagraph(_editor.CurrentAlignment);
                    _paragraphs[_currentParagraphIndex].TextBlock.Focus();
                    // Verificar overflow
                    _editor.CheckPageOverflow(PageIndex);
                    e.Handled = true;
                    break;
            }
        }

        private void OnTextInput(int paragraphIndex, TextInputEventArgs e)
        {
            _currentParagraphIndex = paragraphIndex;

            if (!string.IsNullOrEmpty(e.Text))
            {
                if (_editor.HasSelection()) _editor.DeleteSelection();
                AppendText(e.Text);
                // Verificar overflow
                _editor.CheckPageOverflow(PageIndex);
                e.Handled = true;
                
                _editor.UpdateCaretPosition(); // UPDATE CARET
            }
        }

        private void AppendText(string text)
        {
            if (_currentParagraphIndex < 0 || _currentParagraphIndex >= _paragraphs.Count) return;

            var para = _paragraphs[_currentParagraphIndex];
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

            if (para.Runs.Count > 0 && para.Runs[^1].HasSameFormat(newRun))
            {
                para.Runs[^1].Text += text;
            }
            else
            {
                if (para.Runs.Count == 1 && string.IsNullOrEmpty(para.Runs[0].Text)) para.Runs[0] = newRun;
                else para.Runs.Add(newRun);
            }

            ClearSavedSelection();
            RefreshParagraph(_currentParagraphIndex);
        }

        private void DeleteCharacterFromEnd()
        {
            if (_currentParagraphIndex < 0 || _currentParagraphIndex >= _paragraphs.Count) return;

            var para = _paragraphs[_currentParagraphIndex];
            if (para.Runs.Count == 0) return;

            var lastRun = para.Runs[^1];
            if (!string.IsNullOrEmpty(lastRun.Text))
            {
                lastRun.Text = lastRun.Text.Substring(0, lastRun.Text.Length - 1);
                if (string.IsNullOrEmpty(lastRun.Text) && para.Runs.Count > 1) para.Runs.RemoveAt(para.Runs.Count - 1);
            }
            else if (_paragraphs.Count > 1 && _currentParagraphIndex > 0)
            {
                // Solo borrar párrafo si no es el único
                RemoveParagraphListeners(para.TextBlock);
                _paragraphContainer.Children.RemoveAt(_currentParagraphIndex);
                _paragraphs.RemoveAt(_currentParagraphIndex);
                _currentParagraphIndex--;
                _paragraphs[_currentParagraphIndex].TextBlock.Focus();
            }

            ClearSavedSelection();
            RefreshParagraph(_currentParagraphIndex);
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
                 
                 AppendText(lines[k]);
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
                catch {}
            }
            
            // Do NOT ClearSavedSelection to allow consecutive formatting operations
            // ClearSavedSelection(); 
        }

        private void RefreshParagraph(int paragraphIndex)
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
            if (!textBlock.Inlines.Any()) textBlock.Inlines.Add(new Run(" ") { Foreground = Brushes.Transparent });
        }

        public string GetPlainText() => string.Join("\n", _paragraphs.Select(p => string.Join("", p.Runs.Select(r => r.Text))));
    }

    public class Paragraph
    {
        public SelectableTextBlock TextBlock { get; set; } = null!;
        public List<FormattedRun> Runs { get; set; } = new();
        public TextAlignment Alignment { get; set; } = TextAlignment.Left;
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
}
