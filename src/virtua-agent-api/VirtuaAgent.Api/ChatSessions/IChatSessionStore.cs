namespace VirtuaAgent.ChatSessions;

public interface IChatSessionStore
{
    Task<IReadOnlyList<ChatSessionMessageDto>> ListCurrentMessagesAsync(CancellationToken cancellationToken = default);
    Task<ChatSessionMessageDto> AppendCurrentMessageAsync(SaveChatSessionMessageRequest request, CancellationToken cancellationToken = default);
    Task<int> ClearCurrentMessagesAsync(CancellationToken cancellationToken = default);
}
