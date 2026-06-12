using System.Text.Json;

namespace VirtuaAgent.OpenAi;

public sealed record OpenAiStreamDelta(
    string? Content,
    string? Reasoning,
    string? FinishReason,
    string? Id,
    long? Created,
    string? Model);

public static class OpenAiStreamData
{
    public static OpenAiStreamDelta? ParseDelta(string data)
    {
        if (string.Equals(data.Trim(), "[DONE]", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        using var document = JsonDocument.Parse(data);
        var root = document.RootElement;
        var id = ReadString(root, "id");
        var model = ReadString(root, "model");
        long? created = root.TryGetProperty("created", out var createdElement) && createdElement.ValueKind == JsonValueKind.Number
            ? createdElement.GetInt64()
            : null;

        if (!root.TryGetProperty("choices", out var choices) ||
            choices.ValueKind != JsonValueKind.Array ||
            choices.GetArrayLength() == 0)
        {
            return null;
        }

        var choice = choices[0];
        var finishReason = ReadString(choice, "finish_reason");
        if (!choice.TryGetProperty("delta", out var delta))
        {
            return new OpenAiStreamDelta(null, null, finishReason, id, created, model);
        }

        return new OpenAiStreamDelta(
            ReadString(delta, "content"),
            ReadFirstString(delta, ["reasoning", "reasoning_content", "reasoning_text", "thinking"]),
            finishReason,
            id,
            created,
            model);
    }

    private static string? ReadFirstString(JsonElement element, string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var value = ReadString(element, propertyName);
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String ? property.GetString() : property.GetRawText();
    }
}
