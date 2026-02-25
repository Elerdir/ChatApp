namespace ChatApp.Application.Common;

public interface IEventPublisher
{
    Task PublishAsync<T>(T @event, CancellationToken ct = default) where T : class;
    
    Task PublishToUsersAsync<T>(IReadOnlyCollection<Guid> userIds, string eventName, T payload, CancellationToken ct = default)
        where T : class;

    Task PublishToUserAsync<T>(Guid userId, string eventName, T payload, CancellationToken ct = default)
        where T : class;
}