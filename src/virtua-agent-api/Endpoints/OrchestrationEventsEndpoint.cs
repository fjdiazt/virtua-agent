using VirtuaAgent.Tracing;

namespace VirtuaAgent.Endpoints;

public static class OrchestrationEventsEndpoint
{
    public static async Task HandleAsync(string runId, HttpContext context, ActiveTraceHub hub)
    {
        context.Response.ContentType = "text/event-stream";
        var events = hub.Subscribe(runId, context.RequestAborted);
        await context.Response.Body.FlushAsync(context.RequestAborted);

        await foreach (var traceEvent in events)
        {
            await context.Response.WriteAsync($"event: {traceEvent.Type}\n", context.RequestAborted);
            await context.Response.WriteAsync($"data: {traceEvent.Json}\n\n", context.RequestAborted);
            await context.Response.Body.FlushAsync(context.RequestAborted);
        }
    }
}
