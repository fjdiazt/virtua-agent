using System.Text.Json.Serialization;

namespace VirtuaAgent.ChatSessions;

public sealed record ChatSessionMessageDto
{
    public string Id { get; init; } = "";
    public string Role { get; init; } = "";
    public string Content { get; init; } = "";
    public Dictionary<string, string>? Reasoning { get; init; }
    public Dictionary<string, object?>? Metadata { get; init; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed record SaveChatSessionMessageRequest
{
    public string Role { get; init; } = "";
    public string Content { get; init; } = "";
    public Dictionary<string, string>? Reasoning { get; init; }
    public Dictionary<string, object?>? Metadata { get; init; }
}
