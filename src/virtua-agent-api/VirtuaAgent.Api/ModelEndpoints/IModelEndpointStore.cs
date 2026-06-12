namespace VirtuaAgent.ModelEndpoints;

public interface IModelEndpointStore
{
    Task<IReadOnlyList<ModelEndpointDefinition>> ListAsync(CancellationToken cancellationToken = default);
    Task<ModelEndpointDefinition?> GetAsync(string id, CancellationToken cancellationToken = default);
    Task<ModelEndpointDefinition> SaveAsync(SaveModelEndpointRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
}
