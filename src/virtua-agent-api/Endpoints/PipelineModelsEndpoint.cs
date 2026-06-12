using VirtuaAgent.PipelineModels;
using VirtuaAgent.OpenAi;
using VirtuaAgent.Orchestration;

namespace VirtuaAgent.Endpoints;

public static class PipelineModelsEndpoint
{
    public static async Task<IResult> ListAsync(IPipelineModelStore store, CancellationToken cancellationToken)
    {
        var models = await store.ListAsync(cancellationToken);
        return Results.Json(models, JsonOptions.Default);
    }

    public static async Task<IResult> SaveAsync(
        PipelineModelDefinition model,
        IPipelineModelStore store,
        PipelinePresetCatalog presetCatalog,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.Id))
        {
            return Results.BadRequest(new OpenAiErrorResponse(new OpenAiError
            {
                Message = "Model id is required.",
                Type = "invalid_request_error",
                Param = "id",
                Code = "model_id_required"
            }));
        }

        if (model.Pipeline is null || model.Pipeline.Stages.Count == 0)
        {
            return Results.BadRequest(new OpenAiErrorResponse(new OpenAiError
            {
                Message = "Pipeline must contain at least one stage.",
                Type = "invalid_request_error",
                Param = "pipeline.stages",
                Code = "empty_pipeline"
            }));
        }

        try
        {
            await PipelineModelValidator.EnsureNoNestedPipelineModelsAsync(model.Pipeline, presetCatalog, model.Id, cancellationToken);
            await store.SaveAsync(model, cancellationToken);
            return Results.Json(model, JsonOptions.Default);
        }
        catch (PipelineValidationException ex)
        {
            return Results.Json(new OpenAiErrorResponse(new OpenAiError
            {
                Message = ex.Message,
                Type = "invalid_request_error",
                Param = ex.Param,
                Code = ex.Code
            }), JsonOptions.Default, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    public static async Task<IResult> DeleteAsync(string id, IPipelineModelStore store, CancellationToken cancellationToken)
    {
        var deleted = await store.DeleteAsync(id, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    }
}
