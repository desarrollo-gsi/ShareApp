using MediatR;
using System.Threading;
using System.Threading.Tasks;
using AvaloniaShareApp.Application.Ports;
using AvaloniaShareApp.Domain.Entities;

namespace AvaloniaShareApp.Application.UseCases.Spreadsheets.Commands.SaveSpreadsheet
{
    public record SaveSpreadsheetCommand(Spreadsheet Spreadsheet) : IRequest<bool>;

    public class SaveSpreadsheetCommandHandler : IRequestHandler<SaveSpreadsheetCommand, bool>
    {
        private readonly ISpreadsheetRepository _repository;

        public SaveSpreadsheetCommandHandler(ISpreadsheetRepository repository)
        {
            _repository = repository;
        }

        public async Task<bool> Handle(SaveSpreadsheetCommand request, CancellationToken cancellationToken)
        {
            await _repository.SaveAsync(request.Spreadsheet);
            return true;
        }
    }
}
