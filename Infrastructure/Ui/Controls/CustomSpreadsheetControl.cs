using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using AvaloniaShareApp.Domain.Entities;

namespace AvaloniaShareApp.Infrastructure.Ui.Controls
{
    public class CustomSpreadsheetControl : Control
    {
        private const double HeaderSize = 30;
        private const double DefaultColumnWidth = 100;
        private const double DefaultRowHeight = 25;

        public static readonly StyledProperty<Spreadsheet?> SpreadsheetProperty =
            AvaloniaProperty.Register<CustomSpreadsheetControl, Spreadsheet?>(nameof(Spreadsheet));

        public Spreadsheet? Spreadsheet
        {
            get => GetValue(SpreadsheetProperty);
            set => SetValue(SpreadsheetProperty, value);
        }

        private Spreadsheet? _spreadsheet;
        private int _currentSheetIndex = 0;
        private string _selectedCell = "A1";
        private string _selectionEndCell = "A1";
        private TextBox _editorTextBox;
        private Canvas _canvas;
        private Point _scrollOffset = new Point(0, 0);

        // Resizing state
        private int _resizingColumn = -1;
        private int _resizingRow = -1;
        private double _resizeStartPos = 0;
        private double _originalSize = 0;

        // Styling
        private readonly Pen _gridPen = new Pen(Brush.Parse("#E2E3E3"), 1);
        private readonly Pen _headerPen = new Pen(Brush.Parse("#C0C0C0"), 1);
        private readonly IBrush _headerBackground = Brush.Parse("#F8F9FA");
        private readonly IBrush _selectionBackground = Brush.Parse("#E8F0FE");
        private readonly IBrush _headerSelectionBackground = Brush.Parse("#E1E3E6");
        private readonly Pen _selectionPen = new Pen(Brush.Parse("#1A73E8"), 2);

        public event EventHandler<string>? CellChanged;

        public CustomSpreadsheetControl()
        {
            ClipToBounds = true;
            Focusable = true;

            _editorTextBox = new TextBox
            {
                IsVisible = false,
                BorderThickness = new Thickness(2),
                BorderBrush = Brush.Parse("#1A73E8"),
                Padding = new Thickness(4, 0),
                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Background = Brushes.White,
                Foreground = Brushes.Black
            };
            
            _editorTextBox.KeyDown += Editor_KeyDown;
            _editorTextBox.LostFocus += (s, e) => CommitEdit();

            _canvas = new Canvas();
            _canvas.Children.Add(_editorTextBox);

            LogicalChildren.Add(_canvas);
            VisualChildren.Add(_canvas);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            _canvas.Measure(availableSize);
            return new Size(0, 0); // Take available space dynamically
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            _canvas.Arrange(new Rect(finalSize));
            return finalSize;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == SpreadsheetProperty)
            {
                if (change.NewValue is Spreadsheet s)
                    LoadSpreadsheet(s);
            }
        }

        public void LoadSpreadsheet(Spreadsheet spreadsheet)
        {
            Console.WriteLine($"[DEBUG] LoadSpreadsheet called! ID: {spreadsheet.Id}");
            _spreadsheet = spreadsheet;
            if (_spreadsheet.Sheets.Count == 0)
            {
                _spreadsheet.Sheets.Add(new Sheet { Name = "Hoja 1" });
            }
            
            var sheet = _spreadsheet.Sheets[_currentSheetIndex];
            if (sheet.ColumnWidths.Count == 0)
                for (int i = 0; i < 26; i++) sheet.ColumnWidths.Add(DefaultColumnWidth);
            
            if (sheet.RowHeights == null) sheet.RowHeights = new List<double>();
            if (sheet.RowHeights.Count == 0)
                for (int i = 0; i < 100; i++) sheet.RowHeights.Add(DefaultRowHeight);
            
            InvalidateVisual();
        }

        private double GetColX(int col)
        {
            var sheet = _spreadsheet?.Sheets[_currentSheetIndex];
            if (sheet == null) return HeaderSize + col * DefaultColumnWidth;
            double x = HeaderSize;
            for (int i = 0; i < col; i++)
                x += i < sheet.ColumnWidths.Count ? sheet.ColumnWidths[i] : DefaultColumnWidth;
            return x;
        }

