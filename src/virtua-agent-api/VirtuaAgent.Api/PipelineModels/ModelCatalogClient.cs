using VirtuaAgent.Upstream;

namespace VirtuaAgent.PipelineModels;

public sealed class ModelCatalogClient(
    IOpenAiCompatibleUpstreamClient upstreamClient,
    PipelinePresetCatalog presetCatalog)
{
    public async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        var models = await upstreamClient.ListModelsAsync(cancellationToken);
        var merged = await presetCatalog.AddToAsync(models, cancellationToken);
        return merged.Data
            .Select(model => model.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToList();
    }
}
