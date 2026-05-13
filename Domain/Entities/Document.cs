using System;
using System.Collections.Generic;

namespace AvaloniaShareApp.Domain.Entities
{
    public class Document
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = "Documento sin título";
        public List<Page> Pages { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public bool IsDeleted { get; set; }
    }

    public class Page
    {
        public List<ContentItem> Content { get; set; } = new();
        public List<ImageEntity> FloatingImages { get; set; } = new();
    }

    public class ContentItem
    {
        public string Type { get; set; } = "Paragraph"; // Paragraph, Table
        public Paragraph? Paragraph { get; set; }
        public TableEntity? Table { get; set; }
    }

    public class Paragraph
    {
        public List<TextRun> Runs { get; set; } = new();
        public string Alignment { get; set; } = "Left";
        public double LineSpacing { get; set; } = 1.2;
        public double ParagraphSpacing { get; set; } = 10;
        public double LeftIndent { get; set; }
        public double RightIndent { get; set; }
        public double TextIndent { get; set; }
    }

    public class TableEntity
    {
        public int Columns { get; set; }
        public int Rows { get; set; }
        public List<TableCell> Cells { get; set; } = new();
        public double MarginTop { get; set; } = 10;
        public double MarginBottom { get; set; } = 10;
    }

    public class TableCell
    {
        public int Row { get; set; }
        public int Column { get; set; }
        public string Text { get; set; } = ""; // Simplified for now, or use List<Paragraph>
        public string BackgroundColor { get; set; } = "Transparent";
    }

    public class TextRun
    {
        public string Text { get; set; } = "";
        public string FontWeight { get; set; } = "Normal";
        public string FontStyle { get; set; } = "Normal";
        public string FontFamily { get; set; } = "Calibri";
        public double FontSize { get; set; } = 11;
        public string ForegroundColor { get; set; } = "#000000";
        public bool IsUnderlined { get; set; }
    }

    public class ImageEntity
    {
        public string Path { get; set; } = "";
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Rotation { get; set; }
        public bool IsBackground { get; set; }
    }
}
