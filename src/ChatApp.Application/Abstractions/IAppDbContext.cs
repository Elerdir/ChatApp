using ChatApp.Domain.Auth;
using ChatApp.Domain.Conversations;
using ChatApp.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace ChatApp.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<UserProfile> UserProfiles { get; }
    DbSet<Device> Devices { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<UserSettings> UserSettings { get; }
    DbSet<DeviceSettings> DeviceSettings { get; }
    DbSet<Conversation> Conversations { get; }
    DbSet<ConversationMember> ConversationMembers { get; }
    DbSet<Message> Messages { get; }
    DbSet<UserSettingsGlobal> UserSettingsGlobals { get; }
    DbSet<UserSettingsDevice> UserSettingsDevices { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}