        private double GetColWidth(int col)
        {
            var sheet = _spreadsheet?.Sheets[_currentSheetIndex];
            if (sheet == null || col >= sheet.ColumnWidths.Count) return DefaultColumnWidth;
            return sheet.ColumnWidths[col];
        }

        private double GetRowY(int row)
        {
            var sheet = _spreadsheet?.Sheets[_currentSheetIndex];
            if (sheet == null || sheet.RowHeights == null) return HeaderSize + row * DefaultRowHeight;
            double y = HeaderSize;
            for (int i = 0; i < row; i++)
                y += i < sheet.RowHeights.Count ? sheet.RowHeights[i] : DefaultRowHeight;
            return y;
        }

        private double GetRowHeight(int row)
        {
            var sheet = _spreadsheet?.Sheets[_currentSheetIndex];
            if (sheet == null || sheet.RowHeights == null || row >= sheet.RowHeights.Count) return DefaultRowHeight;
            return sheet.RowHeights[row];
        }

        public override void Render(DrawingContext context)
        {
            Console.WriteLine($"[DEBUG] Render called! Bounds: {Bounds.Width}x{Bounds.Height}, Spreadsheet null? {_spreadsheet == null}");
            base.Render(context);
            
            // Draw background to ensure the control is hit-testable
            context.FillRectangle(Brushes.White, new Rect(Bounds.Size));

            if (_spreadsheet == null || _currentSheetIndex >= _spreadsheet.Sheets.Count) return;

            var sheet = _spreadsheet.Sheets[_currentSheetIndex];
            
            DrawGridAndCells(context, sheet);
            DrawSelection(context, sheet);
            DrawHeaders(context);
        }

        private void DrawHeaders(DrawingContext context)
        {
            var sheet = _spreadsheet?.Sheets[_currentSheetIndex];
            if (sheet == null) return;

            var selStart = GetCoordinates(_selectedCell);
            var selEnd = GetCoordinates(_selectionEndCell);
            var minCol = Math.Min(selStart.col, selEnd.col);
            var maxCol = Math.Max(selStart.col, selEnd.col);
            var minRow = Math.Min(selStart.row, selEnd.row);
            var maxRow = Math.Max(selStart.row, selEnd.row);

            context.FillRectangle(_headerBackground, new Rect(0, 0, HeaderSize, HeaderSize));
            context.DrawRectangle(null, _headerPen, new Rect(0, 0, HeaderSize, HeaderSize));

            // Columns
            for (int i = 0; i < 26; i++)
            {
                var x = GetColX(i) - _scrollOffset.X;
                var w = GetColWidth(i);
                if (x + w < HeaderSize) continue;
                if (x > Bounds.Width) break;

                var isSelected = i >= minCol && i <= maxCol;
                var rect = new Rect(x, 0, w, HeaderSize);
                context.FillRectangle(isSelected ? _headerSelectionBackground : _headerBackground, rect);
                context.DrawRectangle(null, _headerPen, rect);
                if (isSelected) context.FillRectangle(Brush.Parse("#1A73E8"), new Rect(x, HeaderSize - 3, w, 3));

                var label = ((char)('A' + i)).ToString();
                var text = new FormattedText(label, System.Globalization.CultureInfo.CurrentCulture, 
                    FlowDirection.LeftToRight, Typeface.Default, 12, isSelected ? Brush.Parse("#1A73E8") : Brushes.DimGray);
                context.DrawText(text, new Point(x + (w - text.Width) / 2, (HeaderSize - text.Height) / 2));
            }

            // Rows
            for (int i = 0; i < 100; i++)
            {
                var y = GetRowY(i) - _scrollOffset.Y;
                var h = GetRowHeight(i);
                if (y + h < HeaderSize) continue;
                if (y > Bounds.Height) break;

                var isSelected = i >= minRow && i <= maxRow;
                var rect = new Rect(0, y, HeaderSize, h);
                context.FillRectangle(isSelected ? _headerSelectionBackground : _headerBackground, rect);
                context.DrawRectangle(null, _headerPen, rect);
                if (isSelected) context.FillRectangle(Brush.Parse("#1A73E8"), new Rect(HeaderSize - 3, y, 3, h));

                var label = (i + 1).ToString();
                var text = new FormattedText(label, System.Globalization.CultureInfo.CurrentCulture, 
                    FlowDirection.LeftToRight, Typeface.Default, 11, isSelected ? Brush.Parse("#1A73E8") : Brushes.DimGray);
                context.DrawText(text, new Point((HeaderSize - text.Width) / 2, y + (h - text.Height) / 2));
            }
        }

