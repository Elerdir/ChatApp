using ChatApp.Application.Abstractions;
using ChatApp.Domain.Auth;
using ChatApp.Domain.Conversations;
using ChatApp.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace ChatApp.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationMember> ConversationMembers => Set<ConversationMember>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();
    public DbSet<DeviceSettings> DeviceSettings => Set<DeviceSettings>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // User
        b.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Username).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.Username).IsUnique();
            e.Property(x => x.DisplayName).HasMaxLength(128).IsRequired();
            e.Property(x => x.PasswordHash).IsRequired();
            e.Property(x => x.CreatedAt).IsRequired();
        });

        // Conversation
        b.Entity<Conversation>(e =>
        {
            e.ToTable("conversations");
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).IsRequired();
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.CreatedAt).IsRequired();

            e.HasMany(x => x.Messages)
             .WithOne()
             .HasForeignKey(m => m.ConversationId)
             .OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.LastMessageAt);
            e.Property(x => x.LastMessageId);
            e.HasIndex(x => x.LastMessageAt);
        });

        // ConversationMember (join)
        b.Entity<ConversationMember>(e =>
        {
            e.ToTable("conversation_members");
            e.HasKey(x => new { x.ConversationId, x.UserId });
            e.Property(x => x.Role).HasMaxLength(32).IsRequired();
            e.Property(x => x.JoinedAt).IsRequired();
            e.HasIndex(x => x.UserId);
            e.Property(x => x.LastReadAt);
            e.Property(x => x.LastReadMessageId);
            e.Property(x => x.UnreadCount).IsRequired().HasDefaultValue(0);

            e.HasIndex(x => new { x.UserId, x.ConversationId });
        });

        // Message
        b.Entity<Message>(e =>
        {
            e.ToTable("messages");
            e.HasKey(x => x.Id);
            e.Property(x => x.Body).HasMaxLength(4000).IsRequired();
            e.Property(x => x.CreatedAt).IsRequired();
            e.Property(x => x.ClientMessageId).IsRequired();

            e.HasIndex(x => new { x.ConversationId, x.CreatedAt });
            e.HasIndex(x => new { x.ConversationId, x.ClientMessageId }).IsUnique(); // idempotence per convo
        });
        
        b.Entity<UserProfile>(e =>
        {
            e.ToTable("user_profiles");
            e.HasKey(x => x.UserId);
            e.Property(x => x.DisplayName).HasMaxLength(128).IsRequired();
            e.Property(x => x.Bio).HasMaxLength(500);
            e.Property(x => x.CreatedAt).IsRequired();
            e.Property(x => x.UpdatedAt).IsRequired();
        });

        // Devices
        b.Entity<Device>(e =>
        {
            e.ToTable("devices");
            e.HasKey(x => x.Id);
            e.Property(x => x.Platform).HasMaxLength(16).IsRequired();
            e.Property(x => x.Os).HasMaxLength(16).IsRequired();
            e.Property(x => x.DeviceName).HasMaxLength(64);
            e.Property(x => x.AppVersion).HasMaxLength(32);
            e.Property(x => x.CreatedAt).IsRequired();
            e.Property(x => x.LastSeenAt).IsRequired();

            e.HasIndex(x => new { x.UserId, x.InstallationId }).IsUnique();
            e.HasIndex(x => x.UserId);
        });

        // RefreshTokens
        b.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_tokens");
            e.HasKey(x => x.Id);
            e.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
            e.Property(x => x.CreatedAt).IsRequired();
            e.Property(x => x.ExpiresAt).IsRequired();
            e.Property(x => x.RevokedAt);
            e.Property(x => x.ReplacedByTokenId);

            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.DeviceId);
        });

        // UserSettings (jsonb)
        b.Entity<UserSettings>(e =>
        {
            e.ToTable("user_settings");
            e.HasKey(x => x.UserId);
            e.Property(x => x.SettingsJson).HasColumnType("jsonb").IsRequired();
            e.Property(x => x.UpdatedAt).IsRequired();
        });

        // DeviceSettings (jsonb)
        b.Entity<DeviceSettings>(e =>
        {
            e.ToTable("device_settings");
            e.HasKey(x => x.DeviceId);
            e.Property(x => x.SettingsJson).HasColumnType("jsonb").IsRequired();
            e.Property(x => x.UpdatedAt).IsRequired();
        });
    }
}