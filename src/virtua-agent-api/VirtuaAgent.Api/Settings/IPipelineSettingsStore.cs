namespace VirtuaAgent.Settings;

public interface IPipelineSettingsStore
{
    Task<PipelineSettingsDefinition> GetAsync(CancellationToken cancellationToken = default);

    Task<PipelineSettingsDefinition> SaveAsync(
        SavePipelineSettingsRequest request,
        CancellationToken cancellationToken = default);
}
