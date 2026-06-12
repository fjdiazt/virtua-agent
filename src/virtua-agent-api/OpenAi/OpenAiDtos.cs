using System.Text.Json;
using System.Text.Json.Serialization;
using VirtuaAgent.PipelineModels;

namespace VirtuaAgent.OpenAi;

public static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

public sealed record ChatCompletionRequest
{
    public string? Model { get; init; }
    public List<ChatMessageDto> Messages { get; init; } = [];
    public double? Temperature { get; init; }

    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; init; }

    public bool? Stream { get; init; }
    public OrchestrationRequestDto? Orchestration { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtraFields { get; init; }
}

public sealed record ChatMessageDto
{
    public string Role { get; init; } = "";
    public string Content { get; init; } = "";
}

public sealed record ChatCompletionResponse
{
    public string Id { get; init; } = "";
    public string Object { get; init; } = "chat.completion";
    public long Created { get; init; }
    public string Model { get; init; } = "";
    public List<ChatCompletionChoiceDto> Choices { get; init; } = [];
    public UsageDto? Usage { get; init; }

    [JsonPropertyName("virtua_agent")]
    public VirtuaAgentResponseDto? VirtuaAgent { get; init; }
}

public sealed record ChatCompletionChoiceDto
{
    public int Index { get; init; }
    public ChatMessageDto Message { get; init; } = new();

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; init; }
}

public sealed record UsageDto
{
    [JsonPropertyName("prompt_tokens")]
    public int? PromptTokens { get; init; }

    [JsonPropertyName("completion_tokens")]
    public int? CompletionTokens { get; init; }

    [JsonPropertyName("total_tokens")]
    public int? TotalTokens { get; init; }
}

public sealed record ModelListResponse
{
    public string Object { get; init; } = "list";
    public List<ModelDto> Data { get; init; } = [];
}

public sealed record ModelDto
{
    public string Id { get; init; } = "";
    public string Object { get; init; } = "model";
    public long? Created { get; init; }

    [JsonPropertyName("owned_by")]
    public string? OwnedBy { get; init; }
}
