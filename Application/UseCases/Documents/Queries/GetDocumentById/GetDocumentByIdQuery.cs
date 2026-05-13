using MediatR;
using AvaloniaShareApp.Domain.Entities;
using System.Threading;
using System.Threading.Tasks;
using AvaloniaShareApp.Application.Ports;

namespace AvaloniaShareApp.Application.UseCases.Documents.Queries.GetDocumentById
{
    public record GetDocumentByIdQuery(string Id) : IRequest<Document?>;

    public class GetDocumentByIdQueryHandler : IRequestHandler<GetDocumentByIdQuery, Document?>
    {
        private readonly IDocumentRepository _repository;

        public GetDocumentByIdQueryHandler(IDocumentRepository repository)
        {
            _repository = repository;
        }

        public async Task<Document?> Handle(GetDocumentByIdQuery request, CancellationToken cancellationToken)
        {
            return await _repository.GetByIdAsync(request.Id);
        }
    }
}
