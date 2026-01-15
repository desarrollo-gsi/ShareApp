using System;

namespace AvaloniaShareApp.Models
{
    public class DocumentItem
    {
        public string Title { get; set; } = string.Empty;
        public string Owner { get; set; } = "Cualquiera es el propietario";
        public DateTime LastOpened { get; set; }
        public string Icon { get; set; } = "📝"; // Placeholder icon
    }
}