        private void DrawGridAndCells(DrawingContext context, Sheet sheet)
        {
            for (int r = 0; r < 100; r++)
            {
                var y = GetRowY(r) - _scrollOffset.Y;
                var h = GetRowHeight(r);
                if (y + h < HeaderSize) continue;
                if (y > Bounds.Height) break;

                for (int c = 0; c < 26; c++)
                {
                    var x = GetColX(c) - _scrollOffset.X;
                    var w = GetColWidth(c);
                    if (x + w < HeaderSize) continue;
                    if (x > Bounds.Width) break;

                    var rect = new Rect(x, y, w, h);
                    context.DrawRectangle(null, _gridPen, rect);

                    var cellRef = GetCellReference(c, r);
                    if (sheet.Cells.TryGetValue(cellRef, out var cell))
                    {
                        if (cell.Background != null) context.FillRectangle(Brush.Parse(cell.Background), rect.Deflate(0.5));

                        var displayText = EvaluateFormula(cell.Formula, cell.Value);
                        
                        // Number Formatting
                        if (double.TryParse(displayText, out var num))
                        {
                            if (cell.Format == "Currency") displayText = num.ToString("C");
                            else if (cell.Format == "Percent") displayText = (num / 100).ToString("P1");
                        }

                        var text = new FormattedText(displayText, System.Globalization.CultureInfo.CurrentCulture, 
                            FlowDirection.LeftToRight, 
                            new Typeface(Typeface.Default.FontFamily, cell.IsItalic ? FontStyle.Italic : FontStyle.Normal, cell.IsBold ? FontWeight.Bold : FontWeight.Normal), 
                            cell.FontSize, 
                            cell.Foreground != null ? Brush.Parse(cell.Foreground) : Brushes.Black);
                        
                        double tx = x + 4;
                        if (cell.Alignment == "Center") tx = x + (w - text.Width) / 2;
                        else if (cell.Alignment == "Right") tx = x + w - text.Width - 4;

                        context.DrawText(text, new Point(tx, y + (h - text.Height) / 2));
                    }
                }
            }
        }

        private void DrawSelection(DrawingContext context, Sheet sheet)
        {
            var start = GetCoordinates(_selectedCell);
            var end = GetCoordinates(_selectionEndCell);
            
            var x1 = GetColX(Math.Min(start.col, end.col)) - _scrollOffset.X;
            var y1 = GetRowY(Math.Min(start.row, end.row)) - _scrollOffset.Y;
            
            double width = 0;
            for (int i = Math.Min(start.col, end.col); i <= Math.Max(start.col, end.col); i++) width += GetColWidth(i);
            
            double height = 0;
            for (int i = Math.Min(start.row, end.row); i <= Math.Max(start.row, end.row); i++) height += GetRowHeight(i);

            var rect = new Rect(x1, y1, width, height);
            if (_selectedCell != _selectionEndCell) context.FillRectangle(Brush.Parse("#201A73E8"), rect);
            context.DrawRectangle(null, _selectionPen, rect.Inflate(0.5));
            var handleSize = 6;
            context.FillRectangle(Brush.Parse("#1A73E8"), new Rect(x1 + width - handleSize/2, y1 + height - handleSize/2, handleSize, handleSize));
        }

        private string GetCellReference(int col, int row) => $"{(char)('A' + col)}{row + 1}";

