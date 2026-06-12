using VirtuaAgent.Tracing;

namespace VirtuaAgent.Tests;

public sealed class ActiveTraceHubTests
{
    [Fact]
    public async Task SubscriberReceivesPublishedEvents()
    {
        var hub = new ActiveTraceHub();
        var stream = hub.Subscribe("run_test", CancellationToken.None);

        await hub.PublishAsync("run_test", TraceEventRecord.Create("run_started", """{"run_id":"run_test"}"""));

        var enumerator = stream.GetAsyncEnumerator();
        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal("run_started", enumerator.Current.Type);
        await enumerator.DisposeAsync();
    }
}
