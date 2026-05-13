using AvaloniaShareApp.Application.Ports;
using AvaloniaShareApp.Domain.Entities;
using System;
using System.Text.Json;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace AvaloniaShareApp.Infrastructure.Persistence
{
    public class FileDocumentRepository : IDocumentRepository
    {
        private readonly string _storagePath = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "ShareApp", "Documents");

        public FileDocumentRepository()
        {
            if (!Directory.Exists(_storagePath))
                Directory.CreateDirectory(_storagePath);
        }

        public async Task SaveAsync(Document document)
        {
            document.UpdatedAt = DateTime.Now;
            var fileName = $"{document.Id}.json";
            var path = Path.Combine(_storagePath, fileName);
            var json = JsonSerializer.Serialize(document);
            await File.WriteAllTextAsync(path, json);
        }

        public async Task<Document?> GetByIdAsync(string id)
        {
            var path = Path.Combine(_storagePath, $"{id}.json");
            if (!File.Exists(path)) return null;
            
            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<Document>(json);
        }
        
        public async Task<IEnumerable<Document>> GetAllAsync() 
        {
            var docs = await LoadAllInternal();
            return docs.Where(d => !d.IsDeleted);
        }

        public async Task<IEnumerable<Document>> GetDeletedAsync()
        {
            var docs = await LoadAllInternal();
            return docs.Where(d => d.IsDeleted);
        }

        public async Task SoftDeleteAsync(string id)
        {
            var doc = await GetByIdAsync(id);
            if (doc != null)
            {
                doc.IsDeleted = true;
                await SaveAsync(doc);
            }
        }

        public async Task RestoreAsync(string id)
        {
            var doc = await GetByIdAsync(id);
            if (doc != null)
            {
                doc.IsDeleted = false;
                await SaveAsync(doc);
            }
        }

        public Task PermanentDeleteAsync(string id)
        {
            var path = Path.Combine(_storagePath, $"{id}.json");
            if (File.Exists(path)) File.Delete(path);
            return Task.CompletedTask;
        }

        public Task EmptyTrashAsync()
        {
            var files = Directory.GetFiles(_storagePath, "*.json");
            foreach (var file in files)
            {
                var json = File.ReadAllText(file);
                var doc = JsonSerializer.Deserialize<Document>(json);
                if (doc?.IsDeleted == true)
                {
                    File.Delete(file);
                }
            }
            return Task.CompletedTask;
        }

        private async Task<List<Document>> LoadAllInternal()
        {
            var list = new List<Document>();
            if (!Directory.Exists(_storagePath)) return list;

            var files = Directory.GetFiles(_storagePath, "*.json");
            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var doc = JsonSerializer.Deserialize<Document>(json);
                    if (doc != null) list.Add(doc);
                }
                catch { /* Ignore corrupted files */ }
            }
            return list;
        }
    }
}
