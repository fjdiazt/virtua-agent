using VirtuaAgent.PipelineModels;
using VirtuaAgent.OpenAi;
using VirtuaAgent.Upstream;

namespace VirtuaAgent.Tests;

public sealed class ModelCatalogClientTests
{
    [Fact]
    public async Task ListModelsAsyncIncludesSavedVirtuaAgentModels()
    {
        var store = new InMemoryPipelineModelStore();
        await store.SaveAsync(new PipelineModelDefinition
        {
            Id = "virtua-agent/editor",
            Pipeline = new PipelineRequestDto
            {
                Stages = [new PipelineStageRequestDto { Type = "single_agent" }]
            }
        });
        var catalog = new PipelinePresetCatalog([], store);
        var client = new ModelCatalogClient(new FakeUpstreamClient(), catalog);

        var models = await client.ListModelsAsync();

        Assert.Contains("local-model", models);
        Assert.Contains("virtua-agent/editor", models);
    }

    private sealed class FakeUpstreamClient : IOpenAiCompatibleUpstreamClient
    {
        public Task<ModelListResponse> ListModelsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ModelListResponse
            {
                Data = [new ModelDto { Id = "local-model", OwnedBy = "local" }]
            });

        public Task<ChatCompletionResponse> ChatAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task StreamChatAsync(ChatCompletionRequest request, Stream output, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class InMemoryPipelineModelStore : IPipelineModelStore
    {
        private readonly Dictionary<string, PipelineModelDefinition> _models = new(StringComparer.OrdinalIgnoreCase);

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<PipelineModelDefinition>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PipelineModelDefinition>>(_models.Values.ToList());
        public Task<PipelineModelDefinition?> GetAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult(_models.GetValueOrDefault(id));
        public Task SaveAsync(PipelineModelDefinition model, CancellationToken cancellationToken = default)
        {
            _models[model.Id] = model;
            return Task.CompletedTask;
        }
        public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult(_models.Remove(id));
    }
}
