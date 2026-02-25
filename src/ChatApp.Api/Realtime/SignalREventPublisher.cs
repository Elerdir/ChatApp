using ChatApp.Application.Common;
using Microsoft.AspNetCore.SignalR;

namespace ChatApp.Api.Realtime;

public sealed class SignalREventPublisher : IEventPublisher
{
    private readonly IHubContext<ChatHub> _hub;

    public SignalREventPublisher(IHubContext<ChatHub> hub) => _hub = hub;

    public Task PublishAsync<T>(T @event, CancellationToken ct = default) where T : class
        => Task.CompletedTask; // nepoužíváme bez cílení

    public Task PublishToUsersAsync<T>(IReadOnlyCollection<Guid> userIds, string eventName, T payload, CancellationToken ct = default)
        where T : class
    {
        var ids = userIds.Select(x => x.ToString()).ToList();
        return _hub.Clients.Users(ids).SendAsync(eventName, payload, ct);
    }

    public Task PublishToUserAsync<T>(Guid userId, string eventName, T payload, CancellationToken ct = default)
        where T : class
    {
        return _hub.Clients.User(userId.ToString()).SendAsync(eventName, payload, ct);
    }
}