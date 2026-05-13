using System.Threading;
using System.Threading.Tasks;
using AvaloniaShareApp.Application.Ports;
using MediatR;

namespace AvaloniaShareApp.Application.UseCases.Trash.Commands.EmptyTrash
{
    public record EmptyTrashCommand : IRequest;

    public class EmptyTrashHandler : IRequestHandler<EmptyTrashCommand>
    {
        private readonly IDocumentRepository _docRepo;
        private readonly ISpreadsheetRepository _sheetRepo;

        public EmptyTrashHandler(IDocumentRepository docRepo, ISpreadsheetRepository sheetRepo)
        {
            _docRepo = docRepo;
            _sheetRepo = sheetRepo;
        }

        public async Task Handle(EmptyTrashCommand request, CancellationToken cancellationToken)
        {
            await _docRepo.EmptyTrashAsync();
            await _sheetRepo.EmptyTrashAsync();
        }
    }
}
