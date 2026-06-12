using System.Text.Json.Serialization;

namespace VirtuaAgent.PipelineModels;

public sealed record OrchestrationRequestDto
{
    [JsonPropertyName("include_virtua_agent")]
    public bool IncludeVirtuaAgent { get; init; }

    public bool? Store { get; init; }
    public PipelineRequestDto? Pipeline { get; init; }
}

public sealed record PipelineRequestDto
{
    [JsonPropertyName("default_endpoint_id")]
    public string? DefaultEndpointId { get; init; }

    [JsonPropertyName("default_model")]
    public string? DefaultModel { get; init; }

    [JsonPropertyName("default_temperature")]
    public double? DefaultTemperature { get; init; }

    [JsonPropertyName("default_max_tokens")]
    public int? DefaultMaxTokens { get; init; }

    public List<PipelineStageRequestDto> Stages { get; init; } = [];
}

public sealed record PipelineStageRequestDto
{
    public string Type { get; init; } = "";
    public string? Name { get; init; }
    public int Repeat { get; init; } = 1;
    public string? Instructions { get; init; }

    [JsonPropertyName("agent_selection")]
    public string? AgentSelection { get; init; }

    public int? Seed { get; init; }
    public AgentRequestDto? Agent { get; init; }
    public List<AgentRequestDto> Agents { get; init; } = [];
}

public sealed record AgentRequestDto
{
    [JsonPropertyName("endpoint_id")]
    public string? EndpointId { get; init; }

    public string? Model { get; init; }
    public double? Temperature { get; init; }

    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; init; }
}

public sealed record VirtuaAgentResponseDto
{
    [JsonPropertyName("run_id")]
    public string RunId { get; init; } = "";

    [JsonPropertyName("trace_url")]
    public string TraceUrl { get; init; } = "";
}
