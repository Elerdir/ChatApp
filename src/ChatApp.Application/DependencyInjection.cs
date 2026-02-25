using ChatApp.Application.Auth;
using ChatApp.Application.Conversations;
using ChatApp.Application.Devices;
using ChatApp.Application.Inbox;
using ChatApp.Application.Messages;
using ChatApp.Application.Users;
using Microsoft.Extensions.DependencyInjection;

namespace ChatApp.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IDeviceService, DeviceService>();
        services.AddScoped<IConversationService, ConversationService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<IMessageService, MessageService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IConversationMembershipService, ConversationMembershipService>();
        services.AddScoped<IInboxService, InboxService>();
        
        return services;
    }
}