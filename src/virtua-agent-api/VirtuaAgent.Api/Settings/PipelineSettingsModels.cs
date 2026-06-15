using System.Text.Json.Serialization;

namespace VirtuaAgent.Settings;

public sealed record PipelineSettingsDefinition
{
    public string? PipelineProtocol { get; init; }
}

public sealed record PipelineSettingsResponse
{
    [JsonPropertyName("pipeline_protocol")]
    public string? PipelineProtocol { get; init; }

    [JsonPropertyName("built_in_pipeline_protocol")]
    public string BuiltInPipelineProtocol { get; init; } = "";
}

public sealed record SavePipelineSettingsRequest
{
    [JsonPropertyName("pipeline_protocol")]
    public string? PipelineProtocol { get; init; }
}
