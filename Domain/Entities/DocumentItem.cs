using System;

namespace AvaloniaShareApp.Domain.Entities
{
    public class DocumentItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = "";
        public DateTime LastOpened { get; set; }
        public string Owner { get; set; } = "Yo";
        public string Icon { get; set; } = "📄";
    }
}
