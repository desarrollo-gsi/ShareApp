using MediatR;
using System.Threading;
using System.Threading.Tasks;
using AvaloniaShareApp.Application.Ports;

namespace AvaloniaShareApp.Application.UseCases.Documents.Commands.DeleteDocument
{
    public record DeleteDocumentCommand(string Id) : IRequest<bool>;

    public class DeleteDocumentCommandHandler : IRequestHandler<DeleteDocumentCommand, bool>
    {
        private readonly IDocumentRepository _repository;

        public DeleteDocumentCommandHandler(IDocumentRepository repository)
        {
            _repository = repository;
        }

        public async Task<bool> Handle(DeleteDocumentCommand request, CancellationToken cancellationToken)
        {
            await _repository.SoftDeleteAsync(request.Id);
            return true;
        }
    }
}
