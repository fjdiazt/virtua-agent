namespace VirtuaAgent.Tracing;

public sealed record RunRecord(
    string RunId,
    string RequestId,
    string? ClientId,
    string Status,
    string Preview,
    bool Store,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? RequestJson,
    string? ResponseJson,
    List<TraceEventRecord> Events,
    List<ReasoningRecord> Reasonings)
{
    public static RunRecord Started(string runId, string requestId, string? clientId, string preview, bool store) =>
        new(runId, requestId, clientId, "running", preview, store, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, null, [], []);
}

public sealed record TraceEventRecord(string Type, string Json, DateTimeOffset CreatedAt)
{
    public static TraceEventRecord Create(string type, string json) => new(type, json, DateTimeOffset.UtcNow);
}

public sealed record ReasoningRecord(
    int StageIndex,
    int ExecutionIndex,
    int IterationIndex,
    string Label,
    string Content,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static ReasoningRecord Create(int stageIndex, int executionIndex, int iterationIndex, string label, string content) =>
        new(stageIndex, executionIndex, iterationIndex, label, content, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
}
