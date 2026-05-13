using System.Threading;
using System.Threading.Tasks;
using AvaloniaShareApp.Application.Ports;
using FluentValidation;
using MediatR;

namespace AvaloniaShareApp.Application.UseCases.Trash.Commands.PermanentDeleteItem
{
    public record PermanentDeleteItemCommand(string Id, string ItemType) : IRequest;

    public class PermanentDeleteItemCommandValidator : AbstractValidator<PermanentDeleteItemCommand>
    {
        public PermanentDeleteItemCommandValidator()
        {
            RuleFor(x => x.Id).NotEmpty();
            RuleFor(x => x.ItemType).NotEmpty().Must(x => x == "Document" || x == "Spreadsheet");
        }
    }

    public class PermanentDeleteItemHandler : IRequestHandler<PermanentDeleteItemCommand>
    {
        private readonly IDocumentRepository _docRepo;
        private readonly ISpreadsheetRepository _sheetRepo;

        public PermanentDeleteItemHandler(IDocumentRepository docRepo, ISpreadsheetRepository sheetRepo)
        {
            _docRepo = docRepo;
            _sheetRepo = sheetRepo;
        }

        public async Task Handle(PermanentDeleteItemCommand request, CancellationToken cancellationToken)
        {
            if (request.ItemType == "Document") await _docRepo.PermanentDeleteAsync(request.Id);
            else if (request.ItemType == "Spreadsheet") await _sheetRepo.PermanentDeleteAsync(request.Id);
        }
    }
}
