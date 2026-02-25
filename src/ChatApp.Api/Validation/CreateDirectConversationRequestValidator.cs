using ChatApp.Application.Conversations;
using FluentValidation;

namespace ChatApp.Api.Validation;

public sealed class CreateDirectConversationRequestValidator : AbstractValidator<CreateDirectConversationRequest>
{
    public CreateDirectConversationRequestValidator()
    {
        RuleFor(x => x.OtherUserId)
            .NotEmpty();
    }
}