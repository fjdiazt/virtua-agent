using System.Text.Json.Serialization;

namespace VirtuaAgent.OpenAi;

public sealed record OpenAiErrorResponse(OpenAiError Error);

public sealed record OpenAiError
{
    public string Message { get; init; } = "";
    public string Type { get; init; } = "invalid_request_error";
    public string? Param { get; init; }
    public string? Code { get; init; }

    [JsonPropertyName("virtua_agent")]
    public object? VirtuaAgent { get; init; }
}
