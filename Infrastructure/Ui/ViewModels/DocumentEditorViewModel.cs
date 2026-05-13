using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using MediatR;
using AvaloniaShareApp.Application.UseCases.Documents.Commands.SaveDocument;
using AvaloniaShareApp.Domain.Entities;
using System.Threading.Tasks;
using AvaloniaShareApp.Application.Ports;

namespace AvaloniaShareApp.Infrastructure.Ui.ViewModels
{
    public partial class DocumentEditorViewModel : ViewModelBase
    {
        private readonly IMediator? _mediator;

        public DocumentEditorViewModel(IMediator mediator)
        {
            _mediator = mediator;
            InitializeAutosave();
        }


        public async Task LoadDocument(string id)
        {
            if (_mediator == null) return;
            var doc = await _mediator.Send(new Application.UseCases.Documents.Queries.GetDocumentById.GetDocumentByIdQuery(id));
            if (doc != null)
            {
                DocumentId = doc.Id;
                DocumentTitle = doc.Title;
                _originalCreatedAt = doc.CreatedAt;
                // Give UI time to switch to Editor view and bind DataContext before firing the event
                await Task.Delay(100);
                DocumentLoaded?.Invoke(this, doc);
            }
        }

        private DateTime? _originalCreatedAt;

        public event EventHandler<Document>? DocumentLoaded;

        // Constructor sin parámetros para el diseñador (opcional)
        public DocumentEditorViewModel() { }
        
        [ObservableProperty]
        private string? _documentId;

        [ObservableProperty]
        private string _documentTitle = "Documento sin título";

        [ObservableProperty]
        private string _documentContent = "";

        // --- Propiedades de Estilo (Booleanos para los botones) ---
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FontWeight))]
        private bool _isBold;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FontStyle))]
        private bool _isItalic;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TextDecorations))]
        private bool _isUnderline;

        // --- Propiedades Calculadas (Para el TextBox) ---
        public FontWeight FontWeight => IsBold ? FontWeight.Bold : FontWeight.Normal;
        public FontStyle FontStyle => IsItalic ? FontStyle.Italic : FontStyle.Normal;
        public TextDecorationCollection? TextDecorations => IsUnderline ? Avalonia.Media.TextDecorations.Underline : null;

        [ObservableProperty]
        private string _selectedFont = "Calibri";

        [ObservableProperty]
        private double _selectedFontSize = 11;

        // CORRECCIÓN 1: Usar nombre completo para evitar conflicto con la propiedad generada
        [ObservableProperty]
        private Avalonia.Media.TextAlignment _textAlignment = Avalonia.Media.TextAlignment.Left;

        public List<string> AvailableFonts { get; } = new() { "Calibri", "Arial", "Times New Roman", "Verdana", "Georgia", "Consolas", "Segoe UI" };
        public List<double> AvailableFontSizes { get; } = new() { 8, 9, 10, 11, 12, 14, 16, 18, 24, 36, 48, 72 };

        // --- Comandos ---
        [RelayCommand]
        private void ToggleBold() => IsBold = !IsBold;

        [RelayCommand]
        private void ToggleItalic() => IsItalic = !IsItalic;

        [RelayCommand]
        private void ToggleUnderline() => IsUnderline = !IsUnderline;

        [RelayCommand]
        private void SetAlignment(string alignment)
        {
            // CORRECCIÓN 2: Parsear explícitamente usando el tipo completo
            if (System.Enum.TryParse<Avalonia.Media.TextAlignment>(alignment, true, out var result))
            {
                TextAlignment = result;
            }
        }

        // CORRECCIÓN 3: Asegurar que el campo sea IBrush explícito
        [ObservableProperty]
        private IBrush _textColor = Brushes.Black;

        [RelayCommand]
        private void SetTextColor(string colorHex)
        {
            try
            {
                TextColor = Brush.Parse(colorHex);
            }
            catch
            {
                TextColor = Brushes.Black;
            }
        }

        // --- Comandos de Archivo ---
        [RelayCommand]
        private void NewDocument() { }

        [RelayCommand]
        private void OpenDocument() { }

        public Func<Document>? GetDocumentState { get; set; }

        [RelayCommand]
        private async Task SaveDocument() 
        { 
            Document document;
            if (GetDocumentState != null)
            {
                document = GetDocumentState();
                
                // Si ya tenemos un ID, lo reutilizamos para no duplicar archivos
                if (!string.IsNullOrEmpty(DocumentId))
                {
                    document.Id = DocumentId;
                    if (_originalCreatedAt.HasValue)
                    {
                        document.CreatedAt = _originalCreatedAt.Value;
                    }
                }
                else
                {
                    // Si es la primera vez que se guarda, guardamos el nuevo ID generado
                    DocumentId = document.Id;
                    _originalCreatedAt = document.CreatedAt;
                }
                
                document.Title = DocumentTitle;
            }
            else
            {
                document = new Document { Title = DocumentTitle };
                if (!string.IsNullOrEmpty(DocumentId)) 
                {
                    document.Id = DocumentId;
                    if (_originalCreatedAt.HasValue)
                        document.CreatedAt = _originalCreatedAt.Value;
                }
                else 
                {
                    DocumentId = document.Id;
                    _originalCreatedAt = document.CreatedAt;
                }
            }

            if (_mediator != null)
            {
                await _mediator.Send(new SaveDocumentCommand(document));
                IsDirty = false;
            }
        }

        [RelayCommand]
        private void SaveDocumentAs() { }

        public event EventHandler? ExportPdfRequested;

        [RelayCommand]
        private void ExportPdf() 
        { 
            ExportPdfRequested?.Invoke(this, EventArgs.Empty);
        }

        // --- Comandos de Insertar ---
        public event EventHandler? InsertImageRequested;

        [RelayCommand]
        private void InsertImage()
        {
            InsertImageRequested?.Invoke(this, EventArgs.Empty);
        }

        [ObservableProperty]
        private bool _isDirty;

        private System.Timers.Timer? _autosaveTimer;

        private void InitializeAutosave()
        {
            _autosaveTimer = new System.Timers.Timer(30000); // 30 seconds
            _autosaveTimer.Elapsed += async (s, e) => 
            {
                if (IsDirty)
                {
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () => 
                    {
                        await SaveDocument();
                    });
                }
            };
            _autosaveTimer.Start();
        }

        [RelayCommand]
        private void InsertTable() { }

        [RelayCommand]
        private void InsertBlankPage() { }

        [RelayCommand]
        private void InsertPageBreak() { }

        public void MarkAsDirty()
        {
            IsDirty = true;
        }

        // Clean up timer when disposed if needed
        public void Dispose()
        {
            _autosaveTimer?.Stop();
            _autosaveTimer?.Dispose();
        }
    }
}