        private (int col, int row) GetCoordinates(string reference)
        {
            var colStr = new string(reference.TakeWhile(char.IsLetter).ToArray());
            var rowStr = new string(reference.SkipWhile(char.IsLetter).ToArray());
            int col = colStr[0] - 'A';
            int row = int.Parse(rowStr) - 1;
            return (col, row);
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            var pos = e.GetPosition(this);
            var sheet = _spreadsheet?.Sheets[_currentSheetIndex];

            if (_resizingColumn != -1)
            {
                var delta = pos.X - _resizeStartPos;
                if (sheet != null) { sheet.ColumnWidths[_resizingColumn] = Math.Max(20, _originalSize + delta); InvalidateVisual(); }
                return;
            }
            if (_resizingRow != -1)
            {
                var delta = pos.Y - _resizeStartPos;
                if (sheet != null) { sheet.RowHeights[_resizingRow] = Math.Max(15, _originalSize + delta); InvalidateVisual(); }
                return;
            }

            // Cursor for resizing
            if (pos.Y < HeaderSize && pos.X > HeaderSize)
            {
                int col = -1;
                for (int i = 0; i < 26; i++)
                {
                    var x = GetColX(i) - _scrollOffset.X + GetColWidth(i);
                    if (Math.Abs(pos.X - x) < 5) { col = i; break; }
                }
                Cursor = col != -1 ? new Cursor(StandardCursorType.SizeWestEast) : Cursor.Default;
            }
            else if (pos.X < HeaderSize && pos.Y > HeaderSize)
            {
                int row = -1;
                for (int i = 0; i < 100; i++)
                {
                    var y = GetRowY(i) - _scrollOffset.Y + GetRowHeight(i);
                    if (Math.Abs(pos.Y - y) < 5) { row = i; break; }
                }
                Cursor = row != -1 ? new Cursor(StandardCursorType.SizeNorthSouth) : Cursor.Default;
            }
            else Cursor = Cursor.Default;

            if (e.Pointer.Captured == this && pos.X > HeaderSize && pos.Y > HeaderSize)
            {
                int col = 0; double curX = HeaderSize;
                for (int i = 0; i < 26; i++) { var w = GetColWidth(i); if (pos.X + _scrollOffset.X < curX + w) { col = i; break; } curX += w; col = i; }
                int row = 0; double curY = HeaderSize;
                for (int i = 0; i < 100; i++) { var h = GetRowHeight(i); if (pos.Y + _scrollOffset.Y < curY + h) { row = i; break; } curY += h; row = i; }
                var cell = GetCellReference(col, row);
                if (_selectionEndCell != cell) { _selectionEndCell = cell; InvalidateVisual(); }
            }
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            Focus();
            var pos = e.GetPosition(this);
            
            // Col Resize
            if (pos.Y < HeaderSize && pos.X > HeaderSize)
            {
                for (int i = 0; i < 26; i++)
                {
                    var x = GetColX(i) - _scrollOffset.X + GetColWidth(i);
                    if (Math.Abs(pos.X - x) < 5)
                    { _resizingColumn = i; _resizeStartPos = pos.X; _originalSize = GetColWidth(i); e.Pointer.Capture(this); return; }
                }
            }
            // Row Resize
            if (pos.X < HeaderSize && pos.Y > HeaderSize)
            {
                for (int i = 0; i < 100; i++)
                {
                    var y = GetRowY(i) - _scrollOffset.Y + GetRowHeight(i);
                    if (Math.Abs(pos.Y - y) < 5)
                    { _resizingRow = i; _resizeStartPos = pos.Y; _originalSize = GetRowHeight(i); e.Pointer.Capture(this); return; }
                }
            }

            if (pos.X > HeaderSize && pos.Y > HeaderSize)
            {
                int col = 0; double curX = HeaderSize;
                for (int i = 0; i < 26; i++) { var w = GetColWidth(i); if (pos.X + _scrollOffset.X < curX + w) { col = i; break; } curX += w; col = i; }
                int row = 0; double curY = HeaderSize;
                for (int i = 0; i < 100; i++) { var h = GetRowHeight(i); if (pos.Y + _scrollOffset.Y < curY + h) { row = i; break; } curY += h; row = i; }
                
                _selectedCell = GetCellReference(col, row);
                _selectionEndCell = _selectedCell;
                e.Pointer.Capture(this);
                InvalidateVisual();
                CellChanged?.Invoke(this, _selectedCell);
                
                if (e.ClickCount >= 2) ShowEditor(col, row);
            }
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            _resizingColumn = -1; _resizingRow = -1;
            e.Pointer.Capture(null);
        }

