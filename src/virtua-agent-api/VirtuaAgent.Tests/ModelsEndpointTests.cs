using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VirtuaAgent.PipelineModels;
using VirtuaAgent.OpenAi;
using VirtuaAgent.Upstream;

namespace VirtuaAgent.Tests;

public sealed class ModelsEndpointTests
{
    [Fact]
    public async Task GetModelsProxiesUpstreamModels()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IOpenAiCompatibleUpstreamClient>();
                    services.AddSingleton<IOpenAiCompatibleUpstreamClient>(new FakeUpstreamClient());
                });
            });

        var response = await factory.CreateClient().GetAsync("/v1/models");
        var body = await response.Content.ReadFromJsonAsync<ModelListResponse>(JsonOptions.Default);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("local-model", body!.Data[0].Id);
    }

    [Fact]
    public async Task GetModelsIncludesConfiguredPipelinePresetModels()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["PipelinePresets:0:Id"] = "virtua-agent/editor",
                        ["PipelinePresets:0:OwnedBy"] = "virtua-agent",
                        ["PipelinePresets:0:Pipeline:Stages:0:Type"] = "single_agent",
                        ["PipelinePresets:0:Pipeline:Stages:0:Agent:Model"] = "local-model"
                    });
                });
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IOpenAiCompatibleUpstreamClient>();
                    services.AddSingleton<IOpenAiCompatibleUpstreamClient>(new FakeUpstreamClient());
                });
            });

        var response = await factory.CreateClient().GetAsync("/v1/models");
        var body = await response.Content.ReadFromJsonAsync<ModelListResponse>(JsonOptions.Default);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(body!.Data, model => model.Id == "local-model" && model.OwnedBy == "local");
        Assert.Contains(body.Data, model => model.Id == "virtua-agent/editor" && model.OwnedBy == "virtua-agent");
    }

    [Fact]
    public async Task GetModelsIncludesSqliteVirtuaAgentModels()
    {
        var store = new InMemoryPipelineModelStore();
        await store.SaveAsync(new PipelineModelDefinition
        {
            Id = "virtua-agent/sqlite-editor",
            OwnedBy = "virtua-agent",
            Pipeline = new PipelineRequestDto
            {
                Stages = [new PipelineStageRequestDto { Type = "single_agent" }]
            }
        });
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IOpenAiCompatibleUpstreamClient>();
                    services.RemoveAll<IPipelineModelStore>();
                    services.AddSingleton<IOpenAiCompatibleUpstreamClient>(new FakeUpstreamClient());
                    services.AddSingleton<IPipelineModelStore>(store);
                });
            });

        var response = await factory.CreateClient().GetAsync("/v1/models");
        var body = await response.Content.ReadFromJsonAsync<ModelListResponse>(JsonOptions.Default);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(body!.Data, model => model.Id == "local-model" && model.OwnedBy == "local");
        Assert.Contains(body.Data, model => model.Id == "virtua-agent/sqlite-editor" && model.OwnedBy == "virtua-agent");
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
