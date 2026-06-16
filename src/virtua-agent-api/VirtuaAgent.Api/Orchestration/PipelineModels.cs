using VirtuaAgent.OpenAi;
using VirtuaAgent.PipelineModels;

namespace VirtuaAgent.Orchestration;

public sealed record PipelineDefinition(
    string? DefaultEndpointId,
    string? DefaultModel,
    double? DefaultTemperature,
    double? DefaultTopP,
    int? DefaultTopK,
    double? DefaultMinP,
    double? DefaultRepeatPenalty,
    int? DefaultMaxTokens,
    string? Protocol,
    List<PipelineStageDefinition> Stages);

public sealed record PipelineStageDefinition(
    string Type,
    string? Name,
    int Repeat,
    string? Instructions,
    string? Protocol,
    PipelineStageInputDefinition? Input,
    AgentRequestDto? Agent,
    string? AgentSelection,
    int? Seed,
    List<AgentRequestDto> Agents);

public sealed record PipelineStageInputDefinition(string? OriginalMessages, string? PriorStageOutput)
{
    public static PipelineStageInputDefinition DefaultForExecution(int executionIndex) =>
        executionIndex == 0
            ? new PipelineStageInputDefinition("full", "none")
            : new PipelineStageInputDefinition("text", "last");

    public static PipelineStageInputDefinition Resolve(PipelineStageInputDefinition? input, int executionIndex)
    {
        var defaults = DefaultForExecution(executionIndex);
        return new PipelineStageInputDefinition(
            input?.OriginalMessages ?? defaults.OriginalMessages,
            input?.PriorStageOutput ?? defaults.PriorStageOutput);
    }
}

public sealed record PipelineContext(string RunId, List<ChatMessageDto> OriginalMessages)
{
    public string? CurrentAnswer { get; set; }
    public string? CurrentAnswerLabel { get; set; }
}

public sealed class PipelineValidationException(string message, string param, string code) : Exception(message)
{
    public string Param { get; } = param;
    public string Code { get; } = code;
}
