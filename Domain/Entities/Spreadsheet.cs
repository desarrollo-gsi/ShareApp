using System;
using System.Collections.Generic;

namespace AvaloniaShareApp.Domain.Entities
{
    public class Spreadsheet
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = "Hoja de cálculo sin título";
        public List<Sheet> Sheets { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public bool IsDeleted { get; set; } = false;
    }

    public class Sheet
    {
        public string Name { get; set; } = "Hoja 1";
        public Dictionary<string, CellData> Cells { get; set; } = new();
        public List<double> ColumnWidths { get; set; } = new();
        public List<double> RowHeights { get; set; } = new();
        
        // View state
        public string SelectedCell { get; set; } = "A1";
        public double ScrollX { get; set; } = 0;
        public double ScrollY { get; set; } = 0;
    }

    public class CellData
    {
        public string Value { get; set; } = "";
        public string Formula { get; set; } = "";
        
        // Style
        public string? Foreground { get; set; }
        public string? Background { get; set; }
        public bool IsBold { get; set; }
        public bool IsItalic { get; set; }
        public int FontSize { get; set; } = 11;
        public string Alignment { get; set; } = "Left"; // Left, Center, Right
        public string? Format { get; set; } // Currency, Percent, etc.
    }
}
