namespace VirtuaAgent.Tracing;

public interface ITraceStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task CreateRunAsync(RunRecord run, CancellationToken cancellationToken = default);
    Task AppendEventAsync(string runId, TraceEventRecord traceEvent, CancellationToken cancellationToken = default);
    Task AppendReasoningAsync(string runId, ReasoningRecord reasoning, CancellationToken cancellationToken = default);
    Task CompleteRunAsync(string runId, string responseJson, CancellationToken cancellationToken = default);
    Task FailRunAsync(string runId, string errorJson, CancellationToken cancellationToken = default);
    Task<RunRecord?> GetRunAsync(string runId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RunRecord>> ListRunsAsync(string? status, string? clientId, int limit, CancellationToken cancellationToken = default);
    Task<int> ClearRunsAsync(CancellationToken cancellationToken = default);
}
