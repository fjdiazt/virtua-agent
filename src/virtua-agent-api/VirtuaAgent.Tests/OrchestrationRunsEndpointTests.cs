using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VirtuaAgent.OpenAi;
using VirtuaAgent.Tracing;

namespace VirtuaAgent.Tests;

public sealed class OrchestrationRunsEndpointTests
{
    [Fact]
    public async Task GetRunReturnsRunWithEvents()
    {
        await using var store = await SeedStoreAsync();
        await using var factory = FactoryWith(store);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/v1/orchestrations/run_test");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var run = await response.Content.ReadFromJsonAsync<RunRecord>(JsonOptions.Default);
        Assert.Equal("run_test", run!.RunId);
        Assert.Single(run.Events);
    }

    [Fact]
    public async Task ListRunsFiltersByStatusAndClient()
    {
        await using var store = await SeedStoreAsync();
        await using var factory = FactoryWith(store);
        var client = factory.CreateClient();

        var runs = await client.GetFromJsonAsync<List<RunRecord>>("/v1/orchestrations?status=completed&client_id=client-a&limit=10", JsonOptions.Default);

        Assert.Single(runs!);
        Assert.Equal("run_test", runs![0].RunId);
    }

    [Fact]
    public async Task ClearRunsDeletesStoredRuns()
    {
        await using var store = await SeedStoreAsync();
        await using var factory = FactoryWith(store);
        var client = factory.CreateClient();

        var response = await client.DeleteAsync("/v1/orchestrations");
        var runs = await client.GetFromJsonAsync<List<RunRecord>>("/v1/orchestrations", JsonOptions.Default);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"deleted\":2", body);
        Assert.Empty(runs!);
    }


    private static async Task<SqliteTraceStore> SeedStoreAsync()
    {
        var store = new SqliteTraceStore("Data Source=:memory:");
        await store.InitializeAsync();
        await store.CreateRunAsync(RunRecord.Started("run_test", "req_test", "client-a", "hello", true));
        await store.AppendEventAsync("run_test", TraceEventRecord.Create("stage_started", """{"stage_index":0}"""));
        await store.CompleteRunAsync("run_test", """{"ok":true}""");
        await store.CreateRunAsync(RunRecord.Started("run_other", "req_other", "client-b", "other", true));
        return store;
    }

    private static WebApplicationFactory<Program> FactoryWith(ITraceStore store) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<ITraceStore>();
                    services.AddSingleton(store);
                });
            });
}
