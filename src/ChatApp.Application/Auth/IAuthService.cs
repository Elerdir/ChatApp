using ChatApp.Api.Common;

namespace ChatApp.Application.Auth;

public interface IAuthService
{
    Task<Result<AuthResponse>> RegisterAsync(RegisterRequest req, CancellationToken ct = default);
    Task<Result<AuthResponse>> LoginAsync(LoginRequest req, CancellationToken ct = default);
    Task<Result<AuthResponse>> RefreshAsync(RefreshRequest req, CancellationToken ct = default);
    Task<Result> LogoutAsync(LogoutRequest req, CancellationToken ct = default);
    Task<Result> LogoutAllAsync(Guid userId, CancellationToken ct = default);
}