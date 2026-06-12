using VirtuaAgent.OpenAi;

namespace VirtuaAgent.PipelineModels;

public sealed class PipelinePresetCatalog(IReadOnlyList<PipelineModelDefinition> configModels, IPipelineModelStore modelStore)
{
    public async Task<PipelineModelDefinition?> FindAsync(string? modelId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return null;
        }

        return await modelStore.GetAsync(modelId, cancellationToken)
            ?? configModels.FirstOrDefault(preset => preset.Id.Equals(modelId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<ModelListResponse> AddToAsync(ModelListResponse upstreamModels, CancellationToken cancellationToken = default)
    {
        var existing = upstreamModels.Data.Select(model => model.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var data = upstreamModels.Data.ToList();
        foreach (var model in configModels.Concat(await modelStore.ListAsync(cancellationToken)))
        {
            if (string.IsNullOrWhiteSpace(model.Id) || !existing.Add(model.Id))
            {
                continue;
            }

            data.Add(new ModelDto
            {
                Id = model.Id,
                OwnedBy = string.IsNullOrWhiteSpace(model.OwnedBy) ? "virtua_agent": model.OwnedBy
            });
        }

        return upstreamModels with { Data = data };
    }
}

public sealed record PipelineModelDefinition
{
    public string Id { get; init; } = "";
    public string? OwnedBy { get; init; }
    public PipelineRequestDto? Pipeline { get; init; }
}

public interface IPipelineModelStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PipelineModelDefinition>> ListAsync(CancellationToken cancellationToken = default);
    Task<PipelineModelDefinition?> GetAsync(string id, CancellationToken cancellationToken = default);
    Task SaveAsync(PipelineModelDefinition model, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
}
