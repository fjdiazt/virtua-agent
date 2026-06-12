using VirtuaAgent.Orchestration;

namespace VirtuaAgent.PipelineModels;

public static class PipelineModelValidator
{
    public static async Task EnsureNoNestedPipelineModelsAsync(
        PipelineRequestDto pipeline,
        PipelinePresetCatalog catalog,
        string? currentModelId = null,
        CancellationToken cancellationToken = default)
    {
        var defaultModel = NormalizeModel(pipeline.DefaultModel);
        if (defaultModel is not null &&
            (IsCurrentModel(defaultModel, currentModelId) || await catalog.FindAsync(defaultModel, cancellationToken) is not null))
        {
            throw new PipelineValidationException(
                "Pipeline default model must be an upstream model, not a Virtua Agent model.",
                "pipeline.default_model",
                "nested_pipeline_model");
        }

        for (var index = 0; index < pipeline.Stages.Count; index++)
        {
            var stage = pipeline.Stages[index];
            await ValidateAgentAsync(stage.Agent, $"pipeline.stages[{index}].agent.model", catalog, currentModelId, cancellationToken);

            for (var agentIndex = 0; agentIndex < stage.Agents.Count; agentIndex++)
            {
                await ValidateAgentAsync(stage.Agents[agentIndex], $"pipeline.stages[{index}].agents[{agentIndex}].model", catalog, currentModelId, cancellationToken);
            }
        }
    }

    private static async Task ValidateAgentAsync(
        AgentRequestDto? agent,
        string param,
        PipelinePresetCatalog catalog,
        string? currentModelId,
        CancellationToken cancellationToken)
    {
        var model = NormalizeModel(agent?.Model);
        if (model is null)
        {
            return;
        }

        if (IsCurrentModel(model, currentModelId) || await catalog.FindAsync(model, cancellationToken) is not null)
        {
            throw new PipelineValidationException(
                "Stage model must be an upstream model, not a Virtua Agent model.",
                param,
                "nested_pipeline_model");
        }
    }

    private static string? NormalizeModel(string? model)
    {
        if (string.IsNullOrWhiteSpace(model) || string.Equals(model, "default", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return model;
    }

    private static bool IsCurrentModel(string model, string? currentModelId) =>
        !string.IsNullOrWhiteSpace(currentModelId) &&
        model.Equals(currentModelId, StringComparison.OrdinalIgnoreCase);
}
