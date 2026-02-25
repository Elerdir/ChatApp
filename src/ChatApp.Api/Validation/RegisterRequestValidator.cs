using ChatApp.Application.Auth;
using FluentValidation;

namespace ChatApp.Api.Validation;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .MinimumLength(3)
            .MaximumLength(64);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(128);

        // pokud máš email:
        // RuleFor(x => x.Email)
        //     .NotEmpty()
        //     .EmailAddress();
    }
}