using ChatApp.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChatApp.Infrastructure.Persistence.Configurations;

public sealed class UserSettingsGlobalConfig : IEntityTypeConfiguration<UserSettingsGlobal>
{
    public void Configure(EntityTypeBuilder<UserSettingsGlobal> builder)
    {
        builder.ToTable("user_settings_global");

        builder.HasKey(x => x.UserId);

        builder.Property(x => x.SettingsJson)
            .IsRequired()
            .HasColumnType("jsonb"); // PostgreSQL JSONB

        builder.Property(x => x.UpdatedAt)
            .IsRequired();

        builder.HasOne(x => x.User)
            .WithOne()
            .HasForeignKey<UserSettingsGlobal>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}