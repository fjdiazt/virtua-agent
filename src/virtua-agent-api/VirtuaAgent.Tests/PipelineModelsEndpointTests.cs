using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VirtuaAgent.PipelineModels;

namespace VirtuaAgent.Tests;

public sealed class PipelineModelsEndpointTests
{
    [Fact]
    public async Task DeleteSupportsModelIdsContainingSlash()
    {
        var store = new InMemoryPipelineModelStore();
        await store.SaveAsync(new PipelineModelDefinition
        {
            Id = "virtua-agent/delete-test",
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
                    services.RemoveAll<IPipelineModelStore>();
                    services.AddSingleton<IPipelineModelStore>(store);
                });
            });

        var response = await factory.CreateClient().DeleteAsync("/v1/pipeline-models/virtua-agent/delete-test");
        var remaining = await store.ListAsync();

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(remaining);
    }

    [Fact]
    public async Task SaveAndListPreservesPipelineDefaultModel()
    {
        var store = new InMemoryPipelineModelStore();
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IPipelineModelStore>();
                    services.AddSingleton<IPipelineModelStore>(store);
                });
            });
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/v1/pipeline-models", new PipelineModelDefinition
        {
            Id = "virtua-agent/default-test",
            Pipeline = new PipelineRequestDto
            {
                DefaultModel = "local-model",
                DefaultTemperature = 0.2,
                DefaultMaxTokens = 128,
                Stages = [new PipelineStageRequestDto { Type = "single_agent" }]
            }
        }, OpenAi.JsonOptions.Default);
        var models = await client.GetFromJsonAsync<List<PipelineModelDefinition>>("/v1/pipeline-models", OpenAi.JsonOptions.Default);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(models);
        Assert.Contains(models, model => model.Id == "virtua-agent/default-test" && model.Pipeline!.DefaultModel == "local-model");
        Assert.Contains(models, model => model.Id == "virtua-agent/default-test" && model.Pipeline!.DefaultTemperature == 0.2);
        Assert.Contains(models, model => model.Id == "virtua-agent/default-test" && model.Pipeline!.DefaultMaxTokens == 128);
    }

    [Theory]
    [InlineData("pipeline.default_model")]
    [InlineData("pipeline.stages[0].agent.model")]
    public async Task SaveRejectsVirtuaAgentModelsInsidePipeline(string param)
    {
        var store = new InMemoryPipelineModelStore();
        await store.SaveAsync(new PipelineModelDefinition
        {
            Id = "virtua-agent/other",
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
                    services.RemoveAll<IPipelineModelStore>();
                    services.RemoveAll<PipelinePresetCatalog>();
                    services.AddSingleton<IPipelineModelStore>(store);
                    services.AddSingleton(services => new PipelinePresetCatalog([], services.GetRequiredService<IPipelineModelStore>()));
                });
            });
        var client = factory.CreateClient();
        var pipeline = param == "pipeline.default_model"
            ? new PipelineRequestDto
            {
                DefaultModel = "virtua-agent/other",
                Stages = [new PipelineStageRequestDto { Type = "single_agent" }]
            }
            : new PipelineRequestDto
            {
                Stages =
                [
                    new PipelineStageRequestDto
                    {
                        Type = "single_agent",
                        Agent = new AgentRequestDto { Model = "virtua-agent/other" }
                    }
                ]
            };

        var response = await client.PostAsJsonAsync("/v1/pipeline-models", new PipelineModelDefinition
        {
            Id = "virtua-agent/nested-test",
            Pipeline = pipeline
        }, OpenAi.JsonOptions.Default);
        var body = await response.Content.ReadFromJsonAsync<OpenAi.OpenAiErrorResponse>(OpenAi.JsonOptions.Default);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("nested_pipeline_model", body!.Error.Code);
        Assert.Equal(param, body.Error.Param);
    }

    [Fact]
    public async Task SaveRejectsSelfReferenceInsidePipeline()
    {
        var store = new InMemoryPipelineModelStore();
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IPipelineModelStore>();
                    services.RemoveAll<PipelinePresetCatalog>();
                    services.AddSingleton<IPipelineModelStore>(store);
                    services.AddSingleton(services => new PipelinePresetCatalog([], services.GetRequiredService<IPipelineModelStore>()));
                });
            });
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/v1/pipeline-models", new PipelineModelDefinition
        {
            Id = "virtua-agent/self",
            Pipeline = new PipelineRequestDto
            {
                Stages =
                [
                    new PipelineStageRequestDto
                    {
                        Type = "single_agent",
                        Agent = new AgentRequestDto { Model = "virtua-agent/self" }
                    }
                ]
            }
        }, OpenAi.JsonOptions.Default);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
