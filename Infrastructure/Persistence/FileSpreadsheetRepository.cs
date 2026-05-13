using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AvaloniaShareApp.Application.Ports;
using AvaloniaShareApp.Domain.Entities;

namespace AvaloniaShareApp.Infrastructure.Persistence
{
    public class FileSpreadsheetRepository : ISpreadsheetRepository
    {
        private readonly string _storagePath;

        public FileSpreadsheetRepository()
        {
            _storagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Storage", "Spreadsheets");
            if (!Directory.Exists(_storagePath))
            {
                Directory.CreateDirectory(_storagePath);
            }
        }

        public async Task SaveAsync(Spreadsheet spreadsheet)
        {
            spreadsheet.UpdatedAt = DateTime.Now;
            var filePath = Path.Combine(_storagePath, $"{spreadsheet.Id}.json");
            var json = JsonSerializer.Serialize(spreadsheet, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);
        }

        public async Task<Spreadsheet?> GetByIdAsync(string id)
        {
            var filePath = Path.Combine(_storagePath, $"{id}.json");
            if (!File.Exists(filePath)) return null;

            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<Spreadsheet>(json);
        }

        public async Task<IEnumerable<Spreadsheet>> GetAllAsync()
        {
            var files = Directory.GetFiles(_storagePath, "*.json");
            var spreadsheets = new List<Spreadsheet>();

            foreach (var file in files)
            {
                var json = await File.ReadAllTextAsync(file);
                var spreadsheet = JsonSerializer.Deserialize<Spreadsheet>(json);
                if (spreadsheet != null && !spreadsheet.IsDeleted)
                {
                    spreadsheets.Add(spreadsheet);
                }
            }

            return spreadsheets.OrderByDescending(s => s.UpdatedAt);
        }

        public async Task SoftDeleteAsync(string id)
        {
            var spreadsheet = await GetByIdAsync(id);
            if (spreadsheet != null)
            {
                spreadsheet.IsDeleted = true;
                await SaveAsync(spreadsheet);
            }
        }

        public async Task<IEnumerable<Spreadsheet>> GetDeletedAsync()
        {
            var files = Directory.GetFiles(_storagePath, "*.json");
            var spreadsheets = new List<Spreadsheet>();

            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var spreadsheet = JsonSerializer.Deserialize<Spreadsheet>(json);
                    if (spreadsheet != null && spreadsheet.IsDeleted)
                    {
                        spreadsheets.Add(spreadsheet);
                    }
                }
                catch { /* Ignore corrupted files */ }
            }

            return spreadsheets.OrderByDescending(s => s.UpdatedAt);
        }

        public async Task RestoreAsync(string id)
        {
            var spreadsheet = await GetByIdAsync(id);
            if (spreadsheet != null)
            {
                spreadsheet.IsDeleted = false;
                await SaveAsync(spreadsheet);
            }
        }

        public Task PermanentDeleteAsync(string id)
        {
            var filePath = Path.Combine(_storagePath, $"{id}.json");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            return Task.CompletedTask;
        }

        public Task EmptyTrashAsync()
        {
            var files = Directory.GetFiles(_storagePath, "*.json");
            foreach (var file in files)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var spreadsheet = JsonSerializer.Deserialize<Spreadsheet>(json);
                    if (spreadsheet?.IsDeleted == true)
                    {
                        File.Delete(file);
                    }
                }
                catch { /* Ignore */ }
            }
            return Task.CompletedTask;
        }
    }
}
