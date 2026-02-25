using ChatApp.Application.Users;
using FluentValidation;

namespace ChatApp.Api.Validation;

public sealed class UpdateUserSettingsRequestValidator : AbstractValidator<UpdateUserSettingsRequest>
{
    public UpdateUserSettingsRequestValidator()
    {
        RuleFor(x => x.Scope)
            .NotEmpty()
            .Must(s => s is "global" or "device")
            .WithMessage("Scope must be 'global' or 'device'.");

        RuleFor(x => x.Settings)
            .NotNull()
            .Must(d => d!.Count <= 200)
            .WithMessage("Too many settings keys (max 200).");

        RuleForEach(x => x.Settings)
            .Must(kv => kv.Key.Length is > 0 and <= 64)
            .WithMessage("Setting key must be 1-64 chars.");
    }
}