using MediatR;
using AvaloniaShareApp.Domain.Entities;
using System.Threading;
using System.Threading.Tasks;
using AvaloniaShareApp.Application.Ports;

namespace AvaloniaShareApp.Application.UseCases.Spreadsheets.Queries.GetSpreadsheetById
{
    public record GetSpreadsheetByIdQuery(string Id) : IRequest<Spreadsheet?>;

    public class GetSpreadsheetByIdQueryHandler : IRequestHandler<GetSpreadsheetByIdQuery, Spreadsheet?>
    {
        private readonly ISpreadsheetRepository _repository;

        public GetSpreadsheetByIdQueryHandler(ISpreadsheetRepository repository)
        {
            _repository = repository;
        }

        public async Task<Spreadsheet?> Handle(GetSpreadsheetByIdQuery request, CancellationToken cancellationToken)
        {
            return await _repository.GetByIdAsync(request.Id);
        }
    }
}
