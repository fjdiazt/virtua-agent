using VirtuaAgent.OpenAi;

namespace VirtuaAgent.ChatSessions;

public static class ChatSessionsEndpoint
{
    public static async Task<IResult> ListCurrentMessagesAsync(IChatSessionStore store, CancellationToken cancellationToken)
    {
        var messages = await store.ListCurrentMessagesAsync(cancellationToken);
        return Results.Json(messages, JsonOptions.Default);
    }

    public static async Task<IResult> AppendCurrentMessageAsync(
        SaveChatSessionMessageRequest request,
        IChatSessionStore store,
        CancellationToken cancellationToken)
    {
        var role = request.Role.Trim().ToLowerInvariant();
        if (role is not ("user" or "assistant"))
        {
            return BadRequest("Chat message role must be user or assistant.", "role", "invalid_chat_role");
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest("Chat message content is required.", "content", "chat_content_required");
        }

        var saved = await store.AppendCurrentMessageAsync(request with { Role = role }, cancellationToken);
        return Results.Json(saved, JsonOptions.Default);
    }

    public static async Task<IResult> ClearCurrentMessagesAsync(IChatSessionStore store, CancellationToken cancellationToken)
    {
        var deleted = await store.ClearCurrentMessagesAsync(cancellationToken);
        return Results.Json(new ClearChatMessagesResponse(deleted), JsonOptions.Default);
    }

    private static IResult BadRequest(string message, string param, string code) =>
        Results.BadRequest(new OpenAiErrorResponse(new OpenAiError
        {
            Message = message,
            Type = "invalid_request_error",
            Param = param,
            Code = code
        }));

    private sealed record ClearChatMessagesResponse(int Deleted);
}
