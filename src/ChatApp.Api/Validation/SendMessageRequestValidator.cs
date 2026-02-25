using ChatApp.Application.Messages;
using FluentValidation;

namespace ChatApp.Api.Validation;

public sealed class SendMessageRequestValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageRequestValidator()
    {
        RuleFor(x => x.Body).NotEmpty().MaximumLength(8000);
        RuleFor(x => x.ClientMessageId).NotEmpty();
    }
}