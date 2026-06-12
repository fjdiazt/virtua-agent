using VirtuaAgent.Tracing;

namespace VirtuaAgent.Tests;

public sealed class SqliteTraceStoreTests
{
    [Fact]
    public async Task StoresRunAndEvents()
    {
        var store = new SqliteTraceStore("Data Source=:memory:");
        await store.InitializeAsync();

        var run = RunRecord.Started("run_test", "req_test", "client-a", "hello", true);
        await store.CreateRunAsync(run);
        await store.AppendEventAsync("run_test", TraceEventRecord.Create("stage_started", """{"stage_index":0}"""));
        await store.AppendReasoningAsync("run_test", ReasoningRecord.Create(0, 0, 0, "Draft", "reason "));
        await store.AppendReasoningAsync("run_test", ReasoningRecord.Create(0, 0, 0, "Draft", "one"));
        await store.CompleteRunAsync("run_test", """{"ok":true}""");

        var loaded = await store.GetRunAsync("run_test");

        Assert.NotNull(loaded);
        Assert.Equal("completed", loaded!.Status);
        Assert.Single(loaded.Events);
        Assert.Equal("stage_started", loaded.Events[0].Type);
        var reasoning = Assert.Single(loaded.Reasonings);
        Assert.Equal("Draft", reasoning.Label);
        Assert.Equal("reason one", reasoning.Content);
    }

    [Fact]
    public async Task ClearRunsDeletesRunsAndEvents()
    {
        var store = new SqliteTraceStore("Data Source=:memory:");
        await store.InitializeAsync();
        await store.CreateRunAsync(RunRecord.Started("run_test", "req_test", "client-a", "hello", true));
        await store.AppendEventAsync("run_test", TraceEventRecord.Create("stage_started", """{"stage_index":0}"""));
        await store.AppendReasoningAsync("run_test", ReasoningRecord.Create(0, 0, 0, "Draft", "reason"));

        var deleted = await store.ClearRunsAsync();
        var runs = await store.ListRunsAsync(null, null, 10);

        Assert.Equal(1, deleted);
        Assert.Empty(runs);
    }
}
