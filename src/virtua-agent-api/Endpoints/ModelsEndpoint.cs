using VirtuaAgent.OpenAi;
using VirtuaAgent.PipelineModels;
using VirtuaAgent.Upstream;

namespace VirtuaAgent.Endpoints;

public static class ModelsEndpoint
{
    public static async Task<IResult> ListAsync(
        IOpenAiCompatibleUpstreamClient upstreamClient,
        PipelinePresetCatalog presetCatalog,
        CancellationToken cancellationToken)
    {
        try
        {
            var models = await upstreamClient.ListModelsAsync(cancellationToken);
            models = await presetCatalog.AddToAsync(models, cancellationToken);
            return Results.Json(models, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            var error = new OpenAiErrorResponse(new OpenAiError
            {
                Message = ex is TaskCanceledException or TimeoutException
                    ? "Upstream model list timed out. The upstream may be offline or warming."
                    : ex.Message,
                Type = "server_error",
                Code = "upstream_error"
            });

            return Results.Json(error, JsonOptions.Default, statusCode: StatusCodes.Status502BadGateway);
        }
    }
}
