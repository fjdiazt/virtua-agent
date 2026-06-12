using VirtuaAgent.OpenAi;
using VirtuaAgent.Tracing;

namespace VirtuaAgent.Endpoints;

public static class OrchestrationRunsEndpoint
{
    public static async Task<IResult> GetAsync(string runId, ITraceStore traceStore, CancellationToken cancellationToken)
    {
        var run = await traceStore.GetRunAsync(runId, cancellationToken);
        return run is null
            ? Results.NotFound(new OpenAiErrorResponse(new OpenAiError
            {
                Message = $"Run {runId} was not found.",
                Type = "invalid_request_error",
                Param = "run_id",
                Code = "run_not_found"
            }))
            : Results.Json(run, JsonOptions.Default);
    }

    public static async Task<IResult> ListAsync(
        string? status,
        [Microsoft.AspNetCore.Mvc.FromQuery(Name = "client_id")] string? clientId,
        int? limit,
        ITraceStore traceStore,
        CancellationToken cancellationToken)
    {
        var cappedLimit = Math.Clamp(limit ?? 50, 1, 200);
        var runs = await traceStore.ListRunsAsync(status, clientId, cappedLimit, cancellationToken);
        return Results.Json(runs, JsonOptions.Default);
    }

    public static async Task<IResult> ClearAsync(ITraceStore traceStore, CancellationToken cancellationToken)
    {
        var deleted = await traceStore.ClearRunsAsync(cancellationToken);
        return Results.Json(new ClearRunsResponse(deleted), JsonOptions.Default);
    }

    public sealed record ClearRunsResponse(int Deleted);
}
