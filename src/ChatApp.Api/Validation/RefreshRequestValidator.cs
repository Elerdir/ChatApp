using ChatApp.Application.Auth;
using FluentValidation;

namespace ChatApp.Api.Validation;

public sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty()
            .MinimumLength(20); // JWT/secure token bývá delší
    }
}