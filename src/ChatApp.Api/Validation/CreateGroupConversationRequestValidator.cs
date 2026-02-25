using ChatApp.Application.Conversations;
using FluentValidation;

namespace ChatApp.Api.Validation;

public sealed class CreateGroupConversationRequestValidator : AbstractValidator<CreateGroupConversationRequest>
{
    public CreateGroupConversationRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MinimumLength(1)
            .MaximumLength(200);

        RuleFor(x => x.MemberUserIds)
            .NotNull()
            .Must(ids => ids!.Where(x => x != Guid.Empty).Distinct().Count() >= 2)
            .WithMessage("Provide at least 2 additional members (creator makes it 3+).");
    }
}