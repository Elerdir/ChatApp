namespace ChatApp.Application.Common;

public enum ErrorType
{
    Validation,
    Conflict,
    Unauthorized,
    Forbidden,
    NotFound,
    RateLimited,
    Unexpected
}

public sealed record AppError(
    string Code,               // např. "auth.username_taken"
    string Message,            // lidsky čitelné
    ErrorType Type,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null);