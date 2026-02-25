using ChatApp.Application.Conversations;
using FluentValidation;

namespace ChatApp.Api.Validation;

public sealed class RenameConversationRequestValidator : AbstractValidator<RenameConversationRequest>
{
    public RenameConversationRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MinimumLength(1)
            .MaximumLength(200);
    }
}