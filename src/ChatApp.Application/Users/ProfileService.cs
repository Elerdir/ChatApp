using ChatApp.Api.Common;
using ChatApp.Application.Abstractions;
using ChatApp.Application.Common;
using ChatApp.Domain.Users;

namespace ChatApp.Application.Users;

public sealed class ProfileService : IProfileService
{
    private readonly IAppDbContext _db;

    public ProfileService(IAppDbContext db) => _db = db;

    public async Task<Result<MeDto>> GetMeAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users.FindAsync([userId], ct);
        if (user is null)
        {
            return Result<MeDto>.Fail(new AppError(
                "auth.unauthorized",
                "Unauthorized.",
                ErrorType.Unauthorized));
        }

        var profile = await _db.UserProfiles.FindAsync([userId], ct);
        if (profile is null)
        {
            // create default profile lazily (safe)
            profile = new UserProfile(userId, user.Username);
            _db.UserProfiles.Add(profile);
            await _db.SaveChangesAsync(ct);
        }

        return Result<MeDto>.Ok(new MeDto(
            UserId: user.Id,
            Username: user.Username,
            DisplayName: profile.DisplayName,
            Bio: profile.Bio,
            AvatarFileId: profile.AvatarFileId));
    }

    public async Task<Result> UpdateProfileAsync(Guid userId, PatchProfileRequest req, CancellationToken ct = default)
    {
        var dn = (req.DisplayName ?? "").Trim();
        if (dn.Length is < 1 or > 128)
        {
            return Result.Fail(new AppError(
                Code: "profile.invalid_display_name",
                Message: "Invalid display name.",
                Type: ErrorType.Validation,
                FieldErrors: new Dictionary<string, string[]>
                {
                    ["displayName"] = new[] { "DisplayName must be 1-128 characters." }
                }));
        }

        if (req.Bio is { Length: > 500 })
        {
            return Result.Fail(new AppError(
                Code: "profile.invalid_bio",
                Message: "Invalid bio.",
                Type: ErrorType.Validation,
                FieldErrors: new Dictionary<string, string[]>
                {
                    ["bio"] = new[] { "Bio max length is 500." }
                }));
        }

        var profile = await _db.UserProfiles.FindAsync([userId], ct);
        if (profile is null)
        {
            profile = new UserProfile(userId, dn);
            _db.UserProfiles.Add(profile);
        }
        else
        {
            profile.Update(dn, req.Bio);
        }

        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }
}