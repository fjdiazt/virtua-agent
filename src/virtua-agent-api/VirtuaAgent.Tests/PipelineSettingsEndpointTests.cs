using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VirtuaAgent.OpenAi;
using VirtuaAgent.Settings;

namespace VirtuaAgent.Tests;

public sealed class PipelineSettingsEndpointTests
{
    [Fact]
    public async Task GetSaveAndClearPipelineProtocol()
    {
        var store = new InMemoryPipelineSettingsStore();
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IPipelineSettingsStore>();
                    services.AddSingleton<IPipelineSettingsStore>(store);
                });
            });

        var client = factory.CreateClient();

        var initial = await client.GetFromJsonAsync<PipelineSettingsResponse>("/v1/settings", JsonOptions.Default);
        var savedResponse = await client.PutAsJsonAsync("/v1/settings", new SavePipelineSettingsRequest
        {
            PipelineProtocol = "Use short stage prompts."
        }, JsonOptions.Default);
        var saved = await savedResponse.Content.ReadFromJsonAsync<PipelineSettingsResponse>(JsonOptions.Default);
        var loaded = await client.GetFromJsonAsync<PipelineSettingsResponse>("/v1/settings", JsonOptions.Default);
        var clearedResponse = await client.PutAsJsonAsync("/v1/settings", new SavePipelineSettingsRequest
        {
            PipelineProtocol = "   "
        }, JsonOptions.Default);
        var cleared = await clearedResponse.Content.ReadFromJsonAsync<PipelineSettingsResponse>(JsonOptions.Default);

        Assert.NotNull(initial);
        Assert.Null(initial!.PipelineProtocol);
        Assert.Contains("You are executing one stage in a pipeline.", initial.BuiltInPipelineProtocol, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, savedResponse.StatusCode);
        Assert.Equal("Use short stage prompts.", saved!.PipelineProtocol);
        Assert.Equal("Use short stage prompts.", loaded!.PipelineProtocol);
        Assert.Equal(HttpStatusCode.OK, clearedResponse.StatusCode);
        Assert.Null(cleared!.PipelineProtocol);
    }

    private sealed class InMemoryPipelineSettingsStore : IPipelineSettingsStore
    {
        private PipelineSettingsDefinition _settings = new();

        public Task<PipelineSettingsDefinition> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_settings);

        public Task<PipelineSettingsDefinition> SaveAsync(
            SavePipelineSettingsRequest request,
            CancellationToken cancellationToken = default)
        {
            _settings = new PipelineSettingsDefinition
            {
                PipelineProtocol = string.IsNullOrWhiteSpace(request.PipelineProtocol)
                    ? null
                    : request.PipelineProtocol.Trim()
            };
            return Task.FromResult(_settings);
        }
    }
}
