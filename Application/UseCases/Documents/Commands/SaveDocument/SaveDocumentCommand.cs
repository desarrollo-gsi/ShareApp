using MediatR;
using AvaloniaShareApp.Domain.Entities;

namespace AvaloniaShareApp.Application.UseCases.Documents.Commands.SaveDocument
{
    public record SaveDocumentCommand(Document Document) : IRequest<bool>;
}
