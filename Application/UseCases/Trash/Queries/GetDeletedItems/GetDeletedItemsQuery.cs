using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AvaloniaShareApp.Application.Ports;
using MediatR;

namespace AvaloniaShareApp.Application.UseCases.Trash.Queries.GetDeletedItems
{
    public class DeletedItemDto
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public DateTime UpdatedAt { get; set; }
        public string Icon { get; set; } = "";
        public string ItemType { get; set; } = "";
    }

    public record GetDeletedItemsQuery : IRequest<IEnumerable<DeletedItemDto>>;

    public class GetDeletedItemsHandler : IRequestHandler<GetDeletedItemsQuery, IEnumerable<DeletedItemDto>>
    {
        private readonly IDocumentRepository _docRepo;
        private readonly ISpreadsheetRepository _sheetRepo;

        public GetDeletedItemsHandler(IDocumentRepository docRepo, ISpreadsheetRepository sheetRepo)
        {
            _docRepo = docRepo;
            _sheetRepo = sheetRepo;
        }

        public async Task<IEnumerable<DeletedItemDto>> Handle(GetDeletedItemsQuery request, CancellationToken cancellationToken)
        {
            var docs = await _docRepo.GetDeletedAsync();
            var sheets = await _sheetRepo.GetDeletedAsync();

            return docs.Select(d => new DeletedItemDto { Id = d.Id, Title = d.Title, UpdatedAt = d.UpdatedAt, Icon = "📄", ItemType = "Document" })
                .Concat(sheets.Select(s => new DeletedItemDto { Id = s.Id, Title = s.Title, UpdatedAt = s.UpdatedAt, Icon = "📗", ItemType = "Spreadsheet" }))
                .OrderByDescending(i => i.UpdatedAt);
        }
    }
}
