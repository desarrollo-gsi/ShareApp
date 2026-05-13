using AvaloniaShareApp.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AvaloniaShareApp.Application.Ports
{
    public interface ISpreadsheetRepository
    {
        Task SaveAsync(Spreadsheet spreadsheet);
        Task<Spreadsheet?> GetByIdAsync(string id);
        Task<IEnumerable<Spreadsheet>> GetAllAsync();
        Task SoftDeleteAsync(string id);
        Task<IEnumerable<Spreadsheet>> GetDeletedAsync();
        Task RestoreAsync(string id);
        Task PermanentDeleteAsync(string id);
        Task EmptyTrashAsync();
    }
}
