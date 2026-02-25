using ChatApp.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChatApp.Infrastructure.Persistence.Configurations;

public sealed class UserSettingsDeviceConfig : IEntityTypeConfiguration<UserSettingsDevice>
{
    public void Configure(EntityTypeBuilder<UserSettingsDevice> builder)
    {
        builder.ToTable("user_settings_device");

        builder.HasKey(x => new { x.UserId, x.DeviceId });

        builder.Property(x => x.SettingsJson)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(x => x.UpdatedAt)
            .IsRequired();

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.DeviceId);
    }
}