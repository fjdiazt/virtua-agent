using VirtuaAgent.OpenAi;
using VirtuaAgent.PipelineModels;

namespace VirtuaAgent.Orchestration;

public sealed record PipelineDefinition(
    string? DefaultModel,
    double? DefaultTemperature,
    int? DefaultMaxTokens,
    List<PipelineStageDefinition> Stages);

public sealed record PipelineStageDefinition(
    string Type,
    string? Name,
    int Repeat,
    string? Instructions,
    AgentRequestDto? Agent,
    string? AgentSelection,
    int? Seed,
    List<AgentRequestDto> Agents);

public sealed record PipelineContext(string RunId, List<ChatMessageDto> OriginalMessages)
{
    public string? CurrentAnswer { get; set; }
}

public sealed class PipelineValidationException(string message, string param, string code) : Exception(message)
{
    public string Param { get; } = param;
    public string Code { get; } = code;
}
