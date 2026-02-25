using ChatApp.Api.Common;

namespace ChatApp.Application.Messages;

public interface IMessageService
{
    Task<Result<IReadOnlyList<MessageDto>>> GetHistoryAsync(
        Guid userId,
        Guid conversationId,
        DateTimeOffset? before,
        int limit,
        CancellationToken ct = default);

    Task<Result<MessageDto>> SendAsync(
        Guid userId,
        Guid deviceId,
        Guid conversationId,
        SendMessageRequest req,
        CancellationToken ct = default);

    Task<Result> MarkReadAsync(
        Guid userId,
        Guid conversationId,
        MarkReadRequest req,
        CancellationToken ct = default);
}