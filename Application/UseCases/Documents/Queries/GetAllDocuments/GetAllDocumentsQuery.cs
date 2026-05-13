using MediatR;
using System.Collections.Generic;
using AvaloniaShareApp.Domain.Entities;
using System.Threading;
using System.Threading.Tasks;
using AvaloniaShareApp.Application.Ports;
using System.Linq;

namespace AvaloniaShareApp.Application.UseCases.Documents.Queries.GetAllDocuments
{
    public record GetAllDocumentsQuery(string? SearchQuery = null) : IRequest<IEnumerable<Document>>;

    public class GetAllDocumentsQueryHandler : IRequestHandler<GetAllDocumentsQuery, IEnumerable<Document>>
    {
        private readonly IDocumentRepository _repository;

        public GetAllDocumentsQueryHandler(IDocumentRepository repository)
        {
            _repository = repository;
        }

        public async Task<IEnumerable<Document>> Handle(GetAllDocumentsQuery request, CancellationToken cancellationToken)
        {
            var documents = await _repository.GetAllAsync();

            if (!string.IsNullOrWhiteSpace(request.SearchQuery))
            {
                documents = documents.Where(d => d.Title.Contains(request.SearchQuery, System.StringComparison.OrdinalIgnoreCase)).ToList();
            }

            return documents;
        }
    }
}
