using System.Security.Claims;

namespace ChatApp.Api.Common;

public static class ClaimsExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out var id)
            ? id
            : throw new InvalidOperationException("User id claim not found.");
    }

    public static Guid GetDeviceId(this ClaimsPrincipal user)
    {
        var v = user.FindFirstValue("deviceId");
        return Guid.TryParse(v, out var id)
            ? id
            : throw new InvalidOperationException("Device id claim not found.");
    }
}