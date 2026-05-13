using System.Collections.Generic;

namespace AvaloniaShareApp.Domain.Entities
{
    public class DocumentState
    {
        public List<PageState> Pages { get; set; } = new();
        public int CurrentPageIndex { get; set; }
        public int CurrentParagraphIndex { get; set; }
        public int CaretPosition { get; set; }
    }

    public class PageState
    {
        public List<ParagraphState> Paragraphs { get; set; } = new();
        public List<ImageState> Images { get; set; } = new();
    }

    public class ParagraphState
    {
        public List<RunState> Runs { get; set; } = new();
        public string Alignment { get; set; } = "Left";
        public double LineSpacing { get; set; }
        public double ParagraphSpacing { get; set; }
        public double TextIndent { get; set; }
        public double LeftIndent { get; set; }
        public double RightIndent { get; set; }
    }

    public class RunState
    {
        public string Text { get; set; } = "";
        public string FontWeight { get; set; } = "Normal";
        public string FontStyle { get; set; } = "Normal";
        public string FontFamily { get; set; } = "Calibri";
        public double FontSize { get; set; }
        public string Foreground { get; set; } = "Black";
        public bool HasUnderline { get; set; }
    }

    public class ImageState
    {
        public string FilePath { get; set; } = "";
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Rotation { get; set; }
        public bool IsBackground { get; set; }
    }
}
