using System.Threading;
using System.Threading.Tasks;
using AvaloniaShareApp.Application.Ports;
using FluentValidation;
using MediatR;

namespace AvaloniaShareApp.Application.UseCases.Trash.Commands.RestoreItem
{
    public record RestoreItemCommand(string Id, string ItemType) : IRequest;

    public class RestoreItemCommandValidator : AbstractValidator<RestoreItemCommand>
    {
        public RestoreItemCommandValidator()
        {
            RuleFor(x => x.Id).NotEmpty();
            RuleFor(x => x.ItemType).NotEmpty().Must(x => x == "Document" || x == "Spreadsheet");
        }
    }

    public class RestoreItemHandler : IRequestHandler<RestoreItemCommand>
    {
        private readonly IDocumentRepository _docRepo;
        private readonly ISpreadsheetRepository _sheetRepo;

        public RestoreItemHandler(IDocumentRepository docRepo, ISpreadsheetRepository sheetRepo)
        {
            _docRepo = docRepo;
            _sheetRepo = sheetRepo;
        }

        public async Task Handle(RestoreItemCommand request, CancellationToken cancellationToken)
        {
            if (request.ItemType == "Document") await _docRepo.RestoreAsync(request.Id);
            else if (request.ItemType == "Spreadsheet") await _sheetRepo.RestoreAsync(request.Id);
        }
    }
}
