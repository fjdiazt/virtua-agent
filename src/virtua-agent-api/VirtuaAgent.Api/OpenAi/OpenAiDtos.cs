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

    [JsonPropertyName("endpoint_id")]
    public string? EndpointId { get; init; }

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
    public ChatMessageContent Content { get; init; } = "";
}

[JsonConverter(typeof(ChatMessageContentJsonConverter))]
public sealed record ChatMessageContent
{
    public string? Text { get; init; } = "";
    public List<ChatMessageContentPart> Parts { get; init; } = [];

    [JsonIgnore]
    public bool IsParts => Text is null;

    public static ChatMessageContent FromText(string? text) => new() { Text = text ?? "" };

    public static ChatMessageContent FromParts(IEnumerable<ChatMessageContentPart> parts) =>
        new() { Text = null, Parts = parts.ToList() };

    public static implicit operator ChatMessageContent(string content) => FromText(content);

    public static implicit operator string(ChatMessageContent content) => content.AsText();

    public string AsText()
    {
        if (!IsParts)
        {
            return Text ?? "";
        }

        return string.Join(" ", Parts.Select(part => part.AsText()).Where(text => !string.IsNullOrWhiteSpace(text)));
    }

    public ChatMessageContent RedactMedia()
    {
        if (!IsParts)
        {
            return this;
        }

        return FromParts(Parts.Select(part => part.RedactMedia()));
    }
}

public sealed record ChatMessageContentPart
{
    public string Type { get; init; } = "";
    public string? Text { get; init; }

    [JsonPropertyName("image_url")]
    public ChatMessageImageUrl? ImageUrl { get; init; }

    public static ChatMessageContentPart FromText(string text) => new()
    {
        Type = "text",
        Text = text
    };

    public static ChatMessageContentPart FromImageUrl(string url, string? detail = null) => new()
    {
        Type = "image_url",
        ImageUrl = new ChatMessageImageUrl { Url = url, Detail = detail }
    };

    public string AsText() =>
        Type switch
        {
            "text" => Text ?? "",
            "image_url" => "[image_url]",
            "" => "",
            _ => $"[{Type}]"
        };

    public ChatMessageContentPart RedactMedia()
    {
        if (!string.Equals(Type, "image_url", StringComparison.OrdinalIgnoreCase) || ImageUrl is null)
        {
            return this;
        }

        return this with
        {
            ImageUrl = ImageUrl with { Url = "[image_url redacted]" }
        };
    }
}

public sealed record ChatMessageImageUrl
{
    public string Url { get; init; } = "";
    public string? Detail { get; init; }
}

public sealed class ChatMessageContentJsonConverter : JsonConverter<ChatMessageContent>
{
    public override ChatMessageContent Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return ChatMessageContent.FromText(reader.GetString());
        }

        if (reader.TokenType == JsonTokenType.Null)
        {
            return ChatMessageContent.FromText("");
        }

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var parts = JsonSerializer.Deserialize<List<ChatMessageContentPart>>(ref reader, options) ?? [];
            return ChatMessageContent.FromParts(parts);
        }

        throw new JsonException("Chat message content must be a string or content parts array.");
    }

    public override void Write(Utf8JsonWriter writer, ChatMessageContent value, JsonSerializerOptions options)
    {
        if (value.IsParts)
        {
            JsonSerializer.Serialize(writer, value.Parts, options);
            return;
        }

        writer.WriteStringValue(value.Text ?? "");
    }
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
