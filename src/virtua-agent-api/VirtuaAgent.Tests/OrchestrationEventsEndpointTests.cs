using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VirtuaAgent.Tracing;

namespace VirtuaAgent.Tests;

public sealed class OrchestrationEventsEndpointTests
{
    [Fact]
    public async Task EventsEndpointStreamsPublishedTraceEvents()
    {
        var hub = new ActiveTraceHub();
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<ActiveTraceHub>();
                    services.AddSingleton(hub);
                });
            });

        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/orchestrations/run_test/events");
        var responseTask = client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        using var response = await responseTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("text/event-stream", response.Content.Headers.ContentType!.MediaType);

        await hub.PublishAsync("run_test", TraceEventRecord.Create("stage_started", """{"stage_index":0}"""));

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);
        var eventLine = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5));
        var dataLine = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("event: stage_started", eventLine);
        Assert.Equal("""data: {"stage_index":0}""", dataLine);
    }
}
