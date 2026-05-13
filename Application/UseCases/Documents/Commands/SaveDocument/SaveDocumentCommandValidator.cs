using FluentValidation;

namespace AvaloniaShareApp.Application.UseCases.Documents.Commands.SaveDocument
{
    public class SaveDocumentCommandValidator : AbstractValidator<SaveDocumentCommand>
    {
        public SaveDocumentCommandValidator()
        {
            RuleFor(v => v.Document)
                .NotNull()
                .WithMessage("El documento no puede ser nulo.");

            RuleFor(v => v.Document.Title)
                .NotEmpty()
                .WithMessage("El título del documento es obligatorio.")
                .MaximumLength(100)
                .WithMessage("El título no puede exceder los 100 caracteres.");
        }
    }
}
