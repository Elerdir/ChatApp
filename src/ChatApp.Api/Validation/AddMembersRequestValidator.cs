using ChatApp.Application.Conversations;
using FluentValidation;

namespace ChatApp.Api.Validation;

public sealed class AddMembersRequestValidator : AbstractValidator<AddMembersRequest>
{
    public AddMembersRequestValidator()
    {
        RuleFor(x => x.MemberUserIds)
            .NotNull()
            .Must(ids => ids!.Length > 0)
            .WithMessage("Provide at least one member user id.");

        RuleForEach(x => x.MemberUserIds)
            .NotEmpty();
    }
}