        private void ShowEditor(int col, int row)
        {
            var x = GetColX(col) - _scrollOffset.X;
            var y = GetRowY(row) - _scrollOffset.Y;
            var sheet = _spreadsheet?.Sheets[_currentSheetIndex];
            var cellRef = GetCellReference(col, row);
            var value = "";
            if (sheet != null && sheet.Cells.TryGetValue(cellRef, out var cell))
                value = !string.IsNullOrEmpty(cell.Formula) ? cell.Formula : cell.Value;

            _editorTextBox.Width = GetColWidth(col);
            _editorTextBox.Height = GetRowHeight(row);
            _editorTextBox.Text = value;
            _editorTextBox.IsVisible = true;
            
            Canvas.SetLeft(_editorTextBox, x);
            Canvas.SetTop(_editorTextBox, y);

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _editorTextBox.Focus();
                if (_editorTextBox.Text != null)
                {
                    _editorTextBox.CaretIndex = _editorTextBox.Text.Length;
                }
            }, Avalonia.Threading.DispatcherPriority.Loaded);
        }

        private void CommitEdit()
        {
            if (!_editorTextBox.IsVisible || _spreadsheet == null) return;
            var text = _editorTextBox.Text ?? "";
            var sheet = _spreadsheet.Sheets[_currentSheetIndex];
            if (!sheet.Cells.TryGetValue(_selectedCell, out var cell))
            {
                cell = new CellData();
                sheet.Cells[_selectedCell] = cell;
            }
            if (text.StartsWith("=")) { cell.Formula = text; cell.Value = ""; }
            else { cell.Formula = ""; cell.Value = text; }
            _editorTextBox.IsVisible = false;
            InvalidateVisual();
            CellChanged?.Invoke(this, _selectedCell);
        }

        public (string Value, string Formula) GetCellInfo(string cellRef)
        {
            var sheet = _spreadsheet?.Sheets[_currentSheetIndex];
            if (sheet != null && sheet.Cells.TryGetValue(cellRef, out var cell))
                return (cell.Value, cell.Formula);
            return ("", "");
        }

        private void Editor_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { CommitEdit(); e.Handled = true; }
            else if (e.Key == Key.Escape) { _editorTextBox.IsVisible = false; e.Handled = true; }
        }

        protected override void OnTextInput(TextInputEventArgs e)
        {
            base.OnTextInput(e);
            if (!_editorTextBox.IsVisible && !string.IsNullOrEmpty(e.Text) && !char.IsControl(e.Text[0]))
            {
                var coords = GetCoordinates(_selectedCell);
                ShowEditor(coords.col, coords.row);
                
                // Set the text to what was typed and clear any formula
                _editorTextBox.Text = e.Text;
                
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (_editorTextBox.Text != null)
                    {
                        _editorTextBox.CaretIndex = _editorTextBox.Text.Length;
                    }
                }, Avalonia.Threading.DispatcherPriority.Loaded);
                
                e.Handled = true;
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (_editorTextBox.IsVisible) return;
            var coords = GetCoordinates(_selectedCell);
            int col = coords.col;
            int row = coords.row;

            bool moved = false;
            switch (e.Key)
            {
                case Key.Up: row = Math.Max(0, row - 1); moved = true; break;
                case Key.Down: row = Math.Min(99, row + 1); moved = true; break;
                case Key.Left: col = Math.Max(0, col - 1); moved = true; break;
                case Key.Right: col = Math.Min(25, col + 1); moved = true; break;
                case Key.Enter: ShowEditor(col, row); e.Handled = true; return;
            }

            if (moved)
            {
                _selectedCell = GetCellReference(col, row);
                _selectionEndCell = _selectedCell;
                InvalidateVisual();
                CellChanged?.Invoke(this, _selectedCell);
                e.Handled = true;
            }
            // Do not set e.Handled = true unconditionally! Otherwise OnTextInput won't fire.
        }

        private string EvaluateFormula(string formula, string value)
        {
            if (string.IsNullOrEmpty(formula)) return value;
            if (!formula.StartsWith("=")) return value;
            try { return ResolveExpression(formula.Substring(1).ToUpper()); }
            catch { return "#ERROR!"; }
        }

        private string ResolveExpression(string expression)
        {
            var sheet = _spreadsheet?.Sheets[_currentSheetIndex];
            if (sheet == null) return "0";

            if (expression.Contains("("))
            {
                var match = System.Text.RegularExpressions.Regex.Match(expression, @"(SUM|AVG)\((.*)\)");
                if (match.Success)
                {
                    var func = match.Groups[1].Value;
                    var rangeStr = match.Groups[2].Value;
                    var values = GetRangeValues(rangeStr);
                    if (func == "SUM") return values.Sum().ToString();
                    if (func == "AVG") return (values.Count > 0 ? values.Average() : 0).ToString();
                }
            }

            var tokens = System.Text.RegularExpressions.Regex.Split(expression, @"([\+\-\*\/])");
            double result = 0; string lastOp = "+";
            foreach (var token in tokens)
            {
                if (string.IsNullOrWhiteSpace(token)) continue;
                if ("+-*/".Contains(token)) { lastOp = token; continue; }
                double val = 0;
                if (char.IsLetter(token[0]))
                {
                    if (sheet.Cells.TryGetValue(token, out var cell))
                    {
                        var cellVal = EvaluateFormula(cell.Formula, cell.Value);
                        double.TryParse(cellVal, out val);
                    }
                }
                else double.TryParse(token, out val);
                switch (lastOp)
                {
                    case "+": result += val; break;
                    case "-": result -= val; break;
                    case "*": result *= val; break;
                    case "/": result = val != 0 ? result / val : 0; break;
                }
            }
            return result.ToString();
        }

        private List<double> GetRangeValues(string rangeStr)
        {
            var values = new List<double>();
            var sheet = _spreadsheet?.Sheets[_currentSheetIndex];
            if (sheet == null) return values;

            if (rangeStr.Contains(":"))
            {
                var parts = rangeStr.Split(':');
                var start = GetCoordinates(parts[0]);
                var end = GetCoordinates(parts[1]);
                for (int r = Math.Min(start.row, end.row); r <= Math.Max(start.row, end.row); r++)
                {
                    for (int c = Math.Min(start.col, end.col); c <= Math.Max(start.col, end.col); c++)
                    {
                        var cellRef = GetCellReference(c, r);
                        if (sheet.Cells.TryGetValue(cellRef, out var cell))
                        {
                            var cellVal = EvaluateFormula(cell.Formula, cell.Value);
                            if (double.TryParse(cellVal, out var val)) values.Add(val);
                        }
                    }
                }
            }
            else
            {
                if (sheet.Cells.TryGetValue(rangeStr, out var cell))
                {
                    var cellVal = EvaluateFormula(cell.Formula, cell.Value);
                    if (double.TryParse(cellVal, out var val)) values.Add(val);
                }
            }
            return values;
        }

        public void SetBold() => UpdateCellFormat(c => c.IsBold = !c.IsBold);
        public void SetItalic() => UpdateCellFormat(c => c.IsItalic = !c.IsItalic);
        public void SetAlignment(string alignment) => UpdateCellFormat(c => c.Alignment = alignment);
        public void SetFillColor(string color) => UpdateCellFormat(c => c.Background = color);
        public void SetFormat(string format) => UpdateCellFormat(c => c.Format = format);

        private void UpdateCellFormat(Action<CellData> action)
        {
            if (_spreadsheet == null) return;
            var sheet = _spreadsheet.Sheets[_currentSheetIndex];
            var start = GetCoordinates(_selectedCell);
            var end = GetCoordinates(_selectionEndCell);
            for (int r = Math.Min(start.row, end.row); r <= Math.Max(start.row, end.row); r++)
            {
                for (int c = Math.Min(start.col, end.col); c <= Math.Max(start.col, end.col); c++)
                {
                    var cellRef = GetCellReference(c, r);
                    if (!sheet.Cells.TryGetValue(cellRef, out var cell)) { cell = new CellData(); sheet.Cells[cellRef] = cell; }
                    action(cell);
                }
            }
            InvalidateVisual();
        }
    }
}
