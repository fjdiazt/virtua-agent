using VirtuaAgent.OpenAi;
using VirtuaAgent.Orchestration;
using VirtuaAgent.Settings;

namespace VirtuaAgent.Endpoints;

public static class PipelineSettingsEndpoint
{
    public static async Task<IResult> GetAsync(
        IPipelineSettingsStore store,
        CancellationToken cancellationToken)
    {
        var settings = await store.GetAsync(cancellationToken);
        return Results.Json(ToResponse(settings), JsonOptions.Default);
    }

    public static async Task<IResult> SaveAsync(
        SavePipelineSettingsRequest request,
        IPipelineSettingsStore store,
        CancellationToken cancellationToken)
    {
        var settings = await store.SaveAsync(request, cancellationToken);
        return Results.Json(ToResponse(settings), JsonOptions.Default);
    }

    private static PipelineSettingsResponse ToResponse(PipelineSettingsDefinition settings) => new()
    {
        PipelineProtocol = Normalize(settings.PipelineProtocol),
        BuiltInPipelineProtocol = PipelinePromptProtocol.Default.Instructions.Trim()
    };

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
