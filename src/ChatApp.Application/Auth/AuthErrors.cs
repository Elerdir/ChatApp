namespace ChatApp.Application.Auth;

public enum AuthError
{
    None = 0,
    InvalidCredentials,
    UsernameTaken,
    InvalidInput,
    RefreshTokenInvalid
}

public sealed record AuthResult(AuthError Error, AuthResponse? Response)
{
    public static AuthResult Ok(AuthResponse r) => new(AuthError.None, r);
    public static AuthResult Fail(AuthError e) => new(e, null);
}