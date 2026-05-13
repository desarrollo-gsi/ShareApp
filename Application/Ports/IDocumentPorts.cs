using AvaloniaShareApp.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AvaloniaShareApp.Application.Ports
{
    public interface IDocumentRepository
    {
        Task SaveAsync(Document document);
        Task<Document?> GetByIdAsync(string id);
        Task<IEnumerable<Document>> GetAllAsync();
        Task<IEnumerable<Document>> GetDeletedAsync();
        Task SoftDeleteAsync(string id);
        Task RestoreAsync(string id);
        Task PermanentDeleteAsync(string id);
        Task EmptyTrashAsync();
    }

    public interface IPdfExporter
    {
        Task ExportAsync(Document document, string filePath);
    }
}
