using MediatR;
using AvaloniaShareApp.Application.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace AvaloniaShareApp.Application.UseCases.Documents.Commands.SaveDocument
{
    public class SaveDocumentHandler : IRequestHandler<SaveDocumentCommand, bool>
    {
        private readonly IDocumentRepository _repository;

        public SaveDocumentHandler(IDocumentRepository repository)
        {
            _repository = repository;
        }

        public async Task<bool> Handle(SaveDocumentCommand request, CancellationToken cancellationToken)
        {
            // Lógica de negocio si fuera necesaria antes de guardar
            await _repository.SaveAsync(request.Document);
            return true;
        }
    }
}
