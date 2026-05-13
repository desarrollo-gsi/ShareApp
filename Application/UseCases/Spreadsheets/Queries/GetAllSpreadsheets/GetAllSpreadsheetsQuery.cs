using MediatR;
using System.Collections.Generic;
using AvaloniaShareApp.Domain.Entities;
using System.Threading;
using System.Threading.Tasks;
using AvaloniaShareApp.Application.Ports;

namespace AvaloniaShareApp.Application.UseCases.Spreadsheets.Queries.GetAllSpreadsheets
{
    public record GetAllSpreadsheetsQuery : IRequest<IEnumerable<Spreadsheet>>;

    public class GetAllSpreadsheetsQueryHandler : IRequestHandler<GetAllSpreadsheetsQuery, IEnumerable<Spreadsheet>>
    {
        private readonly ISpreadsheetRepository _repository;

        public GetAllSpreadsheetsQueryHandler(ISpreadsheetRepository repository)
        {
            _repository = repository;
        }

        public async Task<IEnumerable<Spreadsheet>> Handle(GetAllSpreadsheetsQuery request, CancellationToken cancellationToken)
        {
            return await _repository.GetAllAsync();
        }
    }
}
